namespace DTech.Core.CommandHistory;

public sealed class CommandHistoryService : ICommandHistoryRecorder
{
    private readonly ICommandHistoryRepository _repository;
    private readonly CommandExclusionSettings _exclusionSettings;

    public CommandHistoryService(ICommandHistoryRepository repository, CommandExclusionSettings exclusionSettings)
    {
        _repository = repository;
        _exclusionSettings = exclusionSettings;
    }

    public async Task<bool> RecordAsync(RecordedCommand command, CancellationToken cancellationToken)
    {
        var text = command.Command.TrimEnd();
        var rules = await _exclusionSettings.LoadRulesAsync(cancellationToken);
        var reason = rules.FindReason(text);
        if (reason is not null)
        {
            return false;
        }

        var entry = new CommandHistoryEntry(Guid.NewGuid(), command.ConnectionId, text, command.ExecutedAt, command.ExitCode, false);
        await _repository.AddAsync(entry, cancellationToken);
        await _repository.PruneUnpinnedAsync(command.ConnectionId, CommandHistoryLimits.MaxUnpinnedEntriesPerConnection, cancellationToken);
        return true;
    }

    public Task<IReadOnlyList<CommandHistoryEntry>> ListAsync(CommandHistoryQuery query, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(query.Limit, 1, CommandHistoryLimits.MaxPageSize);
        var search = NormalizeSearch(query.Search);
        var normalized = query with { Limit = limit, Search = search };
        return _repository.ListAsync(normalized, cancellationToken);
    }

    public Task<bool> SetPinnedAsync(Guid id, bool isPinned, CancellationToken cancellationToken)
    {
        return _repository.SetPinnedAsync(id, isPinned, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        return _repository.DeleteAsync(id, cancellationToken);
    }

    public Task<int> ClearUnpinnedAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        return _repository.ClearUnpinnedAsync(connectionId, cancellationToken);
    }

    private static string? NormalizeSearch(string? search)
    {
        var trimmed = search?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > CommandHistoryLimits.MaxSearchLength ? trimmed[..CommandHistoryLimits.MaxSearchLength] : trimmed;
    }
}
