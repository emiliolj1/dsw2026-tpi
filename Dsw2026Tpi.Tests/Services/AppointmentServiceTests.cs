using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;

namespace Dsw2026Tpi.Tests.Services;

public class AppointmentServiceTests
{
    private readonly Mock<IPersistence> _persistence;
    private readonly Mock<IAppointmentPersistence> _appointmentPersistence;
    private readonly Mock<IClinicClock> _clock;
    private readonly Mock<IClinicalWriteScopeFactory> _writeScopeFactory;
    private readonly Mock<IClinicalWriteScope> _writeScope;
    private readonly AppointmentService _service;

    public AppointmentServiceTests()
    {
        _persistence = new Mock<IPersistence>();
        _appointmentPersistence = new Mock<IAppointmentPersistence>();
        _clock = new Mock<IClinicClock>();
        _clock
            .Setup(clock => clock.GetCurrentLocalDateTime())
            .Returns(new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.FromHours(-3)));

        _writeScope = new Mock<IClinicalWriteScope>();
        _writeScopeFactory = new Mock<IClinicalWriteScopeFactory>();
        _writeScopeFactory
            .Setup(factory => factory.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_writeScope.Object);

        _service = new AppointmentService(
            _persistence.Object,
            _appointmentPersistence.Object,
            _clock.Object,
            _writeScopeFactory.Object,
            NullLogger<AppointmentService>.Instance);
    }

    [Fact]
    public async Task Create_WithAnotherPatientsDni_ThrowsAuthorizationException()
    {
        var patient = new Patient(Guid.NewGuid(), 30123456);
        var request = new AppointmentModel.Request(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AppointmentModel.PatientRequest(40987654),
            "Consulta médica general");

        SetupAuthenticatedPatient(patient);

        await Assert.ThrowsAsync<AuthorizationException>(
            () => _service.Create(request, patient.UserId));

        _persistence.Verify(
            p => p.GetById<Doctor>(It.IsAny<Guid>(), It.IsAny<string[]>()),
            Times.Never);
        _appointmentPersistence.Verify(
            p => p.TryCreate(It.IsAny<Appointment>()),
            Times.Never);
    }

    [Fact]
    public async Task GetActiveByPatient_WithAnotherPatientsDni_ThrowsAuthorizationException()
    {
        var patient = new Patient(Guid.NewGuid(), 30123456);

        SetupAuthenticatedPatient(patient);

        await Assert.ThrowsAsync<AuthorizationException>(
            () => _service.GetActiveByPatient(40987654, patient.UserId));

        _persistence.Verify(
            p => p.GetFiltered<Appointment>(
                It.IsAny<Expression<Func<Appointment, bool>>>(),
                It.IsAny<string[]>()),
            Times.Never);
    }

    [Fact]
    public async Task Cancel_WhenAppointmentBelongsToAnotherPatient_ThrowsEntityNotFoundException()
    {
        var patient = new Patient(Guid.NewGuid(), 30123456);
        var appointmentId = Guid.NewGuid();
        var otherPatientsAppointment = new Appointment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Consulta médica general",
            appointmentId);

        SetupAuthenticatedPatient(patient);

        _persistence
            .Setup(p => p.First<Appointment>(
                It.IsAny<Expression<Func<Appointment, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                Expression<Func<Appointment, bool>> predicate,
                string[] includes) =>
                predicate.Compile().Invoke(otherPatientsAppointment)
                    ? otherPatientsAppointment
                    : null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _service.Cancel(appointmentId, patient.UserId));

        _appointmentPersistence.Verify(
            p => p.TryCancel(It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_WhenSlotAlreadyBooked_ThrowsConflict_AndNeverCompletesScope()
    {
        var speciality = new Speciality("Clinica", "Especialidad de prueba");
        var doctor = new Doctor("Ana Test", "MP-001", speciality);
        var patient = new Patient(Guid.NewGuid(), 30123456);
        var slot = new Availability(
            doctor.Id, new DateOnly(2026, 10, 22), new TimeOnly(9, 0));
        slot.Book();

        SetupAuthenticatedPatient(patient);
        _persistence
            .Setup(p => p.GetById<Doctor>(doctor.Id, It.IsAny<string[]>()))
            .ReturnsAsync(doctor);
        _persistence
            .Setup(p => p.GetById<Availability>(slot.Id, It.IsAny<string[]>()))
            .ReturnsAsync(slot);

        var request = new AppointmentModel.Request(
            doctor.Id,
            slot.Id,
            new AppointmentModel.PatientRequest(patient.Dni),
            "Consulta médica general");

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.Create(request, patient.UserId));

        _appointmentPersistence.Verify(
            p => p.TryCreate(It.IsAny<Appointment>()),
            Times.Never);
        _writeScope.Verify(
            s => s.CompleteAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void SetupAuthenticatedPatient(Patient patient)
    {
        _persistence
            .Setup(p => p.First<Patient>(
                It.IsAny<Expression<Func<Patient, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                Expression<Func<Patient, bool>> predicate,
                string[] includes) =>
                predicate.Compile().Invoke(patient)
                    ? patient
                    : null);
    }
    [Fact]
    public async Task Create_WhenPersistenceFailsUnexpectedly_PropagatesErrorAndDoesNotComplete()
    {
        var patient = new Patient(Guid.NewGuid(), 30123456);
        var speciality = new Speciality("Clinica", "Especialidad de prueba 500");
        var doctor = new Doctor("Ana Perez", "LIC-500", speciality);
        var slot = new Availability(
            doctor.Id, new DateOnly(2026, 10, 22), new TimeOnly(10, 0));

        var request = new AppointmentModel.Request(
            doctor.Id,
            slot.Id,
            new AppointmentModel.PatientRequest(patient.Dni),
            "Consulta de control");

        SetupAuthenticatedPatient(patient);

        _persistence
            .Setup(p => p.GetById<Doctor>(doctor.Id, It.IsAny<string[]>()))
            .ReturnsAsync(doctor);
        _persistence
            .Setup(p => p.GetById<Availability>(slot.Id, It.IsAny<string[]>()))
            .ReturnsAsync(slot);

        _appointmentPersistence
            .Setup(p => p.TryCreate(It.IsAny<Appointment>()))
            .ThrowsAsync(new InvalidOperationException("Fallo SQL simulado"));

        var error = await Record.ExceptionAsync(
            () => _service.Create(request, patient.UserId));

        // Un fallo inesperado NO se disfraza de 409: se propaga (HTTP 500).
        Assert.IsType<InvalidOperationException>(error);
        Assert.IsNotType<ConflictException>(error);

        _writeScope.Verify(
            s => s.CompleteAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }
}