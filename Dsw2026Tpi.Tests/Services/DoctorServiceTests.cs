using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using System.Linq.Expressions;
using Moq;
using Dsw2026Tpi.Application.Interfaces;

namespace Dsw2026Tpi.Tests.Services;

public class DoctorServiceTests
{
    private readonly Mock<IPersistence> _persistence;
    private readonly Mock<IClinicalWriteScopeFactory> _writeScopeFactory;
    private readonly Mock<IClinicalWriteScope> _writeScope;
    private readonly Mock<IClinicClock> _clock;
    private readonly DoctorService _service;

    public DoctorServiceTests()
    {
        _persistence = new Mock<IPersistence>();
        _writeScopeFactory = new Mock<IClinicalWriteScopeFactory>();
        _writeScope = new Mock<IClinicalWriteScope>();
        _clock = new Mock<IClinicClock>();

        _writeScopeFactory
            .Setup(factory => factory.BeginAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_writeScope.Object);

        _writeScope
            .Setup(scope => scope.CompleteAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _writeScope
            .Setup(scope => scope.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _clock
            .Setup(clock => clock.GetCurrentLocalDateTime())
            .Returns(new DateTimeOffset(
                2026, 10, 20, 10, 0, 0,
                TimeSpan.FromHours(-3)));

        _service = new DoctorService(
            _persistence.Object,
            _writeScopeFactory.Object,
            _clock.Object);
    }

    [Fact]
    public async Task Create_WithValidRequest_CreatesAndReturnsDoctor()
    {
        var speciality = new Speciality(
            "Cardiología",
            "Atención cardiológica general");

        var request = new DoctorModel.Request(
            "Ana Pérez",
            "MP-12345",
            speciality.Id);

        _persistence
            .Setup(p => p.GetById<Speciality>(
                speciality.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(speciality);

        _persistence
            .Setup(p => p.Add(It.IsAny<Doctor>()))
            .ReturnsAsync((Doctor doctor) => doctor);

        var result = await _service.Create(request);

        Assert.Equal("Ana Pérez", result.Name);
        Assert.Equal("MP-12345", result.LicenseNumber);
        Assert.Equal(speciality.Id, result.Specialty?.Id);
        Assert.Equal("Cardiología", result.Specialty?.Name);

        _persistence.Verify(
            p => p.Add(It.IsAny<Doctor>()),
            Times.Once);
    }

    [Fact]
    public async Task Update_WithValidRequest_UpdatesAndReturnsDoctor()
    {
        var previousSpeciality = new Speciality(
            "Clínica",
            "Atención clínica general");

        var newSpeciality = new Speciality(
            "Neurología",
            "Atención neurológica general");

        var doctor = new Doctor(
            "Juan López",
            "MP-10000",
            previousSpeciality);

        var request = new DoctorModel.Request(
            "Juan Carlos López",
            "MP-20000",
            newSpeciality.Id);

        _persistence
            .Setup(p => p.GetById<Doctor>(
                doctor.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(doctor);

        _persistence
            .Setup(p => p.GetById<Speciality>(
                newSpeciality.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(newSpeciality);

        _persistence
            .Setup(p => p.Update(doctor))
            .ReturnsAsync(doctor);

        var result = await _service.Update(
            doctor.Id,
            request);

        Assert.Equal("Juan Carlos López", result.Name);
        Assert.Equal("MP-20000", result.LicenseNumber);
        Assert.Equal(newSpeciality.Id, result.Specialty?.Id);
        Assert.Equal("Neurología", result.Specialty?.Name);

        _persistence.Verify(
            p => p.Update(doctor),
            Times.Once);
    }

    [Fact]
    public async Task Delete_WhenDoctorExists_DeactivatesAndDeletesDoctor()
    {
        var speciality = new Speciality(
            "Pediatría",
            "Atención pediátrica general");

        var doctor = new Doctor(
            "María Gómez",
            "MP-30000",
            speciality);

        _persistence
            .Setup(p => p.GetById<Doctor>(
                doctor.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(doctor);

        _persistence
            .Setup(p => p.Delete(doctor))
            .ReturnsAsync(doctor);

        await _service.Delete(doctor.Id);

        Assert.False(doctor.IsActive);

        _persistence.Verify(
            p => p.Delete(doctor),
            Times.Once);
    }
    [Fact]
    public async Task Create_WithInvalidName_ThrowsValidationException()
    {
        var request = new DoctorModel.Request(
            "  ",
            "MP-12345",
            Guid.NewGuid());

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.Create(request));

        _persistence.Verify(
            p => p.Add(It.IsAny<Doctor>()),
            Times.Never);
    }

    [Fact]
    public async Task Create_WhenSpecialityDoesNotExist_ThrowsEntityNotFoundException()
    {
        var specialityId = Guid.NewGuid();

        var request = new DoctorModel.Request(
            "Ana Pérez",
            "MP-12345",
            specialityId);

        _persistence
            .Setup(p => p.GetById<Speciality>(
                specialityId,
                It.IsAny<string[]>()))
            .ReturnsAsync((Speciality?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _service.Create(request));

        _persistence.Verify(
            p => p.Add(It.IsAny<Doctor>()),
            Times.Never);
    }

    [Fact]
    public async Task Update_WhenDoctorDoesNotExist_ThrowsEntityNotFoundException()
    {
        var doctorId = Guid.NewGuid();

        var request = new DoctorModel.Request(
            "Juan López",
            "MP-20000",
            Guid.NewGuid());

        _persistence
            .Setup(p => p.GetById<Doctor>(
                doctorId,
                It.IsAny<string[]>()))
            .ReturnsAsync((Doctor?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _service.Update(doctorId, request));

        _persistence.Verify(
            p => p.Update(It.IsAny<Doctor>()),
            Times.Never);
    }

    [Fact]
    public async Task Delete_WhenDoctorDoesNotExist_ThrowsEntityNotFoundException()
    {
        var doctorId = Guid.NewGuid();

        _persistence
            .Setup(p => p.GetById<Doctor>(
                doctorId,
                It.IsAny<string[]>()))
            .ReturnsAsync((Doctor?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _service.Delete(doctorId));

        _persistence.Verify(
            p => p.Delete(It.IsAny<Doctor>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAll_WithInvalidPagination_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.GetAll(
                pageSize: 0,
                pageIndex: -1));
    }
    [Fact]
    public async Task GetAll_WithInvalidName_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.GetAll(
                pageSize: 10,
                pageIndex: 0,
                name: "AB"));

        _persistence.Verify(
            p => p.Paginate<Doctor, string>(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<Doctor, bool>>>(),
                It.IsAny<Expression<Func<Doctor, string>>>(),
                It.IsAny<string[]>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAll_WithNameFilter_ReturnsOnlyActiveMatchingDoctors()
    {
        var speciality = new Speciality(
            "Cardiología",
            "Atención cardiológica general");

        var matchingDoctor = new Doctor(
            "Ana Pérez",
            "MP-10000",
            speciality);

        var inactiveDoctor = new Doctor(
            "Ana Gómez",
            "MP-20000",
            speciality);

        inactiveDoctor.Deactivate();

        var otherDoctor = new Doctor(
            "Bruno Díaz",
            "MP-30000",
            speciality);

        var doctors = new[]
        {
        matchingDoctor,
        inactiveDoctor,
        otherDoctor
    };

        _persistence
            .Setup(p => p.Paginate<Doctor, string>(
                10,
                0,
                It.IsAny<Expression<Func<Doctor, bool>>>(),
                It.IsAny<Expression<Func<Doctor, string>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                int pageSize,
                int pageIndex,
                Expression<Func<Doctor, bool>> predicate,
                Expression<Func<Doctor, string>> sortOrder,
                string[] includes) =>
            {
                var filtered = doctors
                    .AsQueryable()
                    .Where(predicate)
                    .OrderBy(sortOrder)
                    .ToList();

                return new Pagination<Doctor>(
                    pageSize,
                    pageIndex,
                    filtered.Count,
                    filtered);
            });

        var result = await _service.GetAll(
            pageSize: 10,
            pageIndex: 0,
            name: " Ana ");

        var returnedDoctor = Assert.Single(result.Data);

        Assert.Equal(1, result.Total);
        Assert.Equal(matchingDoctor.Id, returnedDoctor.Id);
        Assert.Equal("Ana Pérez", returnedDoctor.Name);

        _persistence.Verify(
            p => p.Paginate<Doctor, string>(
                10,
                0,
                It.IsAny<Expression<Func<Doctor, bool>>>(),
                It.IsAny<Expression<Func<Doctor, string>>>(),
                It.Is<string[]>(includes =>
                    includes.Contains(nameof(Doctor.Speciality)))),
            Times.Once);
    }

    [Fact]
    public async Task GetAll_WithEmptySpecialtyId_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.GetAll(
                pageSize: 10,
                pageIndex: 0,
                specialtyId: Guid.Empty));

        _persistence.Verify(
            p => p.Paginate<Doctor, string>(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Expression<Func<Doctor, bool>>>(),
                It.IsAny<Expression<Func<Doctor, string>>>(),
                It.IsAny<string[]>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData(" Ana ", 1)]
    public async Task GetAll_WithSpecialtyFilter_CombinesFiltersAndExcludesDeleted(
        string? name,
        int expectedTotal)
    {
        var targetSpeciality = new Speciality(
            "Cardiología",
            "Especialidad buscada");

        var otherSpeciality = new Speciality(
            "Neurología",
            "Otra especialidad");

        var deletedSpeciality = new Speciality(
            "Pediatría",
            "Especialidad eliminada")
        {
            Deleted = true
        };

        var ana = new Doctor("Ana Pérez", "MP-101", targetSpeciality);
        var bruno = new Doctor("Bruno Díaz", "MP-102", targetSpeciality);

        var inactive = new Doctor(
            "Ana Inactiva", "MP-103", targetSpeciality);
        inactive.Deactivate();

        var deleted = new Doctor(
            "Ana Eliminada", "MP-104", targetSpeciality)
        {
            Deleted = true
        };

        var other = new Doctor(
            "Ana Otra", "MP-105", otherSpeciality);

        var relatedToDeleted = new Doctor(
            "Ana Sin Especialidad Vigente",
            "MP-106",
            deletedSpeciality);

        var doctors = new[]
        {
        ana,
        bruno,
        inactive,
        deleted,
        other,
        relatedToDeleted
    };

        _persistence
            .Setup(p => p.Paginate<Doctor, string>(
                10,
                0,
                It.IsAny<Expression<Func<Doctor, bool>>>(),
                It.IsAny<Expression<Func<Doctor, string>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                int pageSize,
                int pageIndex,
                Expression<Func<Doctor, bool>> predicate,
                Expression<Func<Doctor, string>> sortOrder,
                string[] includes) =>
            {
                var filtered = doctors
                    .AsQueryable()
                    .Where(predicate)
                    .OrderBy(sortOrder)
                    .ToList();

                return new Pagination<Doctor>(
                    pageSize,
                    pageIndex,
                    filtered.Count,
                    filtered);
            });

        var result = await _service.GetAll(
            pageSize: 10,
            pageIndex: 0,
            name: name,
            specialtyId: targetSpeciality.Id);

        Assert.Equal(expectedTotal, result.Total);
        Assert.Equal(expectedTotal, result.Data.Count());

        Assert.Contains(result.Data, doctor => doctor.Id == ana.Id);

        if (name is null)
        {
            Assert.Contains(result.Data, doctor => doctor.Id == bruno.Id);
        }
        else
        {
            Assert.DoesNotContain(
                result.Data,
                doctor => doctor.Id == bruno.Id);
        }

        Assert.All(result.Data, doctor =>
            Assert.Equal(targetSpeciality.Id, doctor.Specialty?.Id));

        // Sin filtro de especialidad también deben excluirse
        // médicos asociados a especialidades eliminadas.
        var unfiltered = await _service.GetAll(10, 0);

        Assert.Equal(3, unfiltered.Total);

        Assert.DoesNotContain(
            unfiltered.Data,
            doctor => doctor.Id == relatedToDeleted.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Mutation_WithFutureBooking_RejectsWithoutChangingDoctor(
    bool changeSpecialty)
    {
        var speciality = new Speciality(
            "Cardiología",
            "Especialidad original del médico");

        var doctor = new Doctor(
            "Ana Pérez",
            "MP-ORIGINAL",
            speciality);

        var booking = new Appointment(
            doctor.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Consulta de control");

        _persistence
            .Setup(p => p.GetById<Doctor>(
                doctor.Id,
                It.IsAny<string[]>()))
            .Callback(() =>
            {
                _writeScopeFactory.Verify(
                    factory => factory.BeginAsync(
                        It.IsAny<CancellationToken>()),
                    Times.Once);
            })
            .ReturnsAsync(doctor);

        // Simula que la consulta encuentra una reserva que impide operar.
        // La selección por fecha/hora se verificará con SQL real.
        _persistence
            .Setup(p => p.First<Appointment>(
                It.IsAny<Expression<Func<Appointment, bool>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync(booking);

        if (changeSpecialty)
        {
            var request = new DoctorModel.Request(
                "Nombre modificado",
                "MP-MODIFICADA",
                Guid.NewGuid());

            await Assert.ThrowsAsync<ConflictException>(
                () => _service.Update(doctor.Id, request));
        }
        else
        {
            await Assert.ThrowsAsync<ConflictException>(
                () => _service.Delete(doctor.Id));
        }

        Assert.True(doctor.IsActive);
        Assert.False(doctor.Deleted);
        Assert.Equal("Ana Pérez", doctor.Name);
        Assert.Equal("MP-ORIGINAL", doctor.LicenseNumber);
        Assert.Equal(speciality.Id, doctor.SpecialityId);

        _persistence.Verify(
            p => p.Update(It.IsAny<Doctor>()),
            Times.Never);

        _persistence.Verify(
            p => p.Delete(It.IsAny<Doctor>()),
            Times.Never);

        _clock.Verify(
            clock => clock.GetCurrentLocalDateTime(),
            Times.Once);

        _writeScope.Verify(
            scope => scope.CompleteAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);

        _writeScope.Verify(
            scope => scope.DisposeAsync(),
            Times.Once);
    }

    [Fact]
    public async Task Update_WithSameSpecialty_DoesNotCheckFutureBookings()
    {
        var speciality = new Speciality(
            "Cardiología",
            "Especialidad actual del médico");

        var doctor = new Doctor(
            "Ana Pérez",
            "MP-ORIGINAL",
            speciality);

        _persistence
            .Setup(p => p.GetById<Doctor>(
                doctor.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(doctor);

        _persistence
            .Setup(p => p.GetById<Speciality>(
                speciality.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(speciality);

        _persistence
            .Setup(p => p.Update(doctor))
            .ReturnsAsync(doctor);

        var result = await _service.Update(
            doctor.Id,
            new DoctorModel.Request(
                "Ana Pérez Actualizada",
                "MP-ACTUALIZADA",
                speciality.Id));

        Assert.Equal("Ana Pérez Actualizada", result.Name);
        Assert.Equal("MP-ACTUALIZADA", result.LicenseNumber);
        Assert.Equal(speciality.Id, result.Specialty?.Id);

        _persistence.Verify(
            p => p.First<Appointment>(
                It.IsAny<Expression<Func<Appointment, bool>>>(),
                It.IsAny<string[]>()),
            Times.Never);

        _writeScope.Verify(
            scope => scope.CompleteAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
