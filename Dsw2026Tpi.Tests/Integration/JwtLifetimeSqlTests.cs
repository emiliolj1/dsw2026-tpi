using System.Net;
using System.Net.Http.Headers;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Tests.Integration;

[Trait("Category", "SqlServer")]
public sealed class JwtLifetimeSqlTests
    : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public JwtLifetimeSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(-1, HttpStatusCode.Unauthorized)]
    [InlineData(60, HttpStatusCode.OK)]
    public async Task ApiRespectsTokenExpirationWithoutExtraTolerance(
        int expirationOffsetSeconds,
        HttpStatusCode expectedStatus)
    {
        await using var scope =
            _fixture.Factory.Services.CreateAsyncScope();

        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<JwtOptions>>();

        var clock = scope.ServiceProvider
            .GetRequiredService<TimeProvider>();

        var now = clock.GetUtcNow();

        // La emisión se desplaza para producir un token vencido
        // hace un segundo o uno válido durante sesenta segundos.
        var issuedAt = now
            .AddMinutes(-options.Value.ExpiresInMinutes)
            .AddSeconds(expirationOffsetSeconds);

        var tokenService = new JwtService(
            options,
            new FixedTimeProvider(issuedAt));

        await using var identity =
            _fixture.CreateAuthenticationContext();

        var admin = await identity.Users
            .AsNoTracking()
            .SingleAsync(user =>
                user.NormalizedEmail ==
                    SqlServerWebApplicationFactory.AdminEmail
                        .ToUpperInvariant());

        var token = tokenService.GenerateToken(
            admin,
            Roles.Administrator);

        using var request =
            new HttpRequestMessage(HttpMethod.Get, "/health-check");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await _fixture.Client.SendAsync(request);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
