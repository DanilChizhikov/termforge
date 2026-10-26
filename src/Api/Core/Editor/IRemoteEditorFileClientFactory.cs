using DTech.Core.Connections;

namespace DTech.Core.Editor;

public interface IRemoteEditorFileClientFactory
{
    Task<SshResult<IRemoteEditorFileClient>> ConnectAsync(SshConnectionTarget target, CancellationToken cancellationToken);
}
