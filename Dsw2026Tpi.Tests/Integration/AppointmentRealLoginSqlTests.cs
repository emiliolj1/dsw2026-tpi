using Dsw2026Tpi.Data;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class AppointmentRealLoginSqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public AppointmentRealLoginSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PatientWithRealLogin_BooksAndCancelsByHttp()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);

        var speciality = new Speciality(
            $"Esp {suffix}", "Especialidad para prueba HTTP con login real");
        var doctor = new Doctor(
            "Ana Login Real", $"L04-{suffix}", speciality);
        var slot = new Availability(doctor.Id, day, new TimeOnly(10, 0));

        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<Dsw2026TpiDbContext>();
            context.AddRange(speciality, doctor, slot);
            await context.SaveChangesAsync();
        }

        var dni = Random.Shared.NextInt64(60_000_000, 69_999_999);

        using var client = _fixture.Factory.CreateClient();

        // 1. Login real: crea el paciente y devuelve el token.
        using var login = await client.PostAsJsonAsync(
            "/api/auth/patient/login",
            new { email = $"l04-{suffix}@example.test", dni });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = session.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        // 2. Reservar por HTTP.
        using var booking = await client.PostAsJsonAsync(
            "/api/appointments",
            new
            {
                doctorId = doctor.Id,
                availabilitySlotId = slot.Id,
                patient = new { dni },
                reason = "Consulta por login real"
            });

        Assert.Equal(HttpStatusCode.Created, booking.StatusCode);

        var appointmentId = await GetAppointmentIdBySlotAsync(slot.Id);

        Assert.Equal(AvailabilityStatus.Booked, await GetSlotStatusAsync(slot.Id));
        Assert.Equal(
            AppointmentStatus.BOOKED, await GetAppointmentStatusAsync(appointmentId));

        // 3. Cancelar por HTTP.
        using var cancel = await client.DeleteAsync(
            $"/api/appointments/{appointmentId}");

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal("ok", await cancel.Content.ReadAsStringAsync());

        Assert.Equal(
            AppointmentStatus.CANCELLED, await GetAppointmentStatusAsync(appointmentId));
        Assert.Equal(AvailabilityStatus.Available, await GetSlotStatusAsync(slot.Id));
    }

    private async Task<Guid> GetAppointmentIdBySlotAsync(Guid slotId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider
            .GetRequiredService<Dsw2026TpiDbContext>();

        return await context.Set<Appointment>().AsNoTracking()
            .Where(a => a.Availability!.Id == slotId)
            .Select(a => a.Id)
            .SingleAsync();
    }

    private async Task<AppointmentStatus> GetAppointmentStatusAsync(Guid id)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider
            .GetRequiredService<Dsw2026TpiDbContext>();

        return await context.Set<Appointment>().AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => a.Status)
            .SingleAsync();
    }

    private async Task<AvailabilityStatus> GetSlotStatusAsync(Guid slotId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider
            .GetRequiredService<Dsw2026TpiDbContext>();

        return await context.Set<Availability>().AsNoTracking()
            .Where(s => s.Id == slotId)
            .Select(s => s.Status)
            .SingleAsync();
    }
}