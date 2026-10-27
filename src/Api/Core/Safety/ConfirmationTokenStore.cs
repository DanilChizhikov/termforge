using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace DTech.Core.Safety;

public sealed class ConfirmationTokenStore(TimeProvider timeProvider) : IConfirmationTokenStore
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private const int TokenSizeInBytes = 32;

    private readonly ConcurrentDictionary<string, PendingConfirmation> _pending = new(StringComparer.Ordinal);

    public ConfirmationGrant Issue(ConfirmationScope scope)
    {
        var now = timeProvider.GetUtcNow();
        RemoveExpired(now);
        var tokenBytes = RandomNumberGenerator.GetBytes(TokenSizeInBytes);
        var token = Convert.ToHexStringLower(tokenBytes);
        var expiresAt = now + Lifetime;
        var pending = new PendingConfirmation(scope, expiresAt);
        _pending[token] = pending;
        return new ConfirmationGrant(token, expiresAt);
    }

    public bool TryConsume(ConfirmationScope scope, string? token)
    {
        if (string.IsNullOrEmpty(token) || !_pending.TryRemove(token, out var pending))
        {
            return false;
        }

        var now = timeProvider.GetUtcNow();
        return pending.Scope == scope && now < pending.ExpiresAt;
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var entry in _pending)
        {
            if (entry.Value.ExpiresAt <= now)
            {
                _pending.TryRemove(entry.Key, out _);
            }
        }
    }

    private sealed record PendingConfirmation(ConfirmationScope Scope, DateTimeOffset ExpiresAt);
}
