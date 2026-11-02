using DTech.Core.Terminal.Sessions;

namespace DTech.Core.Terminal.Recording;

public sealed class TerminalRecordingService
{
    private readonly TerminalRecordingSettingsService _settingsService;
    private readonly TerminalRecorderFactory _recorderFactory;
    private readonly TerminalRecordingRegistry _registry;
    private readonly TerminalRecordingRetention _retention;
    private readonly TimeProvider _timeProvider;

    public TerminalRecordingService(TerminalRecordingSettingsService settingsService, TerminalRecorderFactory recorderFactory, TerminalRecordingRegistry registry, TerminalRecordingRetention retention, TimeProvider timeProvider)
    {
        _settingsService = settingsService;
        _recorderFactory = recorderFactory;
        _registry = registry;
        _retention = retention;
        _timeProvider = timeProvider;
    }

    public TerminalRecorder? FindLatest(ManagedTerminalSession session)
    {
        return _registry.FindLatest(session.Descriptor.Id);
    }

    public async Task<TerminalRecordingResult<TerminalRecorder>> StartAsync(ManagedTerminalSession session, CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetAsync(cancellationToken);
        if (!settings.Enabled)
        {
            var disabled = TerminalRecordingError.Disabled();
            return TerminalRecordingResult<TerminalRecorder>.Failure(disabled);
        }

        var sessionId = session.Descriptor.Id;
        if (_registry.FindActive(sessionId) is not null)
        {
            var alreadyRecording = TerminalRecordingError.AlreadyRecording();
            return TerminalRecordingResult<TerminalRecorder>.Failure(alreadyRecording);
        }

        var metadata = CreateMetadata(session);
        var setup = new TerminalRecorderSetup(metadata, settings.MaxRecordingBytes);
        using var starting = _registry.ProtectStart(metadata.Id);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.Closing);
        TerminalRecorder? recorder = null;
        var succeeded = false;
        try
        {
            var started = await _recorderFactory.StartAsync(setup, linked.Token);
            if (!started.IsSuccess)
            {
                return started;
            }

            recorder = started.Value;
            if (!TryAttach(session, recorder))
            {
                var unavailable = TerminalRecordingError.AlreadyRecording();
                return TerminalRecordingResult<TerminalRecorder>.Failure(unavailable);
            }

            await _retention.PruneAsync(settings, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            succeeded = true;
            return TerminalRecordingResult<TerminalRecorder>.Success(recorder);
        }
        finally
        {
            if (!succeeded && recorder is not null)
            {
                session.DetachOutputObserver(recorder);
                _registry.Forget(sessionId, recorder);
                await _recorderFactory.DiscardAsync(recorder);
            }
        }
    }

    public async Task<TerminalRecordingResult<TerminalRecorder>> StopAsync(ManagedTerminalSession session)
    {
        var recorder = _registry.FindActive(session.Descriptor.Id);
        if (recorder is null)
        {
            var notRecording = TerminalRecordingError.NotRecording();
            return TerminalRecordingResult<TerminalRecorder>.Failure(notRecording);
        }

        try
        {
            await recorder.StopAsync(TerminalRecordingEndReason.Stopped);
        }
        finally
        {
            session.DetachOutputObserver(recorder);
        }

        return TerminalRecordingResult<TerminalRecorder>.Success(recorder);
    }

    private bool TryAttach(ManagedTerminalSession session, TerminalRecorder recorder)
    {
        if (!_registry.TryRegister(session, recorder) || !session.TryAttachOutputObserver(recorder))
        {
            return false;
        }

        _ = DetachWhenFinishedAsync(session, recorder);
        return true;
    }

    private static async Task DetachWhenFinishedAsync(ManagedTerminalSession session, TerminalRecorder recorder)
    {
        try
        {
            await recorder.Completion;
        }
        finally
        {
            session.DetachOutputObserver(recorder);
        }
    }

    private TerminalRecordingMetadata CreateMetadata(ManagedTerminalSession session)
    {
        var descriptor = session.Descriptor;
        var id = TerminalRecordingId.Create();
        var startedAt = _timeProvider.GetUtcNow().UtcDateTime;
        return new TerminalRecordingMetadata
        {
            Id = id,
            ConnectionId = descriptor.ConnectionId,
            ConnectionName = descriptor.ConnectionName,
            SessionNumber = descriptor.SessionNumber,
            Size = session.Size,
            StartedAt = startedAt,
        };
    }
}
