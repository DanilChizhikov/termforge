namespace DTech.Core.Terminal.Recording;

public sealed class TerminalRecordingRetention(TerminalRecordingStorage storage, TerminalRecordingRegistry registry, TimeProvider timeProvider)
{
    public async Task<int> PruneAsync(TerminalRecordingSettings settings, CancellationToken cancellationToken)
    {
        var recordings = await storage.ListAsync(cancellationToken);
        var newestFirst = recordings
            .OrderByDescending(recording => recording.StartedAt)
            .ThenByDescending(recording => recording.Id.Value, StringComparer.Ordinal)
            .ToList();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var oldestKept = now - settings.RetentionPeriod;
        var keptCount = 0;
        var deletedCount = 0;
        for (var i = 0; i < newestFirst.Count; i++)
        {
            var recording = newestFirst[i];
            if (registry.IsRecording(recording.Id))
            {
                keptCount++;
                continue;
            }

            var isOverCount = keptCount >= settings.MaxRecordings;
            var isExpired = recording.StartedAt < oldestKept;
            if (isOverCount || isExpired)
            {
                storage.Delete(recording.Id);
                deletedCount++;
                continue;
            }

            keptCount++;
        }

        return deletedCount;
    }
}
