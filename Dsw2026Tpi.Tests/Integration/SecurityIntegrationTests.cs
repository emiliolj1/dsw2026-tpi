using Dsw2026Tpi.CrossCutting.Identity;
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

public sealed class SecurityIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SecurityIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthCheck_WithoutToken_ReturnsUniform401()
    {
        using var client = _factory.CreateClient();

        using var response =
            await client.GetAsync("/health-check");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        var body =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "AUTHENTICATION_FAILED",
            body.GetProperty("errorCode").GetString());

        Assert.True(body.TryGetProperty("message", out _));
        Assert.False(body.TryGetProperty("details", out _));
    }

    [Fact]
    public async Task PatientToken_OnAdminEndpoint_ReturnsUniform403()
    {
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreatePatientToken(Guid.NewGuid()));

        var date =
            DateOnly.FromDateTime(DateTime.UtcNow)
                .ToString("yyyy-MM-dd");

        using var response =
            await client.GetAsync(
                $"/api/appointments?date={date}");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);

        var body =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "AUTHORIZATION_FAILED",
            body.GetProperty("errorCode").GetString());

        Assert.True(body.TryGetProperty("message", out _));
        Assert.False(body.TryGetProperty("details", out _));
    }

    [Fact]
    public async Task AdminLogin_SixthAttempt_ReturnsUniform429()
    {
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var response =
                await client.PostAsJsonAsync(
                    "/api/auth/admin/login",
                    new
                    {
                        Email = "admin-test@example.com",
                        Password = "Password123"
                    });

            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                response.StatusCode);
        }

        using var rejectedResponse =
            await client.PostAsJsonAsync(
                "/api/auth/admin/login",
                new
                {
                    Email = "admin-test@example.com",
                    Password = "Password123"
                });

        await AssertRateLimitExceeded(rejectedResponse);
    }

    [Fact]
    public async Task PatientLogin_EleventhAttempt_ReturnsUniform429()
    {
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            using var response =
                await client.PostAsJsonAsync(
                    "/api/auth/patient/login",
                    new
                    {
                        Dni = 12345678,
                        Password = "Password123"
                    });

            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                response.StatusCode);
        }

        using var rejectedResponse =
            await client.PostAsJsonAsync(
                "/api/auth/patient/login",
                new
                {
                    Dni = 12345678,
                    Password = "Password123"
                });

        await AssertRateLimitExceeded(rejectedResponse);
    }

    [Fact]
    public async Task AppointmentBooking_SixthAttempt_ReturnsUniform429()
    {
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        var patientUserId = Guid.NewGuid();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreatePatientToken(patientUserId));

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var response =
                await client.PostAsJsonAsync(
                    "/api/appointments",
                    new { });

            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                response.StatusCode);
        }

        using var rejectedResponse =
            await client.PostAsJsonAsync(
                "/api/appointments",
                new { });

        await AssertRateLimitExceeded(rejectedResponse);
    }

    [Fact]
    public async Task GeneralLimiter_OneHundredFirstRequest_ReturnsUniform429()
    {
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        for (var attempt = 1; attempt <= 100; attempt++)
        {
            using var response =
                await client.GetAsync("/health-check");

            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                response.StatusCode);
        }

        using var rejectedResponse =
            await client.GetAsync("/health-check");

        await AssertRateLimitExceeded(rejectedResponse);
    }

    private static string CreatePatientToken(
        Guid patientUserId)
    {
        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                patientUserId.ToString()),

            new Claim(
                ClaimTypes.Role,
                Roles.Patient)
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

    private static async Task AssertRateLimitExceeded(
        HttpResponseMessage response)
    {
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            response.StatusCode);

        var body =
            await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "RATE_LIMIT_EXCEEDED",
            body.GetProperty("errorCode").GetString());

        Assert.True(body.TryGetProperty("message", out _));
        Assert.False(body.TryGetProperty("details", out _));
    }
}
