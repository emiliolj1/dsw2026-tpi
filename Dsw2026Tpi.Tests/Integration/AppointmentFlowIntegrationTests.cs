using Dsw2026Tpi.Application.Dtos;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Data;
using Dsw2026Tpi.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace Dsw2026Tpi.Tests.Integration;

public sealed class AppointmentFlowIntegrationTests
{
    [Fact]
    public async Task AppointmentFlow_BookCancelAndRelease_WorksCorrectly()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var patientUserId = Guid.NewGuid();
        const long patientDni = 30123456;
        var targetDate =
            DateOnly.FromDateTime(DateTime.Now).AddDays(1);

        var speciality = new Speciality(
            "Clínica Médica",
            "Especialidad para pruebas");

        var doctor = new Doctor(
            "Ana Test",
            "MP-TEST-001",
            speciality);

        var patient = new Patient(
            patientUserId,
            patientDni,
            "Paciente Test");

        var availability = new Availability(
            doctor.Id,
            targetDate,
            new TimeOnly(9, 0));

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider
                .GetRequiredService<Dsw2026TpiDbContext>();

            context.AddRange(
                speciality,
                doctor,
                patient,
                availability);

            await context.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreateToken(patientUserId, Roles.Patient));

        var initialSlots =
            await client.GetFromJsonAsync<
                List<AvailabilityModel.AvailableSlotResponse>>(
                $"/api/availabilities?doctorId={doctor.Id}");

        Assert.NotNull(initialSlots);
        Assert.Contains(
            initialSlots,
            slot => slot.AvailabilityId == availability.Id);

        var appointmentRequest = new AppointmentModel.Request(
            doctor.Id,
            availability.Id,
            new AppointmentModel.PatientRequest(patientDni),
            "Consulta médica general");

        using var bookingResponse =
            await client.PostAsJsonAsync(
                "/api/appointments",
                appointmentRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            bookingResponse.StatusCode);

        using var duplicateResponse =
            await client.PostAsJsonAsync(
                "/api/appointments",
                appointmentRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicateResponse.StatusCode);

        var activeAppointments =
            await client.GetFromJsonAsync<
                List<AppointmentModel.Response>>(
                $"/api/appointments/patient?dni={patientDni}");

        var appointment = Assert.Single(activeAppointments!);

        Assert.Equal(availability.Id, appointment.AvailabilityId);
        Assert.Equal("BOOKED", appointment.Status);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreateToken(
                    Guid.NewGuid(),
                    Roles.Administrator));

        var searchResult =
            await client.GetFromJsonAsync<
                AppointmentModel.PagedResponse>(
                "/api/appointments/search" +
                $"?doctorId={doctor.Id}" +
                $"&dni={patientDni}" +
                $"&date={targetDate:yyyy-MM-dd}" +
                "&pageSize=10&pageIndex=0");

        Assert.NotNull(searchResult);
        Assert.Contains(
            searchResult.Data,
            result => result.Id == appointment.Id);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreateToken(patientUserId, Roles.Patient));

        using var cancellationResponse =
            await client.DeleteAsync(
                $"/api/appointments/{appointment.Id}");

        Assert.Equal(
            HttpStatusCode.NoContent,
            cancellationResponse.StatusCode);

        var appointmentsAfterCancellation =
            await client.GetFromJsonAsync<
                List<AppointmentModel.Response>>(
                $"/api/appointments/patient?dni={patientDni}");

        Assert.Empty(appointmentsAfterCancellation!);

        var releasedSlots =
            await client.GetFromJsonAsync<
                List<AvailabilityModel.AvailableSlotResponse>>(
                $"/api/availabilities?doctorId={doctor.Id}");

        Assert.NotNull(releasedSlots);
        Assert.Contains(
            releasedSlots,
            slot => slot.AvailabilityId == availability.Id);
    }

    private static string CreateToken(
        Guid userId,
        string role)
    {
        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                userId.ToString()),

            new Claim(
                ClaimTypes.Role,
                role)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                CustomWebApplicationFactory.JwtKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: CustomWebApplicationFactory.JwtIssuer,
            audience: CustomWebApplicationFactory.JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}
