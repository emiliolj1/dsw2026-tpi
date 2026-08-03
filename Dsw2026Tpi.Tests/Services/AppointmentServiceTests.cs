using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Moq;
using System.Linq.Expressions;

namespace Dsw2026Tpi.Tests.Services;

public class AppointmentServiceTests
{
    private readonly Mock<IPersistence> _persistence;
    private readonly Mock<IAppointmentPersistence>
        _appointmentPersistence;
    private readonly AppointmentService _service;

    public AppointmentServiceTests()
    {
        _persistence = new Mock<IPersistence>();
        _appointmentPersistence =
            new Mock<IAppointmentPersistence>();

        _service = new AppointmentService(
            _persistence.Object,
            _appointmentPersistence.Object);
    }

    [Fact]
    public async Task Create_WithAnotherPatientsDni_ThrowsAuthorizationException()
    {
        var patient = new Patient(
                Guid.NewGuid(),
                 30123456);

        var request = new AppointmentModel.Request(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new AppointmentModel.PatientRequest(40987654),
            "Consulta médica general");

        SetupAuthenticatedPatient(patient);

        await Assert.ThrowsAsync<AuthorizationException>(
            () => _service.Create(
                request,
                patient.UserId));

        _persistence.Verify(
            p => p.GetById<Doctor>(
                It.IsAny<Guid>(),
                It.IsAny<string[]>()),
            Times.Never);

        _appointmentPersistence.Verify(
            p => p.TryCreate(
                It.IsAny<Appointment>()),
            Times.Never);
    }

    [Fact]
    public async Task GetActiveByPatient_WithAnotherPatientsDni_ThrowsAuthorizationException()
    {
        var patient = new Patient(
                Guid.NewGuid(),
                   30123456);

        SetupAuthenticatedPatient(patient);

        await Assert.ThrowsAsync<AuthorizationException>(
            () => _service.GetActiveByPatient(
                40987654,
                patient.UserId));

        _persistence.Verify(
            p => p.GetFiltered<Appointment>(
                It.IsAny<
                    Expression<Func<Appointment, bool>>>(),
                It.IsAny<string[]>()),
            Times.Never);
    }

    [Fact]
    public async Task Cancel_WhenAppointmentBelongsToAnotherPatient_ThrowsEntityNotFoundException()
    {
        var patient = new Patient(
                 Guid.NewGuid(),
                    30123456);

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
                It.IsAny<
                    Expression<Func<Appointment, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                Expression<Func<Appointment, bool>> predicate,
                string[] includes) =>
                    predicate
                        .Compile()
                        .Invoke(otherPatientsAppointment)
                            ? otherPatientsAppointment
                            : null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _service.Cancel(
                appointmentId,
                patient.UserId));

        _appointmentPersistence.Verify(
            p => p.TryCancel(
                It.IsAny<Guid>()),
            Times.Never);
    }

    private void SetupAuthenticatedPatient(
    Patient patient)
    {
        _persistence
            .Setup(p => p.First<Patient>(
                It.IsAny<
                    Expression<Func<Patient, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                Expression<Func<Patient, bool>> predicate,
                string[] includes) =>
                    predicate
                        .Compile()
                        .Invoke(patient)
                            ? patient
                            : null);
    }
}
