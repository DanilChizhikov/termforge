using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Security.Network;

internal sealed class LoginAttemptLimiter(IOptions<NetworkAccessOptions> options, TimeProvider timeProvider)
{
    private const int MaximumTrackedClients = 1024;

    private readonly Lock _attemptsLock = new();
    private readonly Dictionary<string, AttemptState> _clientAttempts = new(StringComparer.Ordinal);
    private readonly AttemptState _globalAttempts = new();

    public LoginAttemptDecision TryBegin(string clientKey)
    {
        var networkAccess = options.Value;
        var lockoutMinutes = networkAccess.LockoutMinutes;
        var lockoutDuration = TimeSpan.FromMinutes(lockoutMinutes);
        var now = timeProvider.GetUtcNow();
        lock (_attemptsLock)
        {
            var clientAttempts = GetClientAttempts(clientKey, now, lockoutDuration);
            var lockedUntil = Max(clientAttempts.LockedUntil, _globalAttempts.LockedUntil);
            if (lockedUntil > now)
            {
                var retryAfter = lockedUntil - now;
                return new LoginAttemptDecision(false, retryAfter);
            }

            var clientWindow = new AttemptWindow(now, lockoutDuration, networkAccess.MaxFailedLoginAttempts);
            clientAttempts.Register(clientWindow);
            var globalWindow = new AttemptWindow(now, lockoutDuration, networkAccess.GlobalMaxFailedLoginAttempts);
            _globalAttempts.Register(globalWindow);
            return LoginAttemptDecision.Allowed;
        }
    }

    public void RecordSuccess(string clientKey)
    {
        lock (_attemptsLock)
        {
            _clientAttempts.Remove(clientKey);
            _globalAttempts.Reset();
        }
    }

    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second)
    {
        return first > second ? first : second;
    }

    private AttemptState GetClientAttempts(string clientKey, DateTimeOffset now, TimeSpan lockoutDuration)
    {
        if (_clientAttempts.TryGetValue(clientKey, out var existingAttempts))
        {
            return existingAttempts;
        }

        if (_clientAttempts.Count >= MaximumTrackedClients)
        {
            RemoveStaleClients(now, lockoutDuration);
        }

        var clientAttempts = new AttemptState();
        _clientAttempts[clientKey] = clientAttempts;
        return clientAttempts;
    }

    private void RemoveStaleClients(DateTimeOffset now, TimeSpan lockoutDuration)
    {
        var staleKeys = new List<string>();
        foreach (var entry in _clientAttempts)
        {
            if (entry.Value.IsStaleAt(now, lockoutDuration))
            {
                staleKeys.Add(entry.Key);
            }
        }

        for (var i = 0; i < staleKeys.Count; i++)
        {
            _clientAttempts.Remove(staleKeys[i]);
        }
    }

    private readonly record struct AttemptWindow(DateTimeOffset Now, TimeSpan LockoutDuration, int MaximumAttempts);

    private sealed class AttemptState
    {
        private int _attemptCount;
        private DateTimeOffset _lastAttemptAt = DateTimeOffset.MinValue;

        public DateTimeOffset LockedUntil { get; private set; } = DateTimeOffset.MinValue;

        public void Register(AttemptWindow window)
        {
            if (IsStaleAt(window.Now, window.LockoutDuration))
            {
                _attemptCount = 0;
            }

            _attemptCount++;
            _lastAttemptAt = window.Now;
            if (_attemptCount >= window.MaximumAttempts)
            {
                LockedUntil = window.Now + window.LockoutDuration;
                _attemptCount = 0;
            }
        }

        public bool IsStaleAt(DateTimeOffset now, TimeSpan lockoutDuration)
        {
            return LockedUntil <= now && _lastAttemptAt + lockoutDuration <= now;
        }

        public void Reset()
        {
            _attemptCount = 0;
            LockedUntil = DateTimeOffset.MinValue;
        }
    }
}
