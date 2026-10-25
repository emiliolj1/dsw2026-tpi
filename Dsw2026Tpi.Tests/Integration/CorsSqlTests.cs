using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dsw2026Tpi.Application.Dtos;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class CorsSqlTests : IClassFixture<SqlServerFixture>
{
    private const string AllowedOrigin = "http://localhost:5173";
    private const string DeniedOrigin = "https://untrusted.example.test";

    private readonly SqlServerFixture _fixture;

    public CorsSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AllowedPreflightPermitsAuthorizationAndJson()
    {
        using var request = CreatePreflight(AllowedOrigin);
        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(
            AllowedOrigin,
            Assert.Single(response.Headers.GetValues(
                "Access-Control-Allow-Origin")));

        Assert.Contains(
            "POST",
            HeaderItems(response, "Access-Control-Allow-Methods"));

        var headers = HeaderItems(
                response,
                "Access-Control-Allow-Headers")
            .Select(header => header.ToLowerInvariant())
            .ToArray();

        Assert.Contains("authorization", headers);
        Assert.Contains("content-type", headers);
    }

    [Fact]
    public async Task DeniedPreflightDoesNotGrantCorsPermission()
    {
        using var request = CreatePreflight(DeniedOrigin);
        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.False(response.Headers.Contains(
            "Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData(AllowedOrigin, true)]
    [InlineData(DeniedOrigin, false)]
    public async Task AuthenticatedResponseGrantsCorsOnlyToAllowedOrigin(
        string origin,
        bool shouldAllowOrigin)
    {
        var token = await LoginAdministrator();

        using var request =
            new HttpRequestMessage(HttpMethod.Get, "/health-check");

        request.Headers.Add("Origin", origin);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        if (shouldAllowOrigin)
        {
            Assert.Equal(
                AllowedOrigin,
                Assert.Single(response.Headers.GetValues(
                    "Access-Control-Allow-Origin")));
        }
        else
        {
            Assert.False(response.Headers.Contains(
                "Access-Control-Allow-Origin"));
        }
    }

    private static HttpRequestMessage CreatePreflight(string origin)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/appointments");

        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add(
            "Access-Control-Request-Headers",
            "authorization,content-type");

        return request;
    }

    private async Task<string> LoginAdministrator()
    {
        using var response = await _fixture.Client.PostAsJsonAsync(
            "/api/auth/admin/login",
            new
            {
                email = SqlServerWebApplicationFactory.AdminEmail,
                password = SqlServerWebApplicationFactory.AdminPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content
            .ReadFromJsonAsync<LoginAdminModel.Response>();

        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.Token));

        return login.Token!;
    }

    private static string[] HeaderItems(
        HttpResponseMessage response,
        string name)
    {
        Assert.True(response.Headers.TryGetValues(name, out var values));

        return values!
            .SelectMany(value => value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries))
            .ToArray();
    }
}
