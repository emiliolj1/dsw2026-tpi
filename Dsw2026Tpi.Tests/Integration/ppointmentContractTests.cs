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
using System.Text.Json;
using Xunit;

namespace Dsw2026Tpi.Tests.Integration;

public sealed class AppointmentContractTests
{
    private const string PatientFullName = "Paciente Contrato";

    [Fact]
    public async Task Book_AcceptsAvailabilitySlotId_AndReturnsCommonAppointmentDto()
    {
        using var factory = new CustomWebApplicationFactory();
        var date = FutureDate();
        var seed = await SeedAsync(factory, date, new TimeOnly(9, 0));
        using var patientClient = CreateClient(factory, seed.Patient.UserId, Roles.Patient);

        var body = await BookAsync(
            patientClient, seed.Doctor.Id, seed.Slots[0].Id, seed.Patient.Dni);

        Assert.NotEqual(Guid.Empty, body.GetProperty("appointmentsId").GetGuid());
        Assert.Equal("BOOKED", body.GetProperty("appointmentsStatus").GetString());
        Assert.Equal(seed.Slots[0].Id, body.GetProperty("availabilitySlotId").GetGuid());

        var patient = body.GetProperty("patient");
        Assert.Equal(seed.Patient.Dni, patient.GetProperty("dni").GetInt64());
        Assert.Equal(PatientFullName, patient.GetProperty("fullName").GetString());

        var doctor = body.GetProperty("doctor");
        Assert.Equal(seed.Doctor.Id, doctor.GetProperty("doctorId").GetGuid());
        Assert.Equal(seed.Doctor.Name, doctor.GetProperty("name").GetString());

        var specialty = doctor.GetProperty("specialty");
        Assert.Equal(seed.Speciality.Id, specialty.GetProperty("specialtyId").GetGuid());
        Assert.Equal(seed.Speciality.Name, specialty.GetProperty("name").GetString());

        Assert.Equal(date.ToString("yyyy-MM-dd"), body.GetProperty("date").GetString());
        Assert.Equal("09:00", body.GetProperty("startTime").GetString());
        var endTime = body.GetProperty("endTime").GetString();
        Assert.Equal($"09:00-{endTime}", body.GetProperty("availableTime").GetString());
        Assert.Equal("Consulta de control", body.GetProperty("reason").GetString());

        // Los nombres viejos no deben aparecer en el JSON público.
        Assert.False(body.TryGetProperty("id", out _));
        Assert.False(body.TryGetProperty("availabilityId", out _));
        Assert.False(body.TryGetProperty("status", out _));
    }

