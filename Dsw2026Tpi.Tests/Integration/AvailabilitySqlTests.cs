using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class AvailabilitySqlTests : IClassFixture<SqlServerFixture>
{
    private static readonly IClinicClock Clock = new FixedClinicClock();
    private static readonly DateOnly SlotDate = new(2030, 6, 24);
    private readonly SqlServerFixture _fixture;
    public AvailabilitySqlTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Post_PersisteReglaYGeneraLosDosTurnosEsperados()
    {
        var doctorId = await SeedDoctorAsync();
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = CreateServiceWithFixedClock(scope.ServiceProvider);

        // El 24 de junio es el último lunes del mes.
        var response = await service.Create(new AvailabilityModel.Request(doctorId,
            [new AvailabilityModel.DayRequest("LUNES", "09:15", "10:15")]));

        var day = Assert.Single(response.Days);
        Assert.Equal("10:15", day.EndTime);
        await using var verify = _fixture.CreateApplicationContext();
        var rule = await verify.Set<AvailabilityRule>()
            .SingleAsync(r => r.Id == day.Id);
        Assert.Equal(doctorId, rule.DoctorId);
        Assert.False(rule.Deleted);

        // Buscar por médico para detectar también turnos sin vínculo o de más.
        var slots = await verify.Set<Availability>()
            .Where(s => s.DoctorId == doctorId)
            .OrderBy(s => s.Date).ThenBy(s => s.StartTime)
            .ToListAsync();

        Assert.Collection(slots,
            slot => AssertGeneratedSlot(slot, rule.Id, new TimeOnly(9, 15), new TimeOnly(9, 45)),
            slot => AssertGeneratedSlot(slot, rule.Id, new TimeOnly(9, 45), new TimeOnly(10, 15)));
    }

    [Fact]
    public async Task GetAvailableSlots_MedicoDesactivado_NoOfreceTurnosExistentes()
    {
        var doctorId = await SeedDoctorAsync();
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var service = CreateServiceWithFixedClock(scope.ServiceProvider);
            await service.Create(new AvailabilityModel.Request(doctorId,
                [new AvailabilityModel.DayRequest("LUNES", "09:15", "10:15")]));
            Assert.NotEmpty(await service.GetAvailableSlots(doctorId));
        }

        await using (var context = _fixture.CreateApplicationContext())
        {
            var doctor = await context.Set<Doctor>().SingleAsync(d => d.Id == doctorId);
            doctor.Deactivate();
            await context.SaveChangesAsync();
        }

        using var readScope = _fixture.Factory.Services.CreateScope();
        var readService = CreateServiceWithFixedClock(readScope.ServiceProvider);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => readService.GetAvailableSlots(doctorId));
    }

    [Fact]
    public async Task PutVacio_ConservaCitaYSlotReservado()
    {
        var doctorId = await SeedDoctorAsync();
        using var serviceScope = _fixture.Factory.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var now = serviceScope.ServiceProvider.GetRequiredService<IClinicClock>()
            .GetCurrentLocalDateTime();
        var date = new DateOnly(now.Year, now.Month, 1);
        var rule = new AvailabilityRule(doctorId, now.Year, now.Month,
            date.DayOfWeek, new TimeOnly(9, 0), new TimeOnly(10, 0));
        var slot = new Availability(doctorId, date, new TimeOnly(9, 0),
            availabilityRuleId: rule.Id);
        slot.Book();
        var patient = new Patient(Guid.NewGuid(), Random.Shared.NextInt64(10_000_000, 99_999_999), "Paciente SQL");
        var appointment = new Appointment(doctorId, slot.Id, patient.Id, "Control SQL de agenda");
        await using (var context = _fixture.CreateApplicationContext())
        {
            context.AddRange(rule, patient, slot, appointment);
            await context.SaveChangesAsync();
        }

        var result = await service.Update(new AvailabilityModel.Request(doctorId, []));
        Assert.Empty(result.Days);
        await using var verify = _fixture.CreateApplicationContext();
        var storedSlot = await verify.Set<Availability>()
            .SingleAsync(s => s.Id == slot.Id);
        var storedAppointment = await verify.Set<Appointment>()
            .SingleAsync(a => a.Id == appointment.Id);
        Assert.False(storedSlot.Deleted);
        Assert.Equal(AvailabilityStatus.Booked, storedSlot.Status);
        Assert.Equal(AppointmentStatus.BOOKED, storedAppointment.Status);
        Assert.Equal(slot.Id, storedAppointment.AvailabilityId);
    }

    private static void AssertGeneratedSlot(
        Availability slot, Guid ruleId, TimeOnly startTime, TimeOnly endTime)
    {
        Assert.Equal(SlotDate, slot.Date);
        Assert.Equal(startTime, slot.StartTime);
        Assert.Equal(endTime, slot.EndTime);
        Assert.Equal<Guid?>(ruleId, slot.AvailabilityRuleId);
        Assert.Equal(AvailabilityStatus.Available, slot.Status);
        Assert.False(slot.Deleted);
    }

    private static AvailabilityService CreateServiceWithFixedClock(IServiceProvider services) =>
        new(
            services.GetRequiredService<IPersistence>(),
            services.GetRequiredService<IAvailabilityPersistence>(),
            services.GetRequiredService<IClinicalWriteScopeFactory>(),
            Clock,
            Options.Create(new NonWorkingDaysOptions()),
            NullLogger<AvailabilityService>.Instance);

    private sealed class FixedClinicClock : IClinicClock
    {
        public DateTimeOffset GetCurrentLocalDateTime() =>
            new(2030, 6, 24, 8, 0, 0, TimeSpan.FromHours(-3));
    }

    private async Task<Guid> SeedDoctorAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var speciality = new Speciality($"Esp {suffix}", "Especialidad de pruebas SQL");
        var doctor = new Doctor("Médico agenda", $"N-{suffix}", speciality);
        await using var context = _fixture.CreateApplicationContext();
        context.AddRange(speciality, doctor);
        await context.SaveChangesAsync();
        return doctor.Id;
    }
}
