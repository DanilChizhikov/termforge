using System.Security.Cryptography;
using System.Text;

namespace DTech.Entrypoint.Security;

internal sealed class BrowserSessionStore(TimeProvider timeProvider)
{
    public const int MaximumSessionCount = 32;

    private readonly Lock _sessionsLock = new();
    private readonly Dictionary<string, BrowserSession> _sessions = new(StringComparer.Ordinal);

    public BrowserSession Create(BrowserSessionKind kind, TimeSpan? lifetime)
    {
        var now = timeProvider.GetUtcNow();
        var sessionToken = RandomSecret.Create();
        var csrfToken = RandomSecret.Create();
        var session = new BrowserSession(sessionToken, csrfToken)
        {
            Kind = kind,
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };
        var sessionKey = CreateKey(sessionToken);
        lock (_sessionsLock)
        {
            RemoveExpiredSessions(now);
            while (_sessions.Count >= MaximumSessionCount)
            {
                RemoveOldestSession();
            }

            _sessions[sessionKey] = session;
        }

        return session;
    }

    public BrowserSession? Find(string? sessionToken)
    {
        if (string.IsNullOrEmpty(sessionToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var sessionKey = CreateKey(sessionToken);
        lock (_sessionsLock)
        {
            if (!_sessions.TryGetValue(sessionKey, out var session))
            {
                return null;
            }

            if (session.IsExpiredAt(now))
            {
                _sessions.Remove(sessionKey);
                return null;
            }

            return SecretComparer.AreEqual(session.SessionToken, sessionToken) ? session : null;
        }
    }

    public void Remove(string sessionToken)
    {
        var sessionKey = CreateKey(sessionToken);
        lock (_sessionsLock)
        {
            _sessions.Remove(sessionKey);
        }
    }

    private static string CreateKey(string sessionToken)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(sessionToken);
        var tokenHash = SHA256.HashData(tokenBytes);
        return Convert.ToBase64String(tokenHash);
    }

    private void RemoveExpiredSessions(DateTimeOffset now)
    {
        var expiredKeys = new List<string>();
        foreach (var entry in _sessions)
        {
            if (entry.Value.IsExpiredAt(now))
            {
                expiredKeys.Add(entry.Key);
            }
        }

        for (var i = 0; i < expiredKeys.Count; i++)
        {
            _sessions.Remove(expiredKeys[i]);
        }
    }

    private void RemoveOldestSession()
    {
        string? oldestKey = null;
        var oldestCreatedAt = DateTimeOffset.MaxValue;
        foreach (var entry in _sessions)
        {
            if (entry.Value.CreatedAt < oldestCreatedAt)
            {
                oldestKey = entry.Key;
                oldestCreatedAt = entry.Value.CreatedAt;
            }
        }

        if (oldestKey is not null)
        {
            _sessions.Remove(oldestKey);
        }
    }
}
