namespace DTech.Entrypoint.Tests.Infrastructure;

internal sealed class MovableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    public void Advance(TimeSpan duration)
    {
        _now += duration;
    }
}
