namespace DTech.Core.Tests.Editor;

internal sealed class SteppingTimeProvider(DateTimeOffset start) : TimeProvider
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow()
    {
        _now += Step;
        return _now;
    }
}
