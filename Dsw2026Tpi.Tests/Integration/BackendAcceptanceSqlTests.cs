using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class BackendAcceptanceSqlTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task AdminCreatesPlanning_PatientBooksCancelsAndRebooks()
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

        var suffix = Guid.NewGuid().ToString("N");
        const long dni = 67_000_041;
        await LoginAsync(patient, "/api/auth/patient/login", new
        {
            email = $"c04-{suffix}@example.test",
            dni
        });

        var specialty = await PostAsync<SpecialityModel.Response>(
            admin, "/api/specialties",
            new { name = $"Especialidad C04 {suffix}", description = "Catálogo sintético de aceptación" });

        var doctor = await PostAsync<DoctorModel.Response>(
            admin, "/api/doctors",
            new { name = "Médico de aceptación", licenseNumber = $"C04-{suffix}", specialtyId = specialty.Id });
        Assert.Equal<Guid?>(specialty.Id, doctor.Specialty?.Id);

        var planning = await PostAsync<AvailabilityModel.PlanningResponse>(
            admin, "/api/availabilities",
            new { doctorId = doctor.Id, days = new[] { new { day = "LUNES", startTime = "10:00", endTime = "10:30" } } });
        Assert.Equal(doctor.Id, planning.DoctorId);
        Assert.Equal(2030, planning.Year);
        Assert.Equal(6, planning.Month);
        Assert.Single(planning.Days);

        var slotsUrl = $"/api/availabilities?doctorId={doctor.Id}";
        var slot = Assert.Single(
            await GetAsync<AvailabilityModel.AvailableSlotResponse[]>(patient, slotsUrl));
        Assert.Equal(new DateOnly(2030, 6, 24), slot.Date);
        Assert.Equal("10:00", slot.StartTime);
        Assert.Equal("10:30", slot.EndTime);

        var request = new
        {
            doctorId = doctor.Id,
            availabilitySlotId = slot.AvailabilitySlotId,
            patient = new { dni },
            reason = "Consulta de aceptación C04"
        };

        const string appointmentsUrl = "/api/appointments";
        var activeUrl = $"{appointmentsUrl}/patient?dni={dni}";
        var searchUrl = $"{appointmentsUrl}/search?doctorId={doctor.Id}" +
                        $"&specialtyId={specialty.Id}&dni={dni}&date=2030-06-24";

        var first = await PostAsync<AppointmentModel.Response>(patient, appointmentsUrl, request);
        Assert.Equal("BOOKED", first.AppointmentsStatus);
        Assert.Equal(slot.AvailabilitySlotId, first.AvailabilitySlotId);

        var active = Assert.Single(await GetAsync<AppointmentModel.Response[]>(patient, activeUrl));
        Assert.Equal(first.AppointmentsId, active.AppointmentsId);
        Assert.Empty(await GetAsync<AvailabilityModel.AvailableSlotResponse[]>(patient, slotsUrl));

        using var cancellation = await patient.DeleteAsync($"{appointmentsUrl}/{first.AppointmentsId}");
        Assert.Equal(HttpStatusCode.OK, cancellation.StatusCode);
        Assert.Equal("ok", await cancellation.Content.ReadAsStringAsync());
        Assert.Empty(await GetAsync<AppointmentModel.Response[]>(patient, activeUrl));

        var released = Assert.Single(
            await GetAsync<AvailabilityModel.AvailableSlotResponse[]>(patient, slotsUrl));
        Assert.Equal(slot.AvailabilitySlotId, released.AvailabilitySlotId);

        var second = await PostAsync<AppointmentModel.Response>(patient, appointmentsUrl, request);
        Assert.NotEqual(first.AppointmentsId, second.AppointmentsId);
        Assert.Equal("BOOKED", second.AppointmentsStatus);
        Assert.Equal(slot.AvailabilitySlotId, second.AvailabilitySlotId);

        var history = await GetAsync<AppointmentModel.PagedResponse>(admin, searchUrl);
        Assert.Equal(2, history.Total);
        Assert.Equal("CANCELLED", Assert.Single(history.Data,
            item => item.AppointmentsId == first.AppointmentsId).AppointmentsStatus);
        Assert.Equal("BOOKED", Assert.Single(history.Data,
            item => item.AppointmentsId == second.AppointmentsId).AppointmentsStatus);
    }

    [Fact]
    public async Task DoctorDeletionAndBooking_LeaveOneConsistentOutcome()
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

        var suffix = Guid.NewGuid().ToString("N");
        const long dni = 67_000_042;
        await LoginAsync(patient, "/api/auth/patient/login", new
        {
            email = $"c04-race-{suffix}@example.test",
            dni
        });

        var specialty = await PostAsync<SpecialityModel.Response>(
            admin, "/api/specialties",
            new { name = $"Especialidad C04 {suffix}", description = "Catálogo sintético de aceptación" });

        var doctor = await PostAsync<DoctorModel.Response>(
            admin, "/api/doctors",
            new
            {
                name = "Médico para prueba concurrente",
                licenseNumber = $"C04-{suffix}",
                specialtyId = specialty.Id
            });

        await PostAsync<AvailabilityModel.PlanningResponse>(
            admin, "/api/availabilities",
            new
            {
                doctorId = doctor.Id,
                days = new[] { new { day = "LUNES", startTime = "10:00", endTime = "10:30" } }
            });

        var slot = Assert.Single(await GetAsync<AvailabilityModel.AvailableSlotResponse[]>(
            patient, $"/api/availabilities?doctorId={doctor.Id}"));

        var request = new
        {
            doctorId = doctor.Id,
            availabilitySlotId = slot.AvailabilitySlotId,
            patient = new { dni },
            reason = "Reserva concurrente de aceptación C04"
        };

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deleteTask = DeleteAfterStartAsync();
        var bookTask = BookAfterStartAsync();

        start.SetResult();
        await Task.WhenAll(deleteTask, bookTask);

        using var deletion = await deleteTask;
        using var booking = await bookTask;

        var bookingWon = booking.StatusCode == HttpStatusCode.Created &&
                         deletion.StatusCode == HttpStatusCode.Conflict;
        var deletionWon = deletion.StatusCode == HttpStatusCode.OK &&
                          booking.StatusCode == HttpStatusCode.NotFound;

        Assert.True(bookingWon || deletionWon,
            $"Resultado inesperado: baja {(int)deletion.StatusCode}, " +
            $"reserva {(int)booking.StatusCode}.");

        await using var verify = fixture.CreateApplicationContext();

        var storedDoctor = await verify.Set<Doctor>()
            .IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(item => item.Id == doctor.Id);
        var storedSlot = await verify.Set<Availability>()
            .IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(item => item.Id == slot.AvailabilitySlotId);
        var storedAppointments = await verify.Set<Appointment>()
            .IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.AvailabilityId == slot.AvailabilitySlotId)
            .ToListAsync();

        Assert.False(storedSlot.Deleted);

        if (bookingWon)
        {
            var error = await ReadAsync<JsonElement>(deletion, HttpStatusCode.Conflict);
            Assert.Equal("DOCTOR_HAS_FUTURE_APPOINTMENTS",
                error.GetProperty("errorCode").GetString());

            var response = await ReadAsync<AppointmentModel.Response>(booking, HttpStatusCode.Created);
            var storedAppointment = Assert.Single(storedAppointments);
            Assert.Equal(response.AppointmentsId, storedAppointment.Id);
            Assert.False(storedAppointment.Deleted);
            Assert.Equal(AppointmentStatus.BOOKED, storedAppointment.Status);
            Assert.Equal(AvailabilityStatus.Booked, storedSlot.Status);
            Assert.True(storedDoctor.IsActive);
            Assert.False(storedDoctor.Deleted);
        }
        else
        {
            var error = await ReadAsync<JsonElement>(booking, HttpStatusCode.NotFound);
            Assert.Equal("ENTITY_NOTFOUND", error.GetProperty("errorCode").GetString());
            Assert.Equal("ok", await deletion.Content.ReadAsStringAsync());

            Assert.Empty(storedAppointments);
            Assert.Equal(AvailabilityStatus.Available, storedSlot.Status);
            Assert.False(storedDoctor.IsActive);
            Assert.True(storedDoctor.Deleted);
        }

        async Task<HttpResponseMessage> DeleteAfterStartAsync()
        {
            await start.Task;
            return await admin.DeleteAsync($"/api/doctors/{doctor.Id}");
        }

        async Task<HttpResponseMessage> BookAfterStartAsync()
        {
            await start.Task;
            return await patient.PostAsJsonAsync("/api/appointments", request);
        }
    }

    private static async Task LoginAsync(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body);
        var session = await ReadAsync<JsonElement>(response, HttpStatusCode.OK);
        var token = session.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body);
        return await ReadAsync<T>(response, HttpStatusCode.Created);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        return await ReadAsync<T>(response, HttpStatusCode.OK);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.True(response.StatusCode == expected,
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: " +
            $"esperado {(int)expected}, recibido {(int)response.StatusCode}.");

        var result = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(result);
        return result!;
    }

    private sealed class FixedClinicClock : IClinicClock
    {
        public DateTimeOffset GetCurrentLocalDateTime() =>
            new(2030, 6, 24, 8, 0, 0, TimeSpan.FromHours(-3));
    }
}