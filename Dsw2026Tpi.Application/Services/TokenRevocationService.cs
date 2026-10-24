using System.Collections.Concurrent;
using Dsw2026Tpi.Application.Interfaces;

namespace Dsw2026Tpi.Application.Services;

public sealed class TokenRevocationService : ITokenRevocationService
{
    private readonly TimeProvider _timeProvider;

    private readonly ConcurrentDictionary<string, DateTimeOffset>
        _revokedTokens = new(StringComparer.Ordinal);

    public TokenRevocationService(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public void Revoke(string tokenId, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        var now = _timeProvider.GetUtcNow();
        RemoveExpired(now);

        if (expiresAt <= now)
        {
            return;
        }

        // Revocar nuevamente no debe acortar una revocación existente.
        _revokedTokens.AddOrUpdate(
            tokenId,
            expiresAt,
            (_, currentExpiration) =>
                currentExpiration >= expiresAt
                    ? currentExpiration
                    : expiresAt);
    }

    public bool IsRevoked(string tokenId)
    {
        var now = _timeProvider.GetUtcNow();
        RemoveExpired(now);

        return !string.IsNullOrWhiteSpace(tokenId)
            && _revokedTokens.TryGetValue(tokenId, out var expiresAt)
            && expiresAt > now;
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var entry in _revokedTokens)
        {
            if (entry.Value <= now)
            {
                // Elimina solo si la entrada conserva el mismo valor.
                // Otra solicitud podría haber extendido la revocación.
                ((ICollection<KeyValuePair<string, DateTimeOffset>>)
                    _revokedTokens).Remove(entry);
            }
        }
    }
}
