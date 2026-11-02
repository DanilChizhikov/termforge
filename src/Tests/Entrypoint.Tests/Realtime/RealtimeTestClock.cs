namespace DTech.Entrypoint.Tests.Realtime;

internal sealed class RealtimeTestClock : TimeProvider
{
    private readonly TaskCompletionSource _timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TimerCallback? _callback;
    private object? _state;

    public Task TimerCreated => _timerCreated.Task;
    public TimeSpan DueTime { get; private set; }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        _callback = callback;
        _state = state;
        DueTime = dueTime;
        _timerCreated.TrySetResult();
        return new Timer(callback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Expire()
    {
        _callback!(_state);
    }
}
