using Dsw2026Tpi.Application.Interfaces;
using System.Collections.Concurrent;

namespace Dsw2026Tpi.Application.Services;

public sealed class TokenRevocationService : ITokenRevocationService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset>
        _revokedTokens = new(StringComparer.Ordinal);

    public void Revoke(
        string tokenId,
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            return;
        }

        _revokedTokens[tokenId] = expiresAt;
    }

    public bool IsRevoked(string tokenId)
    {
        if (string.IsNullOrWhiteSpace(tokenId) ||
            !_revokedTokens.TryGetValue(
                tokenId,
                out var expiresAt))
        {
            return false;
        }

        if (expiresAt > DateTimeOffset.UtcNow)
        {
            return true;
        }

        _revokedTokens.TryRemove(
            tokenId,
            out _);

        return false;
    }
}
