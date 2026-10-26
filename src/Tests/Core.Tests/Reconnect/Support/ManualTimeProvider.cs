namespace DTech.Core.Tests.Reconnect.Support;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];
    private readonly Lock _gate = new();
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public int PendingTimerCount
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count(timer => !timer.IsDisposed);
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, GetUtcNow() + dueTime);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    public void Advance(TimeSpan duration)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _now += duration;
            due = _timers.Where(timer => !timer.IsDisposed && timer.DueAt <= _now).ToList();
        }

        for (var i = 0; i < due.Count; i++)
        {
            due[i].Fire();
        }
    }

    public async Task WaitForPendingTimerAsync()
    {
        while (PendingTimerCount == 0)
        {
            await Task.Delay(5);
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset dueAt) : ITimer
    {
        public DateTimeOffset DueAt { get; } = dueAt;

        public bool IsDisposed { get; private set; }

        public void Fire()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            return false;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
