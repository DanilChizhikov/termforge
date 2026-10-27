using DTech.Core.Connections;
using DTech.Core.Editor;
using DTech.Core.Files;

namespace DTech.Ssh.IntegrationTests.Editor;

internal sealed class FailingReplaceEditorFileClient(IRemoteEditorFileClient inner) : IRemoteEditorFileClient
{
    public const string FailureMessage = "Replacing failed: simulated rename failure.";

    public Task<SshResult<RemoteFileAttributes>> GetAttributesAsync(string remotePath, CancellationToken cancellationToken)
    {
        return inner.GetAttributesAsync(remotePath, cancellationToken);
    }

    public Task<SshResult<byte[]>> ReadAsync(EditorReadRequest request, CancellationToken cancellationToken)
    {
        return inner.ReadAsync(request, cancellationToken);
    }

    public Task<SshResult<RemoteFileAttributes>> CreateFileAsync(RemoteFileCreation creation, CancellationToken cancellationToken)
    {
        return inner.CreateFileAsync(creation, cancellationToken);
    }

    public Task<SshResult> OverwriteAsync(RemoteFileContent content, CancellationToken cancellationToken)
    {
        return inner.OverwriteAsync(content, cancellationToken);
    }

    public Task<SshResult> ChangeOwnerAsync(RemoteOwnership ownership, CancellationToken cancellationToken)
    {
        return inner.ChangeOwnerAsync(ownership, cancellationToken);
    }

    public Task<SshResult<AtomicReplaceOutcome>> ReplaceAtomicallyAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        var failure = new SshError(SshErrorKind.OperationFailed, FailureMessage);
        var result = SshResult<AtomicReplaceOutcome>.Failure(failure);
        return Task.FromResult(result);
    }

    public Task<SshResult> RenameAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        return inner.RenameAsync(request, cancellationToken);
    }

    public Task<SshResult> DeleteFileAsync(string remotePath, CancellationToken cancellationToken)
    {
        return inner.DeleteFileAsync(remotePath, cancellationToken);
    }

    public Task<SshResult<IReadOnlyList<string>>> ListNamesAsync(string remoteDirectory, CancellationToken cancellationToken)
    {
        return inner.ListNamesAsync(remoteDirectory, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return inner.DisposeAsync();
    }
}
