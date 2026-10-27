namespace DTech.Core.Editor;

public interface IRemoteEditorConnectionOpener
{
    Task<EditorResult<RemoteEditorSession>> OpenAsync(Guid connectionId, CancellationToken cancellationToken);
}
