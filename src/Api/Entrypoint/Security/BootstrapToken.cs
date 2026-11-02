namespace DTech.Entrypoint.Security;

internal sealed class BootstrapToken(TimeProvider timeProvider)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);
    private readonly Lock _consumeLock = new();
    private string? _value;
    private DateTimeOffset _issuedAt;
    private bool _isConsumed;

    public event Action? Consumed;

    public TimeSpan RemainingLifetime
    {
        get
        {
            lock (_consumeLock)
            {
                var remaining = _issuedAt + Lifetime - timeProvider.GetUtcNow();
                return _value is not null && remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }
    }

    public string Value
    {
        get
        {
            lock (_consumeLock)
            {
                if (_value is null)
                {
                    _value = RandomSecret.Create();
                    _issuedAt = timeProvider.GetUtcNow();
                }

                return _value;
            }
        }
    }

    public bool TryConsume(string? candidate)
    {
        lock (_consumeLock)
        {
            if (_value is null || _isConsumed || timeProvider.GetUtcNow() - _issuedAt >= Lifetime)
            {
                return false;
            }

            if (!SecretComparer.AreEqual(_value, candidate))
            {
                return false;
            }

            _isConsumed = true;
        }

        Consumed?.Invoke();
        return true;
    }
}
