using Dsw2026Tpi.Application.Services;

namespace Dsw2026Tpi.Tests.Services;

public class TokenRevocationServiceTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Revoke_WithActiveToken_MarksItAsRevoked()
    {
        var clock = new MutableTimeProvider(InitialTime);
        var service = new TokenRevocationService(clock);

        service.Revoke("active", InitialTime.AddMinutes(5));

        Assert.True(service.IsRevoked("active"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Revoke_WithExpiredToken_DoesNotRevokeIt(
        int expirationOffsetSeconds)
    {
        var clock = new MutableTimeProvider(InitialTime);
        var service = new TokenRevocationService(clock);

        service.Revoke(
            "expired",
            InitialTime.AddSeconds(expirationOffsetSeconds));

        Assert.False(service.IsRevoked("expired"));
    }

    [Fact]
    public void IsRevoked_WithUnknownToken_ReturnsFalse()
    {
        var clock = new MutableTimeProvider(InitialTime);
        var service = new TokenRevocationService(clock);

        Assert.False(service.IsRevoked("unknown"));
    }

    [Fact]
    public void IsRevoked_AtExpiration_ReturnsFalse()
    {
        var clock = new MutableTimeProvider(InitialTime);
        var service = new TokenRevocationService(clock);
        var expiration = InitialTime.AddMinutes(5);

        service.Revoke("token", expiration);

        clock.UtcNow = expiration.AddTicks(-1);
        Assert.True(service.IsRevoked("token"));

        clock.UtcNow = expiration;
        Assert.False(service.IsRevoked("token"));

        clock.UtcNow = expiration.AddMinutes(1);
        Assert.False(service.IsRevoked("token"));
    }

    [Fact]
    public void Revoke_WithEarlierExpiration_PreservesLongerRevocation()
    {
        var clock = new MutableTimeProvider(InitialTime);
        var service = new TokenRevocationService(clock);

        service.Revoke("token", InitialTime.AddMinutes(10));
        service.Revoke("token", InitialTime.AddMinutes(1));

        clock.UtcNow = InitialTime.AddMinutes(2);

        Assert.True(service.IsRevoked("token"));
    }

    [Fact]
    public void NewInstance_DoesNotPreservePreviousRevocations()
    {
        var clock = new MutableTimeProvider(InitialTime);
        var firstInstance = new TokenRevocationService(clock);

        firstInstance.Revoke("token", InitialTime.AddMinutes(5));

        var secondInstance = new TokenRevocationService(clock);

        Assert.True(firstInstance.IsRevoked("token"));
        Assert.False(secondInstance.IsRevoked("token"));
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public MutableTimeProvider(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; set; }

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
