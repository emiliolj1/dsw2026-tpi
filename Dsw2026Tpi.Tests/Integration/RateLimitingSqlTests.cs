using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dsw2026Tpi.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class RateLimitingSqlTests
    : IClassFixture<SqlServerFixture>
{
    private const string TestIpHeader = "X-Test-Client-IP";
    private const string FirstIp = "192.0.2.10";
    private const string SecondIp = "192.0.2.20";

    private static long _nextDni = 78_000_000;

    private readonly SqlServerFixture _fixture;

    public RateLimitingSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("admin", 5)]
    [InlineData("patient", 10)]
    public async Task LoginLimitIsIsolatedByIp(
        string scenario,
        int permittedRequests)
    {
        using var factory = CreateIsolatedFactory();
        using var client = factory.CreateClient();

        var path = scenario == "admin"
            ? "/api/auth/admin/login"
            : "/api/auth/patient/login";

        object payload = scenario == "admin"
            ? new
            {
                email = $"absent-{Guid.NewGuid():N}@example.test",
                password = "IncorrectPassword2026!"
            }
            : new
            {
                email = "invalid-email",
                dni = 0L
            };

        var expectedBeforeLimit = scenario == "admin"
            ? HttpStatusCode.Unauthorized
            : HttpStatusCode.BadRequest;

        for (var attempt = 0; attempt < permittedRequests; attempt++)
        {
            using var response = await Send(
                client, HttpMethod.Post, path, payload);

            Assert.Equal(expectedBeforeLimit, response.StatusCode);
        }

        using var rejected = await Send(
            client, HttpMethod.Post, path, payload);

        await AssertRateLimit(rejected);

        using var otherIp = await Send(
            client,
            HttpMethod.Post,
            path,
            payload,
            ip: SecondIp);

        Assert.Equal(expectedBeforeLimit, otherIp.StatusCode);
    }

    [Theory]
    [InlineData("booking", 5)]
    [InlineData("general", 100)]
    public async Task AuthenticatedLimitIsIsolatedByUser(
        string scenario,
        int permittedRequests)
    {
        using var factory = CreateIsolatedFactory();
        using var client = factory.CreateClient();

        var firstToken = await LoginPatient(client);
        var secondToken = await LoginPatient(client);

        var method = scenario == "booking"
            ? HttpMethod.Post
            : HttpMethod.Get;

        var path = scenario == "booking"
            ? "/api/appointments"
            : "/health-check";

        object? payload = scenario == "booking" ? new { } : null;

        var expectedBeforeLimit = scenario == "booking"
            ? HttpStatusCode.BadRequest
            : HttpStatusCode.OK;

        for (var attempt = 0; attempt < permittedRequests; attempt++)
        {
            using var response = await Send(
                client, method, path, payload, firstToken);

            Assert.Equal(expectedBeforeLimit, response.StatusCode);
        }

        using var rejected = await Send(
            client, method, path, payload, firstToken);

        await AssertRateLimit(rejected);

        // Mismo origen IP, pero otro usuario: conserva su cupo.
        using var otherUser = await Send(
            client, method, path, payload, secondToken);

        Assert.Equal(expectedBeforeLimit, otherUser.StatusCode);
    }

    [Fact]
    public async Task AnonymousGeneralLimitIsIsolatedByIp()
    {
        using var factory = CreateIsolatedFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 100; attempt++)
        {
            using var response = await Send(
                client, HttpMethod.Get, "/health-check");

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                response.StatusCode);
        }

        using var rejected = await Send(
            client, HttpMethod.Get, "/health-check");

        await AssertRateLimit(rejected);

        using var otherIp = await Send(
            client,
            HttpMethod.Get,
            "/health-check",
            ip: SecondIp);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            otherIp.StatusCode);
    }

    private WebApplicationFactory<Program> CreateIsolatedFactory()
    {
        // Cada caso obtiene limitadores nuevos, sobre la base SQL
        // exclusiva del fixture.
        return _fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter>(
                    new TestClientIpStartupFilter());
            });
        });
    }

    private static async Task<string> LoginPatient(HttpClient client)
    {
        using var response = await Send(
            client,
            HttpMethod.Post,
            "/api/auth/patient/login",
            new
            {
                email = $"rate-{Guid.NewGuid():N}@example.test",
                dni = Interlocked.Increment(ref _nextDni)
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        var token = body.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        return token!;
    }

    private static async Task<HttpResponseMessage> Send(
        HttpClient client,
        HttpMethod method,
        string path,
        object? payload = null,
        string? token = null,
        string ip = FirstIp)
    {
        using var request = new HttpRequestMessage(method, path);

        request.Headers.Add(TestIpHeader, ip);

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        if (token is not null)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return await client.SendAsync(request);
    }

    private static async Task AssertRateLimit(
        HttpResponseMessage response)
    {
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            response.StatusCode);

        Assert.Equal(
            "application/json",
            response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "RATE_LIMIT_EXCEEDED",
            body.GetProperty("errorCode").GetString());

        Assert.False(string.IsNullOrWhiteSpace(
            body.GetProperty("message").GetString()));
    }

    // Este encabezado artificial se interpreta únicamente en el host
    // de pruebas. No se agrega este comportamiento a la API.
    private sealed class TestClientIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(
            Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (
                    HttpContext context,
                    RequestDelegate nextMiddleware) =>
                {
                    if (IPAddress.TryParse(
                        context.Request.Headers[TestIpHeader].ToString(),
                        out var address))
                    {
                        context.Connection.RemoteIpAddress = address;
                    }

                    await nextMiddleware(context);
                });

                next(app);
            };
        }
    }
}
