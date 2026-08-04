namespace Dsw2026Tpi.Application.Interfaces;

public interface ITokenRevocationService
{
    void Revoke(string tokenId, DateTimeOffset expiresAt);

    bool IsRevoked(string tokenId);
}
