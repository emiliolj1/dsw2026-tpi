using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class AvailabilitySqlTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    public AvailabilitySqlTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Post_PersisteReglaEnSqlYDevuelveSuId()
    {
        var doctorId = await SeedDoctorAsync();
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAvailabilityService>();
        var response = await service.Create(new AvailabilityModel.Request(doctorId,
            [new AvailabilityModel.DayRequest("LUNES", "09:15", "10:15")]));

        Assert.Equal("10:15", response.Days.Single().EndTime);
        await using var verify = _fixture.CreateApplicationContext();
        var rule = await verify.Set<AvailabilityRule>()
            .SingleAsync(r => r.Id == response.Days.Single().Id);
        Assert.Equal(doctorId, rule.DoctorId);
        Assert.False(rule.Deleted);
        Assert.DoesNotContain(await verify.Set<Availability>()
            .Where(s => s.AvailabilityRuleId == rule.Id).ToListAsync(),
            s => s.AvailabilityRuleId is null);
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
