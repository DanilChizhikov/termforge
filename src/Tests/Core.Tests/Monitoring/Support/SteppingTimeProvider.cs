namespace DTech.Core.Tests.Monitoring.Support;

internal sealed class SteppingTimeProvider : TimeProvider
{
    private readonly long _stepTicks;
    private long _timestamp;

    public SteppingTimeProvider(TimeSpan step)
    {
        _stepTicks = (long)(step.TotalSeconds * TimestampFrequency);
    }

    public override long GetTimestamp()
    {
        var current = _timestamp;
        _timestamp += _stepTicks;
        return current;
    }
}
