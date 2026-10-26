using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Domain.Entities;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Dsw2026Tpi.Tests.Services;

public class AvailabilityServiceTests
{
    private readonly Mock<IPersistence> _persistence = new();
    private readonly Mock<IAvailabilityPersistence> _availabilityPersistence = new();
    private readonly Mock<IClinicalWriteScopeFactory> _scopeFactory = new();
    private readonly Mock<IClinicalWriteScope> _scope = new();
    private readonly Mock<IClinicClock> _clock = new();
    private readonly AvailabilityService _service;

    public AvailabilityServiceTests()
    {
        _clock.Setup(c => c.GetCurrentLocalDateTime())
            .Returns(new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.FromHours(-3)));
        _scopeFactory.Setup(s => s.BeginAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_scope.Object);
        _availabilityPersistence.Setup(p => p.GetRulesByDoctorAndMonth(
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(Array.Empty<AvailabilityRule>());
        _availabilityPersistence.Setup(p => p.GetByDoctorAndPeriod(
                It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(Array.Empty<Availability>());
        _service = new AvailabilityService(
            _persistence.Object, _availabilityPersistence.Object,
            _scopeFactory.Object, _clock.Object,
            Options.Create(new NonWorkingDaysOptions()),
            NullLogger<AvailabilityService>.Instance);
    }

    [Fact]
    public async Task Post_SinDias_RechazaAntesDeAbrirTransaccion()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.Create(new AvailabilityModel.Request(Guid.NewGuid(), [])));
        _scopeFactory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Post_Intervalo45Min_RechazaAntesDeAbrirTransaccion()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.Create(new AvailabilityModel.Request(Guid.NewGuid(),
                [new AvailabilityModel.DayRequest("LUNES", "09:00", "09:45")])));
        _scopeFactory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Post_RangosSuperpuestos_RechazaAntesDeAbrirTransaccion()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.Create(new AvailabilityModel.Request(Guid.NewGuid(),
            [
                new AvailabilityModel.DayRequest("LUNES", "09:00", "10:00"),
                new AvailabilityModel.DayRequest("LUNES", "09:30", "10:30")
            ])));
        _scopeFactory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Post_0915A1015_GeneraDosSlotsConRegla()
    {
        var id = Guid.NewGuid();
        StubActiveDoctor(id);
        var result = await _service.Create(new AvailabilityModel.Request(id,
            [new AvailabilityModel.DayRequest("LUNES", "09:15", "10:15")]));
        Assert.Equal(id, result.DoctorId);
        Assert.Single(result.Days);
        Assert.NotEqual(Guid.Empty, result.Days.Single().Id);
        _availabilityPersistence.Verify(p => p.AddRange(It.Is<IReadOnlyCollection<Availability>>(
            slots => slots.Count >= 2 && slots.All(s => s.AvailabilityRuleId != null))), Times.Once);
        _scope.Verify(s => s.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Put_Vacio_AceptaYSustituyeConCeroSlots()
    {
        var id = Guid.NewGuid();
        StubActiveDoctor(id);
        var result = await _service.Update(new AvailabilityModel.Request(id, []));
        Assert.Empty(result.Days);
        _availabilityPersistence.Verify(p => p.ReplaceFutureUnbooked(
            id, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
            It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(),
            It.Is<IReadOnlyCollection<Availability>>(items => items.Count == 0)), Times.Once);
        _scope.Verify(s => s.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSinReglas_NoInfiereAgendaDeSlots()
    {
        var id = Guid.NewGuid();
        StubActiveDoctor(id);
        var result = await _service.GetMonthlyPlanning(id);
        Assert.Empty(result);
        _availabilityPersistence.Verify(p => p.GetByDoctorAndPeriod(
            It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()), Times.Never);
    }

    private void StubActiveDoctor(Guid doctorId)
    {
        var speciality = new Speciality("Clínica", "Clínica de prueba");
        var doctor = new Doctor("Ana Prueba", "N-123", speciality, doctorId);
        _persistence.Setup(p => p.GetById<Doctor>(doctorId, It.IsAny<string[]>()))
            .ReturnsAsync(doctor);
    }
}
