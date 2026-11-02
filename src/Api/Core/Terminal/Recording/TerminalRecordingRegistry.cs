using DTech.Core.Terminal.Sessions;

namespace DTech.Core.Terminal.Recording;

public sealed class TerminalRecordingRegistry
{
    private readonly Dictionary<TerminalSessionId, SessionRecording> _sessions = new();
    private readonly HashSet<TerminalRecorder> _active = [];
    private readonly HashSet<TerminalRecordingId> _starting = [];
    private readonly Lock _gate = new();

    public TerminalRecorder? FindLatest(TerminalSessionId sessionId)
    {
        lock (_gate)
        {
            return _sessions.GetValueOrDefault(sessionId)?.Recorder;
        }
    }

    public TerminalRecorder? FindActive(TerminalSessionId sessionId)
    {
        var latest = FindLatest(sessionId);
        return latest is { IsActive: true } ? latest : null;
    }

    public bool TryRegister(ManagedTerminalSession session, TerminalRecorder recorder)
    {
        var sessionId = session.Descriptor.Id;
        lock (_gate)
        {
            if (session.IsClosed)
            {
                return false;
            }

            if (_sessions.TryGetValue(sessionId, out var existing))
            {
                if (existing.Recorder.IsActive)
                {
                    return false;
                }

                existing.Recorder = recorder;
                TrackActive(recorder);
                return true;
            }

            var entry = new SessionRecording(recorder);
            _sessions.Add(sessionId, entry);
            TrackActive(recorder);
            Action onClosing = () => OnClosing(sessionId);
            entry.Registration = session.Closing.Register(onClosing);
            return !session.IsClosed;
        }
    }

    public void Forget(TerminalSessionId sessionId, TerminalRecorder recorder)
    {
        CancellationTokenRegistration? registration = null;
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out var existing) && ReferenceEquals(existing.Recorder, recorder))
            {
                _sessions.Remove(sessionId);
                registration = existing.Registration;
            }
        }

        registration?.Dispose();
    }

    public IReadOnlyList<TerminalRecorder> ListActive()
    {
        lock (_gate)
        {
            return _active.Where(recorder => recorder.IsActive).ToList();
        }
    }

    public bool IsRecording(TerminalRecordingId id)
    {
        lock (_gate)
        {
            if (_starting.Contains(id))
            {
                return true;
            }

            foreach (var recorder in _active)
            {
                if (recorder.Id == id && recorder.IsActive)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public IDisposable ProtectStart(TerminalRecordingId id)
    {
        lock (_gate)
        {
            if (!_starting.Add(id))
            {
                throw new InvalidOperationException($"Recording {id} is already starting.");
            }
        }

        return new StartingRecording(this, id);
    }

    private void ReleaseStart(TerminalRecordingId id)
    {
        lock (_gate)
        {
            _starting.Remove(id);
        }
    }

    private void TrackActive(TerminalRecorder recorder)
    {
        _active.Add(recorder);
        Action<Task> release = _ => ReleaseFinished(recorder);
        _ = recorder.Completion.ContinueWith(release, TaskScheduler.Default);
    }

    private void ReleaseFinished(TerminalRecorder recorder)
    {
        lock (_gate)
        {
            _active.Remove(recorder);
        }
    }

    private void OnClosing(TerminalSessionId sessionId)
    {
        SessionRecording? entry;
        lock (_gate)
        {
            if (!_sessions.Remove(sessionId, out entry))
            {
                return;
            }
        }

        entry.Recorder.RequestStop(TerminalRecordingEndReason.SessionClosed);
    }

    public async Task StopAllAsync(TerminalRecordingEndReason reason)
    {
        var active = ListActive();
        var stops = new Task[active.Count];
        for (var i = 0; i < active.Count; i++)
        {
            stops[i] = active[i].StopAsync(reason);
        }

        await Task.WhenAll(stops);
    }

    private sealed class SessionRecording(TerminalRecorder recorder)
    {
        public TerminalRecorder Recorder { get; set; } = recorder;

        public CancellationTokenRegistration Registration { get; set; }
    }

    private sealed class StartingRecording(TerminalRecordingRegistry registry, TerminalRecordingId id) : IDisposable
    {
        private TerminalRecordingRegistry? _registry = registry;

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _registry, null);
            owner?.ReleaseStart(id);
        }
    }
}
