namespace DTech.Core.Editor.Sudo;

public interface ISudoStatusReader
{
    Task<EditorResult<SudoStatus>> ReadAsync(Guid connectionId, CancellationToken cancellationToken);
}
