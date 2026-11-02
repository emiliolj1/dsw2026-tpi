using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Tests.Integration;

/// <summary>
/// N03: integración real de agenda y reservas sobre SQL Server.
/// Se usa un reloj fijo para que la prueba no dependa del día ni de la hora de ejecución.
/// La fábrica SQL crea un catálogo aislado con prefijo Dsw2026Tpi_Test_.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AgendaBookingConcurrencySqlTests : IClassFixture<SqlServerFixture>
{
    private static readonly IClinicClock Clock = new FixedClinicClock();
    private static readonly DateOnly SlotDate = new(2030, 6, 4);
    private static readonly TimeOnly SlotTime = new(10, 0);
    private readonly SqlServerFixture _fixture;

    public AgendaBookingConcurrencySqlTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ReservaConfirmada_YLuegoPutVacio_ConservaCitaYSuSlot()
    {
        var seed = await SeedAsync();

        using (var bookingScope = _fixture.Factory.Services.CreateScope())
        {
            await NewAppointmentService(bookingScope.ServiceProvider).Create(
                BookingRequest(seed), seed.PatientUserId);
        }

        using (var planningScope = _fixture.Factory.Services.CreateScope())
        {
            await NewAvailabilityService(planningScope.ServiceProvider).Update(
                new AvailabilityModel.Request(seed.DoctorId, []));
        }

        await using var verify = _fixture.CreateApplicationContext();
        var slot = await verify.Set<Availability>().AsNoTracking()
            .SingleAsync(s => s.Id == seed.SlotId);
        var rule = await verify.Set<AvailabilityRule>().AsNoTracking()
            .SingleAsync(r => r.Id == seed.RuleId);
        var appointments = await verify.Set<Appointment>().AsNoTracking()
            .Where(a => a.AvailabilityId == seed.SlotId).ToListAsync();

        Assert.True(rule.Deleted); // La regla deja de estar activa.
        Assert.False(slot.Deleted); // El slot reservado NO desaparece.
        Assert.Equal(AvailabilityStatus.Booked, slot.Status);
        Assert.Single(appointments, a => a.Status == AppointmentStatus.BOOKED);
    }

    [Fact]
    public async Task PutVacio_YReservaEnParalelo_NuncaPierdenUnaReservaConfirmada()
    {
        var seed = await SeedAsync();

        // Dos scopes diferentes -> dos DbContext/conexiones SQL independientes.
        using var bookingScope = _fixture.Factory.Services.CreateScope();
        using var planningScope = _fixture.Factory.Services.CreateScope();
        var bookingContext = bookingScope.ServiceProvider
            .GetRequiredService<Dsw2026TpiDbContext>();
        var planningContext = planningScope.ServiceProvider
            .GetRequiredService<Dsw2026TpiDbContext>();
        Assert.NotSame(bookingContext, planningContext);
        Assert.NotSame(
            bookingContext.Database.GetDbConnection(),
            planningContext.Database.GetDbConnection());

        var appointmentService = NewAppointmentService(bookingScope.ServiceProvider);
        var availabilityService = NewAvailabilityService(planningScope.ServiceProvider);
        var start = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var bookingTask = Task.Run(async () =>
        {
            await start.Task;
            try
            {
                await appointmentService.Create(BookingRequest(seed), seed.PatientUserId);
                return (Exception?)null;
            }
            catch (Exception ex) when (ex is ConflictException or EntityNotFoundException)
            {
                return ex;
            }
        });

        var planningTask = Task.Run(async () =>
        {
            await start.Task;
            try
            {
                await availabilityService.Update(
                    new AvailabilityModel.Request(seed.DoctorId, []));
                return (ConflictException?)null;
            }
            catch (ConflictException ex)
            {
                return ex;
            }
        });

        start.SetResult(true);
        var bookingFailure = await bookingTask;
        var planningFailure = await planningTask;

        await using var verify = _fixture.CreateApplicationContext();
        var slot = await verify.Set<Availability>().AsNoTracking()
            .SingleAsync(s => s.Id == seed.SlotId);
        var appointments = await verify.Set<Appointment>().AsNoTracking()
            .Where(a => a.AvailabilityId == seed.SlotId).ToListAsync();
        var booked = appointments.Where(a => a.Status == AppointmentStatus.BOOKED)
            .ToList();

        Assert.True(booked.Count <= 1);
        if (bookingFailure is null)
        {
            // Si la reserva ganó la carrera, PUT la tiene que preservar,
            // incluso cuando PUT alcanzó a confirmar.
            Assert.Single(booked);
            Assert.False(slot.Deleted);
            Assert.Equal(AvailabilityStatus.Booked, slot.Status);
        }
        else
        {
            // Si PUT ganó, el slot se retiró y la reserva no se confirmó.
            // Un 404 por slot ya retirado o un 409 son resultados controlados.
            Assert.Null(planningFailure);
            Assert.Empty(booked);
            Assert.True(slot.Deleted);
        }

        // Un bloqueo ocupado puede causar 409 en PUT. Nunca se permite
        // confirmar una cita cuyo slot quedó eliminado lógicamente.
        Assert.False(slot.Deleted && booked.Count > 0);
    }

    [Fact]
    public async Task PutHttp_ConBloqueoOcupado_Responde409ConCodigoDeConflicto()
    {
        var seed = await SeedAsync();
        using var login = await _fixture.Client.PostAsJsonAsync(
            "/api/auth/admin/login",
            new
            {
                email = SqlServerWebApplicationFactory.AdminEmail,
                password = SqlServerWebApplicationFactory.AdminPassword
            });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginBody.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        using var holderScope = _fixture.Factory.Services.CreateScope();
        var writeFactory = holderScope.ServiceProvider
            .GetRequiredService<IClinicalWriteScopeFactory>();
        await using var heldLock = await writeFactory.BeginAsync();

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/availabilities")
        {
            Content = JsonContent.Create(new AvailabilityModel.Request(seed.DoctorId, []))
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await _fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CLINICAL_WRITE_CONFLICT",
            body.GetProperty("errorCode").GetString());
        // Dispose de heldLock revierte la transacción y libera el applock.
    }

    private async Task<TestSeed> SeedAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var speciality = new Speciality($"Esp N03 {suffix}", "Pruebas de concurrencia N03");
        var doctor = new Doctor("Médico prueba N03", $"N03-{suffix}", speciality);
        var patient = new Patient(
            Guid.NewGuid(), Random.Shared.NextInt64(10_000_000, 99_999_999),
            "Paciente prueba N03");
        var rule = new AvailabilityRule(
            doctor.Id, 2030, 6, DayOfWeek.Tuesday,
            SlotTime, new TimeOnly(11, 0));
        var slot = new Availability(
            doctor.Id, SlotDate, SlotTime, availabilityRuleId: rule.Id);

        await using var context = _fixture.CreateApplicationContext();
        context.AddRange(speciality, doctor, patient, rule, slot);
        await context.SaveChangesAsync();
        return new TestSeed(doctor.Id, patient.UserId, patient.Dni, rule.Id, slot.Id);
    }

    private static AppointmentModel.Request BookingRequest(TestSeed seed) =>
        new(seed.DoctorId, seed.SlotId,
            new AppointmentModel.PatientRequest(seed.PatientDni),
            "Reserva de prueba N03");

    private static AppointmentService NewAppointmentService(IServiceProvider services) =>
        new(
            services.GetRequiredService<IPersistence>(),
            services.GetRequiredService<IAppointmentPersistence>(),
            Clock,
            services.GetRequiredService<IClinicalWriteScopeFactory>(),
            NullLogger<AppointmentService>.Instance);

    private static AvailabilityService NewAvailabilityService(IServiceProvider services) =>
        new(
            services.GetRequiredService<IPersistence>(),
            services.GetRequiredService<IAvailabilityPersistence>(),
            services.GetRequiredService<IClinicalWriteScopeFactory>(),
            Clock,
            Options.Create(new NonWorkingDaysOptions()),
            NullLogger<AvailabilityService>.Instance);

    private sealed record TestSeed(
        Guid DoctorId, Guid PatientUserId, long PatientDni, Guid RuleId, Guid SlotId);

    private sealed class FixedClinicClock : IClinicClock
    {
        public DateTimeOffset GetCurrentLocalDateTime() =>
            new(2030, 6, 3, 8, 0, 0, TimeSpan.FromHours(-3));
    }
}
