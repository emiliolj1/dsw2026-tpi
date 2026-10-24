using System.IdentityModel.Tokens.Jwt;
using Dsw2026Tpi.Application.Options;
using Dsw2026Tpi.Application.Services;
using Dsw2026Tpi.CrossCutting.Identity;
using Dsw2026Tpi.Data.Identity;
using Microsoft.Extensions.Options;

namespace Dsw2026Tpi.Tests.Services;

public class JwtServiceTests
{
    [Theory]
    [InlineData("empty-key", "Jwt:Key")]
    [InlineData("short-key", "Jwt:Key")]
    [InlineData("issuer", "Jwt:Issuer")]
    [InlineData("audience", "Jwt:Audience")]
    [InlineData("zero-expiration", "Jwt:ExpiresInMinutes")]
    [InlineData("negative-expiration", "Jwt:ExpiresInMinutes")]
    public void InvalidConfigurationIsRejected(
        string scenario,
        string expectedField)
    {
        var options = CreateOptions();

        switch (scenario)
        {
            case "empty-key":
                options.Key = "";
                break;
            case "short-key":
                options.Key = "short";
                break;
            case "issuer":
                options.Issuer = " ";
                break;
            case "audience":
                options.Audience = "";
                break;
            case "zero-expiration":
                options.ExpiresInMinutes = 0;
                break;
            case "negative-expiration":
                options.ExpiresInMinutes = -1;
                break;
        }

        var result = new JwtOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures!,
            failure => failure.Contains(expectedField));
    }

    [Fact]
    public void ValidConfigurationIsAccepted()
    {
        var result = new JwtOptionsValidator()
            .Validate(null, CreateOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void TokenUsesConfiguredLifetimeAndInjectedClock()
    {
        var now = new DateTimeOffset(
            2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

        var options = CreateOptions();
        options.ExpiresInMinutes = 15;

        var service = new JwtService(
            Options.Create(options),
            new FixedTimeProvider(now));

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "jwt-test@example.test"
        };

        var first = new JwtSecurityTokenHandler().ReadJwtToken(
            service.GenerateToken(user, Roles.Patient));

        var second = new JwtSecurityTokenHandler().ReadJwtToken(
            service.GenerateToken(user, Roles.Patient));

        Assert.Equal(options.Issuer, first.Issuer);
        Assert.Equal(new[] { options.Audience }, first.Audiences);
        Assert.Equal(now.UtcDateTime, first.ValidFrom);
        Assert.Equal(now.AddMinutes(15).UtcDateTime, first.ValidTo);
        Assert.Equal(
            user.Id.ToString(),
            first.Claims.Single(
                claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);

        Assert.NotEqual(first.Id, second.Id);
    }

    private static JwtOptions CreateOptions()
    {
        return new JwtOptions
        {
            Key = "ClaveExclusivaJwtTests_ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            Issuer = "JwtTests",
            Audience = "JwtTests",
            ExpiresInMinutes = 60
        };
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
