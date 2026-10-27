using DTech.Core.CommandHistory;
using DTech.Core.SettingsTransfer.Document;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class PinnedCommandImportApplier(ICommandHistoryRepository repository, CommandExclusionSettings exclusionSettings, TimeProvider timeProvider)
{
    public async Task<PinnedCommandImportOutcome> ApplyAsync(IReadOnlyList<ExportedPinnedCommand> commands, SettingsImportPlan plan, CancellationToken cancellationToken)
    {
        var rules = await exclusionSettings.LoadRulesAsync(cancellationToken);
        var historyByConnection = new Dictionary<Guid, Dictionary<string, CommandHistoryEntry>>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var added = 0;
        var skipped = 0;
        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i].Command!;
            var localId = plan.MapConnectionId(commands[i].ConnectionId);
            if (localId is null || rules.FindReason(command) is not null)
            {
                skipped++;
                continue;
            }

            var history = await LoadHistoryAsync(localId.Value, historyByConnection, cancellationToken);
            if (history.TryGetValue(command, out var entry))
            {
                if (!entry.IsPinned)
                {
                    await repository.SetPinnedAsync(entry.Id, true, cancellationToken);
                    history[command] = entry with { IsPinned = true };
                    added++;
                }

                continue;
            }

            var entryId = Guid.NewGuid();
            var pinned = new CommandHistoryEntry(entryId, localId.Value, command, now, null, true);
            await repository.AddAsync(pinned, cancellationToken);
            history[command] = pinned;
            added++;
        }

        return new PinnedCommandImportOutcome(added, skipped);
    }

    private async Task<Dictionary<string, CommandHistoryEntry>> LoadHistoryAsync(Guid connectionId, Dictionary<Guid, Dictionary<string, CommandHistoryEntry>> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(connectionId, out var cached))
        {
            return cached;
        }

        var query = new CommandHistoryQuery(connectionId, null, CommandHistoryLimits.MaxPageSize);
        var entries = await repository.ListAsync(query, cancellationToken);
        var history = new Dictionary<string, CommandHistoryEntry>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Count; i++)
        {
            history.TryAdd(entries[i].Command, entries[i]);
        }

        cache[connectionId] = history;
        return history;
    }
}
