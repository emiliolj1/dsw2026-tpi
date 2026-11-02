using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class AppointmentStatusSqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public AppointmentStatusSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("ATTENDED", AppointmentStatus.ATTENDED)]
    [InlineData("NO_SHOW", AppointmentStatus.NO_SHOW)]
    public async Task FinishedBookedAppointment_CanBeClosed_AndSlotStaysBooked(
        string status, AppointmentStatus expected)
    {
        var appointmentId = await SeedBookedAsync(daysFromToday: -2);

        var response = await UpdateAsync(appointmentId, status);

        Assert.Equal(expected.ToString(), response.AppointmentsStatus);
        Assert.Equal(expected, await GetStatusAsync(appointmentId));
        Assert.Equal(
            AvailabilityStatus.Booked,
            await GetSlotStatusOfAppointmentAsync(appointmentId));
    }

    [Fact]
    public async Task FutureAppointment_IsConflict_AndStaysBooked()
    {
        var appointmentId = await SeedBookedAsync(daysFromToday: 3);

        await Assert.ThrowsAsync<ConflictException>(
            () => UpdateAsync(appointmentId, "ATTENDED"));

        Assert.Equal(
            AppointmentStatus.BOOKED, await GetStatusAsync(appointmentId));
    }

    [Theory]
    [InlineData("INVENTADO")]
    [InlineData("CANCELLED")]
    [InlineData("BOOKED")]
    [InlineData(null)]
    public async Task InvalidStatus_IsValidationError(string? status)
    {
        var appointmentId = await SeedBookedAsync(daysFromToday: -2);

        await Assert.ThrowsAsync<ValidationException>(
            () => UpdateAsync(appointmentId, status));

        Assert.Equal(
            AppointmentStatus.BOOKED, await GetStatusAsync(appointmentId));
    }

    [Fact]
    public async Task UnknownAppointment_IsNotFound()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => UpdateAsync(Guid.NewGuid(), "ATTENDED"));
    }

    [Fact]
    public async Task AlreadyClosedAppointment_IsNotReopenedNorChanged()
    {
        var appointmentId = await SeedBookedAsync(daysFromToday: -2);

        await UpdateAsync(appointmentId, "ATTENDED");

        await Assert.ThrowsAsync<ConflictException>(
            () => UpdateAsync(appointmentId, "NO_SHOW"));

        Assert.Equal(
            AppointmentStatus.ATTENDED, await GetStatusAsync(appointmentId));
    }

    [Fact]
    public async Task CancelledAppointment_IsNotReopened()
    {
        var appointmentId = await SeedBookedAsync(daysFromToday: 3);
        var patientUserId = await GetPatientUserIdAsync(appointmentId);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<IAppointmentService>();
            await service.Cancel(appointmentId, patientUserId);
        }

        await Assert.ThrowsAsync<ConflictException>(
            () => UpdateAsync(appointmentId, "ATTENDED"));

        Assert.Equal(
            AppointmentStatus.CANCELLED, await GetStatusAsync(appointmentId));
    }

    [Theory]
    [InlineData("ATTENDED", AppointmentStatus.ATTENDED)]
    [InlineData("NO_SHOW", AppointmentStatus.NO_SHOW)]
    public async Task ClosedAppointment_CannotBeCancelledByPatient(
    string status, AppointmentStatus expected)
    {
        var appointmentId = await SeedBookedAsync(daysFromToday: -2);
        var patientUserId = await GetPatientUserIdAsync(appointmentId);

        await UpdateAsync(appointmentId, status);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<IAppointmentService>();

            await Assert.ThrowsAsync<ConflictException>(
                () => service.Cancel(appointmentId, patientUserId));
        }

        Assert.Equal(expected, await GetStatusAsync(appointmentId));
        Assert.Equal(
            AvailabilityStatus.Booked,
            await GetSlotStatusOfAppointmentAsync(appointmentId));
    }
    // ---------- Ayudas ----------

    private async Task<Guid> SeedBookedAsync(int daysFromToday)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysFromToday);

        var speciality = new Speciality(
            $"Esp {suffix}", "Especialidad para pruebas SQL de estados");
        var doctor = new Doctor(
            "Ana Estados SQL", $"SQL-EST-{suffix}", speciality);
        var patient = new Patient(
            Guid.NewGuid(),
            Random.Shared.NextInt64(10_000_000, 99_999_999),
            "Paciente Estados");
        var slot = new Availability(doctor.Id, day, new TimeOnly(10, 0));
        slot.Book();
        var appointment = new Appointment(
            doctor.Id, slot.Id, patient.Id, "Estado SQL");

        using var scope = _fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2026TpiDbContext>();
        context.AddRange(speciality, doctor, patient, slot, appointment);
        await context.SaveChangesAsync();

        return appointment.Id;
    }

    private async Task<AppointmentModel.Response> UpdateAsync(
        Guid appointmentId, string? status)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<IAppointmentService>();

        return await service.UpdateStatus(
            appointmentId, new AppointmentModel.UpdateStatusRequest(status));
    }

    private async Task<AppointmentStatus> GetStatusAsync(Guid appointmentId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2026TpiDbContext>();

        return await context.Set<Appointment>().AsNoTracking()
            .Where(a => a.Id == appointmentId)
            .Select(a => a.Status)
            .SingleAsync();
    }

    private async Task<AvailabilityStatus> GetSlotStatusOfAppointmentAsync(
        Guid appointmentId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2026TpiDbContext>();

        return await context.Set<Appointment>().AsNoTracking()
            .Where(a => a.Id == appointmentId)
            .Select(a => a.Availability!.Status)
            .SingleAsync();
    }

    private async Task<Guid> GetPatientUserIdAsync(Guid appointmentId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2026TpiDbContext>();

        return await context.Set<Appointment>().AsNoTracking()
            .Where(a => a.Id == appointmentId)
            .Select(a => a.Patient!.UserId)
            .SingleAsync();
    }
}