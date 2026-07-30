using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Dsw2026Tpi.CrossCutting.Exceptions;
using System.Linq.Expressions;
using Moq;

namespace Dsw2026Tpi.Tests.Services;

public class DoctorServiceTests
{
    private readonly Mock<IPersistence> _persistence;
    private readonly DoctorService _service;

    public DoctorServiceTests()
    {
        _persistence = new Mock<IPersistence>();
        _service = new DoctorService(_persistence.Object);
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
}