namespace DTech.Core.Terminal.Recording;

public sealed class TerminalRecordingLibrary(TerminalRecordingStorage storage, TerminalRecordingRegistry registry)
{
    public async Task<IReadOnlyList<TerminalRecordingSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var stored = await storage.ListAsync(cancellationToken);
        var active = registry.ListActive();
        var summaries = new List<TerminalRecordingSummary>(stored.Count);
        for (var i = 0; i < stored.Count; i++)
        {
            var metadata = stored[i];
            var recorder = FindRecorder(active, metadata.Id);
            var summary = Summarize(metadata, recorder);
            summaries.Add(summary);
        }

        return summaries
            .OrderByDescending(summary => summary.Metadata.StartedAt)
            .ToList();
    }

    public async Task<TerminalRecordingResult<Stream>> OpenAsync(string idText, CancellationToken cancellationToken)
    {
        var found = await FindAsync(idText, cancellationToken);
        if (!found.IsSuccess)
        {
            return TerminalRecordingResult<Stream>.Failure(found.Error);
        }

        var cast = storage.OpenCast(found.Value.Id);
        if (cast is null)
        {
            var missing = TerminalRecordingError.NotFound();
            return TerminalRecordingResult<Stream>.Failure(missing);
        }

        return TerminalRecordingResult<Stream>.Success(cast);
    }

    public async Task<TerminalRecordingResult<TerminalRecordingMetadata>> DeleteAsync(string idText, CancellationToken cancellationToken)
    {
        var found = await FindAsync(idText, cancellationToken);
        if (!found.IsSuccess)
        {
            return found;
        }

        if (registry.IsRecording(found.Value.Id))
        {
            var stillRecording = TerminalRecordingError.StillRecording();
            return TerminalRecordingResult<TerminalRecordingMetadata>.Failure(stillRecording);
        }

        storage.Delete(found.Value.Id);
        return found;
    }

    private async Task<TerminalRecordingResult<TerminalRecordingMetadata>> FindAsync(string idText, CancellationToken cancellationToken)
    {
        if (!TerminalRecordingId.TryParse(idText, out var id))
        {
            var invalid = TerminalRecordingError.InvalidId();
            return TerminalRecordingResult<TerminalRecordingMetadata>.Failure(invalid);
        }

        var metadata = await storage.FindAsync(id, cancellationToken);
        if (metadata is null)
        {
            var notFound = TerminalRecordingError.NotFound();
            return TerminalRecordingResult<TerminalRecordingMetadata>.Failure(notFound);
        }

        return TerminalRecordingResult<TerminalRecordingMetadata>.Success(metadata);
    }

    private static TerminalRecordingSummary Summarize(TerminalRecordingMetadata stored, TerminalRecorder? recorder)
    {
        if (recorder is null)
        {
            return new TerminalRecordingSummary(stored, false);
        }

        var live = recorder.Metadata with { SizeBytes = recorder.BytesWritten };
        return new TerminalRecordingSummary(live, true);
    }

    private static TerminalRecorder? FindRecorder(IReadOnlyList<TerminalRecorder> active, TerminalRecordingId id)
    {
        for (var i = 0; i < active.Count; i++)
        {
            if (active[i].Id == id)
            {
                return active[i];
            }
        }

        return null;
    }
}
