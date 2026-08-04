using Dsw2026Tpi.Application.Services;

namespace Dsw2026Tpi.Tests.Services;

public class TokenRevocationServiceTests
{
    [Fact]
    public void Revoke_WithActiveToken_MarksItAsRevoked()
    {
        var service = new TokenRevocationService();
        var tokenId = Guid.NewGuid().ToString();

        service.Revoke(
            tokenId,
            DateTimeOffset.UtcNow.AddMinutes(5));

        Assert.True(service.IsRevoked(tokenId));
    }

    [Fact]
    public void Revoke_WithExpiredToken_DoesNotStoreIt()
    {
        var service = new TokenRevocationService();
        var tokenId = Guid.NewGuid().ToString();

        service.Revoke(
            tokenId,
            DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.False(service.IsRevoked(tokenId));
    }

    [Fact]
    public void IsRevoked_WithUnknownToken_ReturnsFalse()
    {
        var service = new TokenRevocationService();

        Assert.False(
            service.IsRevoked(Guid.NewGuid().ToString()));
    }
}
