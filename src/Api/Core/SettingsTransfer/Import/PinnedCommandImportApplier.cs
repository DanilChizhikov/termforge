using DTech.Core.CommandHistory;
using DTech.Core.SettingsTransfer.Document;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class PinnedCommandImportApplier(ICommandHistoryRepository repository, CommandExclusionSettings exclusionSettings, TimeProvider timeProvider)
{
    public async Task<PinnedCommandImportOutcome> ApplyAsync(IReadOnlyList<ExportedPinnedCommand> commands, SettingsImportPlan plan, CancellationToken cancellationToken)
    {
        var rules = await exclusionSettings.LoadRulesAsync(cancellationToken);
        var entries = new List<CommandHistoryEntry>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
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

            var entryId = Guid.NewGuid();
            var pinned = new CommandHistoryEntry(entryId, localId.Value, command, now, null, true);
            entries.Add(pinned);
        }

        var added = await repository.ImportPinnedAsync(entries, cancellationToken);
        return new PinnedCommandImportOutcome(added, skipped);
    }
}
