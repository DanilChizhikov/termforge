namespace DTech.Core.CommandHistory;

public sealed class CommandCaptureSession
{
    private readonly Guid _connectionId;
    private readonly ICommandHistoryRecorder _recorder;
    private readonly TimeProvider _timeProvider;
    private readonly ShellIntegrationParser _parser;
    private PendingCommand? _pending;

    public CommandCaptureSession(CommandCaptureTarget target, ICommandHistoryRecorder recorder, TimeProvider timeProvider)
    {
        _connectionId = target.ConnectionId;
        _recorder = recorder;
        _timeProvider = timeProvider;
        _parser = new ShellIntegrationParser(target.Nonce);
    }

    public async ValueTask<int> ObserveAsync(ReadOnlyMemory<byte> output, CancellationToken cancellationToken)
    {
        var events = _parser.Feed(output.Span);
        var recorded = 0;
        for (var i = 0; i < events.Count; i++)
        {
            var saved = await ApplyAsync(events[i], cancellationToken);
            recorded += saved ? 1 : 0;
        }

        return recorded;
    }

    public async ValueTask<int> CompleteAsync(CancellationToken cancellationToken)
    {
        var saved = await FlushPendingAsync(null, cancellationToken);
        return saved ? 1 : 0;
    }

    private async Task<bool> ApplyAsync(ShellIntegrationEvent shellEvent, CancellationToken cancellationToken)
    {
        if (shellEvent.Kind == ShellIntegrationEventKind.CommandFinished)
        {
            return await FlushPendingAsync(shellEvent.ExitCode, cancellationToken);
        }

        var flushed = await FlushPendingAsync(null, cancellationToken);
        var executedAt = _timeProvider.GetUtcNow().UtcDateTime;
        _pending = new PendingCommand(shellEvent.CommandLine ?? string.Empty, executedAt);
        return flushed;
    }

    private async Task<bool> FlushPendingAsync(int? exitCode, CancellationToken cancellationToken)
    {
        var pending = _pending;
        _pending = null;
        if (pending is null)
        {
            return false;
        }

        var command = new RecordedCommand(_connectionId, pending.Text, pending.ExecutedAt, exitCode);
        return await _recorder.RecordAsync(command, cancellationToken);
    }

    private sealed record PendingCommand(string Text, DateTime ExecutedAt);
}
