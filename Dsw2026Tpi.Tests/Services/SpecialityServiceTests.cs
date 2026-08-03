using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Moq;
using System.Linq.Expressions;

namespace Dsw2026Tpi.Tests.Services;

public class SpecialityServiceTests
{
    private readonly Mock<IPersistence> _persistence;
    private readonly SpecialityService _service;

    public SpecialityServiceTests()
    {
        _persistence = new Mock<IPersistence>();
        _service = new SpecialityService(_persistence.Object);
    }

    [Fact]
    public async Task Create_WithValidRequest_CreatesAndReturnsSpeciality()
    {
        var request = new SpecialityModel.Request(
            "Cardiología",
            "Atención cardiológica general");

        _persistence
            .Setup(p => p.Add(It.IsAny<Speciality>()))
            .ReturnsAsync((Speciality speciality) => speciality);

        var result = await _service.Create(request);

        Assert.Equal("Cardiología", result.Name);
        Assert.Equal(
            "Atención cardiológica general",
            result.Description);

        _persistence.Verify(
            p => p.Add(It.Is<Speciality>(speciality =>
                speciality.Name == "Cardiología" &&
                speciality.Description ==
                    "Atención cardiológica general")),
            Times.Once);
    }

    [Fact]
    public async Task Create_WithInvalidRequest_ThrowsValidationException()
    {
        var request = new SpecialityModel.Request(
            "  ",
            "Corta");

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.Create(request));

        _persistence.Verify(
            p => p.Add(It.IsAny<Speciality>()),
            Times.Never);
    }

    [Fact]
    public async Task Update_WithValidRequest_UpdatesAndReturnsSpeciality()
    {
        var speciality = new Speciality(
            "Clínica",
            "Atención clínica general");

        var request = new SpecialityModel.Request(
            "Clínica médica",
            "Atención clínica integral");

        _persistence
            .Setup(p => p.GetById<Speciality>(
                speciality.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(speciality);

        _persistence
            .Setup(p => p.Update(speciality))
            .ReturnsAsync(speciality);

        var result = await _service.Update(
            speciality.Id,
            request);

        Assert.Equal("Clínica médica", result.Name);
        Assert.Equal(
            "Atención clínica integral",
            result.Description);

        _persistence.Verify(
            p => p.Update(speciality),
            Times.Once);
    }

    [Fact]
    public async Task Update_WhenSpecialityDoesNotExist_ThrowsEntityNotFoundException()
    {
        var specialityId = Guid.NewGuid();

        var request = new SpecialityModel.Request(
            "Neurología",
            "Atención neurológica general");

        _persistence
            .Setup(p => p.GetById<Speciality>(
                specialityId,
                It.IsAny<string[]>()))
            .ReturnsAsync((Speciality?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _service.Update(
                specialityId,
                request));

        _persistence.Verify(
            p => p.Update(It.IsAny<Speciality>()),
            Times.Never);
    }

    [Fact]
    public async Task Delete_WhenSpecialityExists_DeletesSpeciality()
    {
        var speciality = new Speciality(
            "Pediatría",
            "Atención pediátrica general");

        _persistence
            .Setup(p => p.GetById<Speciality>(
                speciality.Id,
                It.IsAny<string[]>()))
            .ReturnsAsync(speciality);

        _persistence
            .Setup(p => p.Delete(speciality))
            .ReturnsAsync(speciality);

        await _service.Delete(speciality.Id);

        _persistence.Verify(
            p => p.Delete(speciality),
            Times.Once);
    }

    [Fact]
    public async Task Delete_WhenSpecialityDoesNotExist_ThrowsEntityNotFoundException()
    {
        var specialityId = Guid.NewGuid();

        _persistence
            .Setup(p => p.GetById<Speciality>(
                specialityId,
                It.IsAny<string[]>()))
            .ReturnsAsync((Speciality?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _service.Delete(specialityId));

        _persistence.Verify(
            p => p.Delete(It.IsAny<Speciality>()),
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
            p => p.Paginate<Speciality, string>(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<
                    Expression<Func<Speciality, bool>>>(),
                It.IsAny<
                    Expression<Func<Speciality, string>>>(),
                It.IsAny<string[]>()),
            Times.Never);
    }

    [Fact]
    public async Task GetAll_WithNameFilter_ReturnsMatchingSpecialities()
    {
        var matchingSpeciality = new Speciality(
            "Cardiología",
            "Atención cardiológica general");

        var otherSpeciality = new Speciality(
            "Neurología",
            "Atención neurológica general");

        var specialities = new[]
        {
            matchingSpeciality,
            otherSpeciality
        };

        _persistence
            .Setup(p => p.Paginate<Speciality, string>(
                10,
                0,
                It.IsAny<
                    Expression<Func<Speciality, bool>>>(),
                It.IsAny<
                    Expression<Func<Speciality, string>>>(),
                It.IsAny<string[]>()))
            .ReturnsAsync((
                int pageSize,
                int pageIndex,
                Expression<Func<Speciality, bool>> predicate,
                Expression<Func<Speciality, string>> sortOrder,
                string[] includes) =>
            {
                var filtered = specialities
                    .AsQueryable()
                    .Where(predicate)
                    .OrderBy(sortOrder)
                    .ToList();

                return new Pagination<Speciality>(
                    pageSize,
                    pageIndex,
                    filtered.Count,
                    filtered);
            });

        var result = await _service.GetAll(
            pageSize: 10,
            pageIndex: 0,
            name: " Card ");

        var returnedSpeciality =
            Assert.Single(result.Data);

        Assert.Equal(1, result.Total);
        Assert.Equal(
            matchingSpeciality.Id,
            returnedSpeciality.Id);
        Assert.Equal(
            "Cardiología",
            returnedSpeciality.Name);
    }
}