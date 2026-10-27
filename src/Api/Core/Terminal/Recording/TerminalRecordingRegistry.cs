using DTech.Core.Terminal.Sessions;

namespace DTech.Core.Terminal.Recording;

public sealed class TerminalRecordingRegistry
{
    private readonly Dictionary<TerminalSessionId, TerminalRecorder> _latest = new();
    private readonly Lock _gate = new();

    public TerminalRecorder? FindLatest(TerminalSessionId sessionId)
    {
        lock (_gate)
        {
            return _latest.GetValueOrDefault(sessionId);
        }
    }

    public TerminalRecorder? FindActive(TerminalSessionId sessionId)
    {
        var latest = FindLatest(sessionId);
        return latest is { IsActive: true } ? latest : null;
    }

    public bool TryRegister(TerminalSessionId sessionId, TerminalRecorder recorder)
    {
        lock (_gate)
        {
            if (_latest.TryGetValue(sessionId, out var existing) && existing.IsActive)
            {
                return false;
            }

            _latest[sessionId] = recorder;
            return true;
        }
    }

    public void Forget(TerminalSessionId sessionId, TerminalRecorder recorder)
    {
        lock (_gate)
        {
            if (_latest.TryGetValue(sessionId, out var existing) && ReferenceEquals(existing, recorder))
            {
                _latest.Remove(sessionId);
            }
        }
    }

    public IReadOnlyList<TerminalRecorder> ListActive()
    {
        lock (_gate)
        {
            return _latest.Values.Where(recorder => recorder.IsActive).ToList();
        }
    }

    public bool IsRecording(TerminalRecordingId id)
    {
        var active = ListActive();
        for (var i = 0; i < active.Count; i++)
        {
            if (active[i].Id == id)
            {
                return true;
            }
        }

        return false;
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
}
