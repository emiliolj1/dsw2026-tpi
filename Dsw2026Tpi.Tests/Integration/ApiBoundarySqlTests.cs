using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class ApiBoundarySqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public ApiBoundarySqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(
        "invalid-json",
        HttpStatusCode.BadRequest,
        "VALIDATION_ERROR")]
    [InlineData(
        "unauthenticated",
        HttpStatusCode.Unauthorized,
        "AUTHENTICATION_FAILED")]
    [InlineData(
        "forbidden",
        HttpStatusCode.Forbidden,
        "AUTHORIZATION_FAILED")]
    [InlineData(
        "missing-entity",
        HttpStatusCode.NotFound,
        "ENTITY_NOTFOUND")]
    [InlineData(
        "missing-route",
        HttpStatusCode.NotFound,
        "ROUTE_NOT_FOUND")]
    [InlineData(
        "wrong-method",
        HttpStatusCode.MethodNotAllowed,
        "METHOD_NOT_ALLOWED")]
    [InlineData(
        "unsupported-media-type",
        HttpStatusCode.UnsupportedMediaType,
        "UNSUPPORTED_MEDIA_TYPE")]
    public async Task HttpErrorsUseTheCommonContract(
        string scenario,
        HttpStatusCode expectedStatus,
        string expectedErrorCode)
    {
        using var request = CreateRequest(scenario);

        request.Headers.Add("Origin", "http://localhost:5173");

        if (scenario != "unauthenticated")
        {
            var token = scenario == "forbidden"
                ? await LoginPatient()
                : await LoginAdministrator();

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(expectedStatus, response.StatusCode);

        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            expectedErrorCode,
            body.GetProperty("errorCode").GetString());

        Assert.False(string.IsNullOrWhiteSpace(
            body.GetProperty("message").GetString()));

        // Los errores también deben poder ser leídos por React.
        Assert.Equal(
            "http://localhost:5173",
            Assert.Single(response.Headers.GetValues(
                "Access-Control-Allow-Origin")));
    }

    private static HttpRequestMessage CreateRequest(string scenario)
    {
        return scenario switch
        {
            "invalid-json" => new HttpRequestMessage(
                HttpMethod.Post,
                "/api/specialties")
            {
                Content = new StringContent(
                    "{",
                    Encoding.UTF8,
                    "application/json")
            },

            "unauthenticated" => new HttpRequestMessage(
                HttpMethod.Get,
                "/health-check"),

            "forbidden" => new HttpRequestMessage(
                HttpMethod.Get,
                "/api/appointments?date=2026-10-25"),

            "missing-entity" => new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/doctors/{Guid.NewGuid()}/availabilities"),

            "missing-route" => new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/nonexistent-{Guid.NewGuid():N}"),

            "wrong-method" => new HttpRequestMessage(
                HttpMethod.Get,
                "/api/auth/admin/login"),

            "unsupported-media-type" => new HttpRequestMessage(
                HttpMethod.Post,
                "/api/specialties")
            {
                Content = new StringContent(
                    "contenido de prueba",
                    Encoding.UTF8,
                    "text/plain")
            },

            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
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

        return await ReadToken(response);
    }

    private async Task<string> LoginPatient()
    {
        using var response = await _fixture.Client.PostAsJsonAsync(
            "/api/auth/patient/login",
            new
            {
                email = $"boundary-{Guid.NewGuid():N}@example.test",
                dni = 77_000_001L
            });

        return await ReadToken(response);
    }

    private static async Task<string> ReadToken(
        HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        var token = body.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        return token!;
    }
}
