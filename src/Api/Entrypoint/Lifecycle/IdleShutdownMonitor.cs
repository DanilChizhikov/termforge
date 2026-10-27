namespace DTech.Entrypoint.Lifecycle;

internal sealed class IdleShutdownMonitor(TimeSpan idleTimeout)
{
    private DateTimeOffset? _idleSince;

    public bool ShouldShutdown(bool isIdle, DateTimeOffset now)
    {
        if (!isIdle)
        {
            _idleSince = null;
            return false;
        }

        _idleSince ??= now;
        var idleDuration = now - _idleSince.Value;
        return idleDuration >= idleTimeout;
    }
}
