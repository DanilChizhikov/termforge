namespace DTech.Core.CommandHistory;

public interface ICommandExclusionPatternStore
{
    Task<IReadOnlyList<string>?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(IReadOnlyList<string> patterns, CancellationToken cancellationToken);
}
