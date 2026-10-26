using DTech.Core.CommandHistory;

namespace DTech.Core.Tests.CommandHistory.Support;

internal sealed class InMemoryExclusionPatternStore : ICommandExclusionPatternStore
{
    private IReadOnlyList<string>? _patterns;

    public Task<IReadOnlyList<string>?> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_patterns);
    }

    public Task SaveAsync(IReadOnlyList<string> patterns, CancellationToken cancellationToken)
    {
        _patterns = patterns;
        return Task.CompletedTask;
    }
}
