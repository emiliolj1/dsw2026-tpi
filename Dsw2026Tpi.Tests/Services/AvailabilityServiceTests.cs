using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Extensions.Options;
using Moq;

namespace Dsw2026Tpi.Tests.Services;

public class AvailabilityServiceTests
{
    private readonly Mock<IPersistence> _persistence;
    private readonly Mock<IAvailabilityPersistence> _availabilityPersistence;
    private readonly AvailabilityService _service;

    public AvailabilityServiceTests()
    {
        _persistence = new Mock<IPersistence>();
        _availabilityPersistence = new Mock<IAvailabilityPersistence>();

        var nonWorkingDays = Options.Create(
            new NonWorkingDaysOptions());

        _service = new AvailabilityService(
            _persistence.Object,
            _availabilityPersistence.Object,
            nonWorkingDays);
    }

    [Fact]
    public async Task Create_WithEmptyDoctorId_ThrowsValidationException()
    {
        var request = new AvailabilityModel.Request(
            Guid.Empty,
            [new AvailabilityModel.DayRequest(
                "LUNES",
                "09:00",
                "12:00")]);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.Create(request));

        VerifyPersistenceWasNotCalled();
    }

    [Fact]
    public async Task Create_WithIncompleteThirtyMinuteInterval_ThrowsValidationException()
    {
        var request = new AvailabilityModel.Request(
            Guid.NewGuid(),
            [new AvailabilityModel.DayRequest(
                "LUNES",
                "09:00",
                "09:45")]);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.Create(request));

        VerifyPersistenceWasNotCalled();
    }

    [Fact]
    public async Task Create_WithOverlappingRanges_ThrowsValidationException()
    {
        var request = new AvailabilityModel.Request(
            Guid.NewGuid(),
            [
                new AvailabilityModel.DayRequest(
                    "LUNES",
                    "09:00",
                    "12:00"),
                new AvailabilityModel.DayRequest(
                    "LUNES",
                    "11:30",
                    "13:00")
            ]);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.Create(request));

        VerifyPersistenceWasNotCalled();
    }

    private void VerifyPersistenceWasNotCalled()
    {
        _persistence.VerifyNoOtherCalls();
        _availabilityPersistence.VerifyNoOtherCalls();
    }
}   
