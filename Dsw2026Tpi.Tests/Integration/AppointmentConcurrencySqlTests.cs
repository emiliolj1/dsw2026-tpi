using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class AppointmentConcurrencySqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public AppointmentConcurrencySqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TwoPatientsBookingSameSlot_OnlyOneAppointmentIsBooked()
    {
        var seed = await SeedAsync();

        var results = await Task.WhenAll(
            TryBookAsync(seed, seed.PatientA),
            TryBookAsync(seed, seed.PatientB));

        Assert.Equal(1, results.Count(result => result is null));

        var failure = Assert.Single(results.Where(result => result is not null));
        Assert.True(
            failure is ConflictException or ClinicalWriteConflictException,
            $"Se esperaba un conflicto controlado y fue {failure!.GetType().Name}.");

        var appointments = await GetAppointmentsOfSlotAsync(seed.Slot.Id);
        Assert.Single(appointments, a => a.Status == AppointmentStatus.BOOKED);
        Assert.Equal(AvailabilityStatus.Booked, await GetSlotStatusAsync(seed.Slot.Id));
    }

    [Fact]
    public async Task CancelAndBookAgain_KeepsCancelledAndAddsNewBooked()
    {
        var seed = await SeedAsync();

        var first = await BookAsync(seed, seed.PatientA);
        await CancelAsync(first.AppointmentsId, seed.PatientA);
        await BookAsync(seed, seed.PatientB);

        var appointments = await GetAppointmentsOfSlotAsync(seed.Slot.Id);
        Assert.Equal(2, appointments.Count);
        Assert.Single(appointments, a => a.Status == AppointmentStatus.CANCELLED);
        Assert.Single(appointments, a => a.Status == AppointmentStatus.BOOKED);
        Assert.Equal(AvailabilityStatus.Booked, await GetSlotStatusAsync(seed.Slot.Id));
    }

    [Fact]
    public async Task DoubleCancel_SecondIsControlledConflict_AndForeignPatientCannotCancel()
    {
        var seed = await SeedAsync();

        var booked = await BookAsync(seed, seed.PatientA);

        // Un paciente ajeno no puede tocar la cita.
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => CancelAsync(booked.AppointmentsId, seed.PatientB));

        await CancelAsync(booked.AppointmentsId, seed.PatientA);

        await Assert.ThrowsAsync<ConflictException>(
            () => CancelAsync(booked.AppointmentsId, seed.PatientA));

        Assert.Equal(AvailabilityStatus.Available, await GetSlotStatusAsync(seed.Slot.Id));
    }
    [Fact]
    public async Task FailureBeforeComplete_RollsBackSlotAndAppointment()
    {
        var seed = await SeedAsync();

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var factory = scope.ServiceProvider
                .GetRequiredService<IClinicalWriteScopeFactory>();
            var persistence = scope.ServiceProvider
                .GetRequiredService<IAppointmentPersistence>();

            await using var writeScope = await factory.BeginAsync();

            var appointment = new Appointment(
                seed.Doctor.Id, seed.Slot.Id, seed.PatientA.Id, "Rollback SQL");

            // Modifica el slot y crea la cita...
            Assert.True(await persistence.TryCreate(appointment));

            // ...pero NO se llama a CompleteAsync: al salir se revierte todo.
        }

        Assert.Empty(await GetAppointmentsOfSlotAsync(seed.Slot.Id));
        Assert.Equal(
            AvailabilityStatus.Available,
            await GetSlotStatusAsync(seed.Slot.Id));
    }
    // ---------- Ayudas ----------

    private sealed record Seed(
        Doctor Doctor,
        Patient PatientA,
        Patient PatientB,
        Availability Slot);

    private async Task<Seed> SeedAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);

        var speciality = new Speciality(
            $"Esp {suffix}", "Especialidad para pruebas SQL de reservas");
        var doctor = new Doctor(
            "Ana Reservas SQL", $"SQL-RES-{suffix}", speciality);
        var patientA = new Patient(
            Guid.NewGuid(), Random.Shared.NextInt64(10_000_000, 49_999_999), "Paciente A");
        var patientB = new Patient(
            Guid.NewGuid(), Random.Shared.NextInt64(50_000_000, 99_999_999), "Paciente B");
        var slot = new Availability(doctor.Id, day, new TimeOnly(10, 0));

        using var scope = _fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2026TpiDbContext>();
        context.AddRange(speciality, doctor, patientA, patientB, slot);
        await context.SaveChangesAsync();

        return new Seed(doctor, patientA, patientB, slot);
    }

    private async Task<AppointmentModel.Response> BookAsync(Seed seed, Patient patient)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAppointmentService>();

        return await service.Create(
            new AppointmentModel.Request(
                seed.Doctor.Id,
                seed.Slot.Id,
                new AppointmentModel.PatientRequest(patient.Dni),
                "Reserva simultanea SQL"),
            patient.UserId);
    }

    private async Task<Exception?> TryBookAsync(Seed seed, Patient patient)
    {
        try
        {
            await BookAsync(seed, patient);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private async Task CancelAsync(Guid appointmentId, Patient patient)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAppointmentService>();

        await service.Cancel(appointmentId, patient.UserId);
    }

    private async Task<List<Appointment>> GetAppointmentsOfSlotAsync(Guid slotId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2026TpiDbContext>();

        return await context.Set<Appointment>()
            .AsNoTracking()
            .Where(a => a.AvailabilityId == slotId)
            .ToListAsync();
    }

    private async Task<AvailabilityStatus> GetSlotStatusAsync(Guid slotId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2026TpiDbContext>();

        return await context.Set<Availability>()
            .AsNoTracking()
            .Where(a => a.Id == slotId)
            .Select(a => a.Status)
            .SingleAsync();
    }
}