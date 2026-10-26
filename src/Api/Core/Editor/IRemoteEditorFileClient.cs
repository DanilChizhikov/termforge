using DTech.Core.Connections;
using DTech.Core.Files;

namespace DTech.Core.Editor;

public interface IRemoteEditorFileClient : IAsyncDisposable
{
    Task<SshResult<RemoteFileAttributes>> GetAttributesAsync(string remotePath, CancellationToken cancellationToken);

    Task<SshResult<byte[]>> ReadAsync(RemoteReadRequest request, CancellationToken cancellationToken);

    Task<SshResult<RemoteFileAttributes>> CreateFileAsync(RemoteFileCreation creation, CancellationToken cancellationToken);

    Task<SshResult> OverwriteAsync(RemoteFileContent content, CancellationToken cancellationToken);

    Task<SshResult> ChangeOwnerAsync(RemoteOwnership ownership, CancellationToken cancellationToken);

    Task<SshResult<AtomicReplaceOutcome>> ReplaceAtomicallyAsync(RemoteRenameRequest request, CancellationToken cancellationToken);

    Task<SshResult> RenameAsync(RemoteRenameRequest request, CancellationToken cancellationToken);

    Task<SshResult> DeleteFileAsync(string remotePath, CancellationToken cancellationToken);

    Task<SshResult<IReadOnlyList<string>>> ListNamesAsync(string remoteDirectory, CancellationToken cancellationToken);
}
