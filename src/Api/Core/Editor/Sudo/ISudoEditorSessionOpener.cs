namespace DTech.Core.Editor.Sudo;

public interface ISudoEditorSessionOpener
{
    Task<EditorResult<RemoteEditorSession>> OpenAsync(Guid connectionId, SudoCredentials credentials, CancellationToken cancellationToken);
}