    [Fact]
    public async Task Cancel_ReturnsPlainTextOk_AndRemovesAppointmentFromActiveList()
    {
        using var factory = new CustomWebApplicationFactory();
        var seed = await SeedAsync(factory, FutureDate(), new TimeOnly(9, 0));
        using var patientClient = CreateClient(factory, seed.Patient.UserId, Roles.Patient);

        var booked = await BookAsync(
            patientClient, seed.Doctor.Id, seed.Slots[0].Id, seed.Patient.Dni);
        var appointmentId = booked.GetProperty("appointmentsId").GetGuid();
        var activeUrl = $"/api/appointments/patient?dni={seed.Patient.Dni}";

        using var activeBefore = await patientClient.GetAsync(activeUrl);
        Assert.Equal(HttpStatusCode.OK, activeBefore.StatusCode);
        var activeBeforeBody = await ReadJson(activeBefore);
        Assert.Equal(1, activeBeforeBody.GetArrayLength());
        Assert.Equal(
            appointmentId,
            activeBeforeBody[0].GetProperty("appointmentsId").GetGuid());

        using var cancelResponse = await patientClient.DeleteAsync(
            $"/api/appointments/{appointmentId}");

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        Assert.Equal("text/plain", cancelResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("ok", await cancelResponse.Content.ReadAsStringAsync());

        using var activeAfter = await patientClient.GetAsync(activeUrl);
        Assert.Equal(HttpStatusCode.OK, activeAfter.StatusCode);
        Assert.Equal(0, (await ReadJson(activeAfter)).GetArrayLength());
    }

    [Fact]
    public async Task AdminQueries_ReturnPagedDtoSortedByStartTime_AndFilterByStatus()
    {
        using var factory = new CustomWebApplicationFactory();
        var date = FutureDate();
        var day = date.ToString("yyyy-MM-dd");

        // Se cargan a propósito en desorden: primero 09:30 y después 09:00.
        var seed = await SeedAsync(
            factory, date, new TimeOnly(9, 30), new TimeOnly(9, 0));

        using var patientClient = CreateClient(factory, seed.Patient.UserId, Roles.Patient);
        using var adminClient = CreateClient(factory, Guid.NewGuid(), Roles.Administrator);

        var lateAppointment = await BookAsync(
            patientClient, seed.Doctor.Id, seed.Slots[0].Id, seed.Patient.Dni);
        await BookAsync(
            patientClient, seed.Doctor.Id, seed.Slots[1].Id, seed.Patient.Dni);

        using var cancelResponse = await patientClient.DeleteAsync(
            $"/api/appointments/{lateAppointment.GetProperty("appointmentsId").GetGuid()}");
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        // Consulta diaria sin filtro de estado: trae las dos, ordenadas por hora.
        var daily = await GetPageAsync(adminClient, $"/api/appointments?date={day}");
        Assert.Equal(10, daily.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, daily.GetProperty("pageIndex").GetInt32());
        Assert.Equal(2, daily.GetProperty("total").GetInt32());
        var dailyData = daily.GetProperty("data");
        Assert.Equal(2, dailyData.GetArrayLength());
        Assert.Equal("09:00", dailyData[0].GetProperty("startTime").GetString());
        Assert.Equal("09:30", dailyData[1].GetProperty("startTime").GetString());

        // Segunda página de tamaño 1: debe traer la de las 09:30.
        var secondPage = await GetPageAsync(
            adminClient, $"/api/appointments?date={day}&pageSize=1&pageIndex=1");
        Assert.Equal(2, secondPage.GetProperty("total").GetInt32());
        var secondPageData = secondPage.GetProperty("data");
        Assert.Equal(1, secondPageData.GetArrayLength());
        Assert.Equal("09:30", secondPageData[0].GetProperty("startTime").GetString());

        // Filtro por estado: solo queda la reservada.
        var booked = await GetPageAsync(
            adminClient, $"/api/appointments?date={day}&status=BOOKED");
        Assert.Equal(1, booked.GetProperty("total").GetInt32());
        var bookedItem = booked.GetProperty("data")[0];
        Assert.Equal("BOOKED", bookedItem.GetProperty("appointmentsStatus").GetString());
        Assert.Equal("09:00", bookedItem.GetProperty("startTime").GetString());

        // Búsqueda combinada con filtro de estado.
        var search = await GetPageAsync(
            adminClient,
            "/api/appointments/search"
            + $"?doctorId={seed.Doctor.Id}"
            + $"&dni={seed.Patient.Dni}"
            + $"&date={day}"
            + "&status=CANCELLED");
        Assert.Equal(1, search.GetProperty("total").GetInt32());
        var cancelledItem = search.GetProperty("data")[0];
        Assert.Equal("CANCELLED", cancelledItem.GetProperty("appointmentsStatus").GetString());
        Assert.Equal("09:30", cancelledItem.GetProperty("startTime").GetString());
        Assert.Equal(
            seed.Patient.Dni,
            cancelledItem.GetProperty("patient").GetProperty("dni").GetInt64());
    }

    [Theory]
    [InlineData("/api/appointments?date=2026-10-22&status=INVENTADO")]
    [InlineData("/api/appointments/search?status=INVENTADO")]
    [InlineData("/api/appointments?date=2026-10-22&pageSize=0")]
    [InlineData("/api/appointments")]
    public async Task AdminQueries_WithInvalidInput_ReturnBadRequestWithCommonErrorFormat(
        string url)
    {
        using var factory = new CustomWebApplicationFactory();
        using var adminClient = CreateClient(factory, Guid.NewGuid(), Roles.Administrator);

        using var response = await adminClient.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJson(response);
        Assert.True(body.TryGetProperty("errorCode", out _));
        Assert.True(body.TryGetProperty("message", out _));
    }

    // ---------- Ayudas para armar los escenarios ----------

    private sealed record SeedData(
        Speciality Speciality,
        Doctor Doctor,
        Patient Patient,
        Availability[] Slots);

    private static DateOnly FutureDate()
    {
        // Dos días adelante, para que nunca caiga en el pasado.
        return DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
    }

    private static async Task<SeedData> SeedAsync(
        CustomWebApplicationFactory factory,
        DateOnly date,
        params TimeOnly[] startTimes)
    {
        var speciality = new Speciality(
            "Clínica Médica", "Especialidad para pruebas de contrato");
        var doctor = new Doctor("Ana Contrato", "MP-CONTRATO-001", speciality);
        var patient = new Patient(Guid.NewGuid(), 30123456, PatientFullName);
        var slots = startTimes
            .Select(startTime => new Availability(doctor.Id, date, startTime))
            .ToArray();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2026TpiDbContext>();
        context.AddRange(speciality, doctor, patient);
        context.AddRange(slots);
        await context.SaveChangesAsync();

        return new SeedData(speciality, doctor, patient, slots);
    }

    private static async Task<JsonElement> BookAsync(
        HttpClient patientClient,
        Guid doctorId,
        Guid availabilitySlotId,
        long dni)
    {
        using var content = JsonContent($$"""
            {
              "doctorId": "{{doctorId}}",
              "availabilitySlotId": "{{availabilitySlotId}}",
              "patient": { "dni": {{dni}} },
              "reason": "Consulta de control"
            }
            """);

        using var response = await patientClient.PostAsync("/api/appointments", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJson(response);
    }

    private static async Task<JsonElement> GetPageAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJson(response);
    }

    private static HttpClient CreateClient(
        CustomWebApplicationFactory factory,
        Guid userId,
        string role)
    {
        var client = factory.CreateClient();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: CustomWebApplicationFactory.JwtIssuer,
            audience: CustomWebApplicationFactory.JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", new JwtSecurityTokenHandler().WriteToken(token));

        return client;
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}