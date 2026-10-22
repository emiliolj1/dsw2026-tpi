using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.Application.Interfaces;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class AppointmentSearchSqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public AppointmentSearchSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Search_SortsByDateAndStartTime_AndPaginates_WithRealSql()
    {
        var seed = await SeedAsync();
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<IAppointmentService>();

        var firstPage = await service.Search(
            new AppointmentModel.SearchRequest(null, seed.DoctorId, null, null, 2, 0));
        var secondPage = await service.Search(
            new AppointmentModel.SearchRequest(null, seed.DoctorId, null, null, 2, 1));

        Assert.Equal(3, firstPage.Total);
        Assert.Equal(
            new[] { $"{seed.Day1:yyyy-MM-dd} 09:00", $"{seed.Day1:yyyy-MM-dd} 09:30" },
            firstPage.Data.Select(a => $"{a.Date:yyyy-MM-dd} {a.StartTime}").ToArray());
        Assert.Equal(
            new[] { $"{seed.Day2:yyyy-MM-dd} 08:00" },
            secondPage.Data.Select(a => $"{a.Date:yyyy-MM-dd} {a.StartTime}").ToArray());
    }

    [Fact]
    public async Task Search_FiltersByStatus_WithRealSql()
    {
        var seed = await SeedAsync();
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<IAppointmentService>();

        var booked = await service.Search(
            new AppointmentModel.SearchRequest(
                null, seed.DoctorId, null, seed.Day1, 10, 0, "BOOKED"));
        var cancelled = await service.Search(
            new AppointmentModel.SearchRequest(
                null, seed.DoctorId, null, seed.Day1, 10, 0, "CANCELLED"));

        Assert.Equal(1, booked.Total);
        Assert.Equal("09:00", booked.Data.Single().StartTime);
        Assert.Equal(1, cancelled.Total);
        Assert.Equal("09:30", cancelled.Data.Single().StartTime);
    }

    private sealed record Seed(Guid DoctorId, DateOnly Day1, DateOnly Day2);

    private async Task<Seed> SeedAsync()
    {
        var day1 = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        var day2 = day1.AddDays(1);
        var suffix = Guid.NewGuid().ToString("N");

        var speciality = new Speciality(
            $"Esp {suffix}", "Especialidad para pruebas SQL de citas");
        var doctor = new Doctor("Ana Citas SQL", $"SQL-APP-{suffix}", speciality);
        var patient = new Patient(
            Guid.NewGuid(),
            Random.Shared.NextInt64(10_000_000, 99_999_999),
            "Paciente SQL");

        var slotLate = new Availability(doctor.Id, day1, new TimeOnly(9, 30));
        var slotEarly = new Availability(doctor.Id, day1, new TimeOnly(9, 0));
        var slotNextDay = new Availability(doctor.Id, day2, new TimeOnly(8, 0));

        var cancelled = new Appointment(
            doctor.Id, slotLate.Id, patient.Id, "Consulta cancelada SQL");
        cancelled.Cancel();
        var early = new Appointment(
            doctor.Id, slotEarly.Id, patient.Id, "Consulta temprana SQL");
        var nextDay = new Appointment(
            doctor.Id, slotNextDay.Id, patient.Id, "Consulta dia siguiente SQL");

        using var scope = _fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider
            .GetRequiredService<Dsw2026TpiDbContext>();
        context.AddRange(speciality, doctor, patient);
        context.AddRange(slotLate, slotEarly, slotNextDay);
        context.AddRange(cancelled, early, nextDay);
        await context.SaveChangesAsync();

        return new Seed(doctor.Id, day1, day2);
    }
}