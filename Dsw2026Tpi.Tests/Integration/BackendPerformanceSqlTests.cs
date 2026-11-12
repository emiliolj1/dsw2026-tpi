using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
[Trait("Purpose", "Performance")]
public sealed class BackendPerformanceSqlTests(
    SqlServerFixture fixture,
    ITestOutputHelper output) : IClassFixture<SqlServerFixture>
{
    private const long PatientDni = 67_000_050;
    private const int Warmups = 3;
    private const int Samples = 20;
    private const double LimitMs = 3_000;

    [Fact]
    public async Task CatalogAvailabilityAndAppointmentReads_StayBelowThreeSeconds()
    {
        await using var factory = fixture.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IClinicClock>();
                services.AddSingleton<IClinicClock>(new FixedClinicClock());
            }));

        using var admin = factory.CreateClient();
        using var patient = factory.CreateClient();

        await LoginAsync(admin, "/api/auth/admin/login", new
        {
            email = SqlServerWebApplicationFactory.AdminEmail,
            password = SqlServerWebApplicationFactory.AdminPassword
        });

        await LoginAsync(patient, "/api/auth/patient/login", new
        {
            email = "c04-performance@example.test",
            dni = PatientDni
        });

        var doctorId = await SeedAsync();

        var queries = new (string Name, HttpClient Client, string Url)[]
        {
            ("especialidades", admin, "/api/specialties?pageSize=10&pageIndex=0"),
            ("medicos", admin, "/api/doctors?pageSize=10&pageIndex=0"),
            ("turnos-libres", patient, $"/api/availabilities?doctorId={doctorId}"),
            ("mis-citas", patient, $"/api/appointments/patient?dni={PatientDni}"),
            ("busqueda-admin", admin, "/api/appointments/search?status=BOOKED&pageSize=10&pageIndex=0")
        };

        var failures = new List<string>();

        foreach (var query in queries)
        {
            if (!await MeasureAsync(query.Name, query.Client, query.Url))
                failures.Add(query.Name);
        }

        Assert.True(failures.Count == 0,
            $"Respuestas fallidas o de 3 segundos en adelante: {string.Join(", ", failures)}");
    }

    private async Task<Guid> SeedAsync()
    {
        await using var db = fixture.CreateApplicationContext();
        var signedInPatient = await db.Set<Patient>()
            .SingleAsync(p => p.Dni == PatientDni);

        var specialties = Enumerable.Range(1, 20)
            .Select(i => new Speciality(
                $"Especialidad sintética {i:D2}", "Datos sintéticos de rendimiento"))
            .ToArray();
        db.AddRange(specialties);

        var slotCount = 0;
        var appointmentCount = 0;
        var firstDoctorId = Guid.Empty;
        var firstDay = new DateOnly(2030, 6, 1);
        var startTime = new TimeOnly(9, 0);
        var endTime = new TimeOnly(11, 0);

        for (var i = 0; i < 100; i++)
        {
            var doctor = new Doctor(
                $"Médico sintético {i + 1:D3}", $"PERF-{i + 1:D3}", specialties[i % 20]);
            db.Add(doctor);

            var patient = signedInPatient;
            if (i == 0)
            {
                firstDoctorId = doctor.Id;
            }
            else
            {
                patient = new Patient(Guid.NewGuid(), 68_000_000L + i, $"Paciente sintético {i:D3}");
                db.Add(patient);
            }

            var rules = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday }
                .ToDictionary(day => day,
                    day => new AvailabilityRule(doctor.Id, 2030, 6, day, startTime, endTime));
            db.AddRange(rules.Values);

            var booked = 0;
            for (var date = firstDay; date.Month == 6; date = date.AddDays(1))
            {
                if (!rules.TryGetValue(date.DayOfWeek, out var rule))
                    continue;

                for (var time = startTime; time < endTime; time = time.AddMinutes(30))
                {
                    var slot = new Availability(doctor.Id, date, time, availabilityRuleId: rule.Id);
                    db.Add(slot);
                    slotCount++;

                    if (booked < 10)
                    {
                        slot.Book();
                        db.Add(new Appointment(doctor.Id, slot.Id, patient.Id, "Consulta sintética"));
                        booked++;
                        appointmentCount++;
                    }
                }
            }
        }

        await db.SaveChangesAsync();
        Assert.Equal(3_200, slotCount);
        Assert.Equal(1_000, appointmentCount);
        output.WriteLine($"C04 DATA: 20 especialidades; 100 médicos; {slotCount} turnos; {appointmentCount} citas");
        return firstDoctorId;
    }

    private async Task<bool> MeasureAsync(string name, HttpClient client, string url)
    {
        for (var i = 0; i < Warmups; i++)
        {
            using var response = await client.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.OK,
                $"Calentamiento de {name}: HTTP {(int)response.StatusCode}");
        }

        var times = new double[Samples];
        var failedStatuses = new List<int>();

        for (var i = 0; i < Samples; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            // GetAsync completa cuando ya se recibió el cuerpo de la respuesta.
            using var response = await client.GetAsync(url);
            stopwatch.Stop();

            times[i] = stopwatch.Elapsed.TotalMilliseconds;
            if (response.StatusCode != HttpStatusCode.OK)
                failedStatuses.Add((int)response.StatusCode);
        }

        Array.Sort(times);
        var median = (times[Samples / 2 - 1] + times[Samples / 2]) / 2;
        var p95 = times[(int)Math.Ceiling(Samples * 0.95) - 1];
        var max = times[^1];
        var statuses = failedStatuses.Count == 0 ? "-" : string.Join(",", failedStatuses);

        output.WriteLine(FormattableString.Invariant(
            $"C04 PERF {name}: n={Samples}; mediana={median:F2} ms; p95={p95:F2} ms; max={max:F2} ms; fallos={failedStatuses.Count}; HTTP={statuses}"));

        return failedStatuses.Count == 0 && max < LimitMs;
    }

    private static async Task LoginAsync(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = session.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed class FixedClinicClock : IClinicClock
    {
        public DateTimeOffset GetCurrentLocalDateTime() =>
            new(2030, 6, 1, 8, 0, 0, TimeSpan.FromHours(-3));
    }
}