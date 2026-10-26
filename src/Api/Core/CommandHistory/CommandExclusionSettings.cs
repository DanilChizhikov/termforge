namespace DTech.Core.CommandHistory;

public sealed class CommandExclusionSettings(ICommandExclusionPatternStore store)
{
    public async Task<IReadOnlyList<string>> GetPatternsAsync(CancellationToken cancellationToken)
    {
        var stored = await store.LoadAsync(cancellationToken);
        return stored ?? DefaultCommandExclusionPatterns.Values;
    }

    public async Task<CommandExclusionRules> LoadRulesAsync(CancellationToken cancellationToken)
    {
        var patterns = await GetPatternsAsync(cancellationToken);
        return CommandExclusionRules.Create(patterns);
    }

    public async Task<string?> SavePatternsAsync(IReadOnlyList<string> patterns, CancellationToken cancellationToken)
    {
        if (patterns.Count > CommandHistoryLimits.MaxExclusionPatterns)
        {
            return $"At most {CommandHistoryLimits.MaxExclusionPatterns} patterns are allowed.";
        }

        for (var i = 0; i < patterns.Count; i++)
        {
            var error = CommandExclusionRules.Validate(patterns[i]);
            if (error is not null)
            {
                return error;
            }
        }

        await store.SaveAsync(patterns, cancellationToken);
        return null;
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        var defaults = DefaultCommandExclusionPatterns.Values;
        await store.SaveAsync(defaults, cancellationToken);
    }
}
