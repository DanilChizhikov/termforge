namespace DTech.Core.Editor.Sudo;

public interface IFileAccessReader
{
    Task<EditorResult<RemoteFileAccess>> ReadAsync(FileAccessRequest request, CancellationToken cancellationToken);
}
