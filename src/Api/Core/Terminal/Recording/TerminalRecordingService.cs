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
        var started = await _recorderFactory.StartAsync(setup, cancellationToken);
        if (!started.IsSuccess)
        {
            return started;
        }

        var recorder = started.Value;
        var attached = await AttachAsync(session, recorder);
        if (!attached.IsSuccess)
        {
            return attached;
        }

        await _retention.PruneAsync(settings, cancellationToken);
        return attached;
    }

    public async Task<TerminalRecordingResult<TerminalRecorder>> StopAsync(ManagedTerminalSession session)
    {
        var recorder = _registry.FindActive(session.Descriptor.Id);
        if (recorder is null)
        {
            var notRecording = TerminalRecordingError.NotRecording();
            return TerminalRecordingResult<TerminalRecorder>.Failure(notRecording);
        }

        await recorder.StopAsync(TerminalRecordingEndReason.Stopped);
        return TerminalRecordingResult<TerminalRecorder>.Success(recorder);
    }

    private async Task<TerminalRecordingResult<TerminalRecorder>> AttachAsync(ManagedTerminalSession session, TerminalRecorder recorder)
    {
        var sessionId = session.Descriptor.Id;
        if (!_registry.TryRegister(sessionId, recorder))
        {
            await _recorderFactory.DiscardAsync(recorder);
            var alreadyRecording = TerminalRecordingError.AlreadyRecording();
            return TerminalRecordingResult<TerminalRecorder>.Failure(alreadyRecording);
        }

        if (!session.TryAttachOutputObserver(recorder))
        {
            _registry.Forget(sessionId, recorder);
            await _recorderFactory.DiscardAsync(recorder);
            var unavailable = TerminalRecordingError.AlreadyRecording();
            return TerminalRecordingResult<TerminalRecorder>.Failure(unavailable);
        }

        var closingRegistration = session.Closing.Register(() => recorder.RequestStop(TerminalRecordingEndReason.SessionClosed));
        _ = DetachWhenFinishedAsync(session, recorder, closingRegistration);
        return TerminalRecordingResult<TerminalRecorder>.Success(recorder);
    }

    private async Task DetachWhenFinishedAsync(ManagedTerminalSession session, TerminalRecorder recorder, CancellationTokenRegistration closingRegistration)
    {
        await recorder.Completion;
        session.DetachOutputObserver(recorder);
        await closingRegistration.DisposeAsync();
        if (session.IsClosed)
        {
            _registry.Forget(session.Descriptor.Id, recorder);
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
