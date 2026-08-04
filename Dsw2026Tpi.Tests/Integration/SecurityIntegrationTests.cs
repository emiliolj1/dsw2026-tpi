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

    [Theory]
    [InlineData("/api/specialties?pageSize=10&pageIndex=0")]
    [InlineData("/api/doctors?pageSize=10&pageIndex=0")]
    public async Task PatientToken_OnCatalogEndpoint_ReturnsOk(
        string endpoint)
    {
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreatePatientToken(Guid.NewGuid()));

        using var response =
            await client.GetAsync(endpoint);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Theory]
    [InlineData("/api/specialties?pageSize=10&pageIndex=0")]
    [InlineData("/api/doctors?pageSize=10&pageIndex=0")]
    public async Task AdminToken_OnCatalogEndpoint_ReturnsOk(
        string endpoint)
    {
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreateAdminToken(Guid.NewGuid()));

        using var response =
            await client.GetAsync(endpoint);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);
    }

    [Theory]
    [InlineData("/api/specialties?pageSize=10&pageIndex=0")]
    [InlineData("/api/doctors?pageSize=10&pageIndex=0")]
    public async Task CatalogEndpoint_WithoutToken_ReturnsUniform401(
        string endpoint)
    {
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        using var response =
            await client.GetAsync(endpoint);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        var body =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "AUTHENTICATION_FAILED",
            body.GetProperty("errorCode").GetString());

        Assert.True(body.TryGetProperty("message", out _));
        Assert.False(body.TryGetProperty("details", out _));
    }

    [Theory]
    [InlineData("POST", "/api/specialties")]
    [InlineData(
        "PUT",
        "/api/specialties/00000000-0000-0000-0000-000000000000")]
    [InlineData(
        "DELETE",
        "/api/specialties/00000000-0000-0000-0000-000000000000")]
    [InlineData("POST", "/api/doctors")]
    [InlineData(
        "PUT",
        "/api/doctors/00000000-0000-0000-0000-000000000000")]
    [InlineData(
        "DELETE",
        "/api/doctors/00000000-0000-0000-0000-000000000000")]
    [InlineData(
        "GET",
        "/api/doctors/00000000-0000-0000-0000-000000000000/availabilities")]
    public async Task PatientToken_OnAdministrativeEndpoint_ReturnsUniform403(
        string method,
        string endpoint)
    {
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                CreatePatientToken(Guid.NewGuid()));

        using var request =
            new HttpRequestMessage(
                new HttpMethod(method),
                endpoint);

        using var response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);

        var body =
            await response.Content
                .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "AUTHORIZATION_FAILED",
            body.GetProperty("errorCode").GetString());

        Assert.True(body.TryGetProperty("message", out _));
        Assert.False(body.TryGetProperty("details", out _));
    }

    [Theory]
    [InlineData("email-invalido", "Password123")]
    [InlineData("admin-test@example.com", "corta")]
    [InlineData("admin-test@example.com", "")]
    public async Task AdminLogin_WithInvalidFormat_Returns400(
    string email,
    string password)
    {
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        using var response =
            await client.PostAsJsonAsync(
                "/api/auth/admin/login",
                new
                {
                    Email = email,
                    Password = password
                });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task AdminLogin_WithIncorrectCredentials_Returns401()
    {
        using var factory =
            new CustomWebApplicationFactory();

        using var client = factory.CreateClient();

        using var response =
            await client.PostAsJsonAsync(
                "/api/auth/admin/login",
                new
                {
                    Email = "admin-inexistente@example.com",
                    Password = "Password123"
                });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
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
        return CreateToken(
            patientUserId,
            Roles.Patient);
    }

    private static string CreateAdminToken(
        Guid adminUserId)
    {
        return CreateToken(
            adminUserId,
            Roles.Administrator);
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
