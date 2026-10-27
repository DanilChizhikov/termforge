using DTech.Core.Connections;

namespace DTech.Core.Files;

public interface IRemoteFileClient : IAsyncDisposable
{
    Task<SshResult<IReadOnlyList<RemoteFileEntry>>> ListAsync(string remotePath, CancellationToken cancellationToken);

    Task<SshResult<RemoteFileEntry>> StatAsync(string remotePath, CancellationToken cancellationToken);

    Task<SshResult<string>> GetHomeDirectoryAsync(CancellationToken cancellationToken);

    Task<SshResult> CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken);

    Task<SshResult> RenameAsync(RemoteRenameRequest request, CancellationToken cancellationToken);

    Task<SshResult> DeleteAsync(string remotePath, CancellationToken cancellationToken);

    Task<SshResult> UploadAsync(Stream source, string remotePath, CancellationToken cancellationToken);

    Task<SshResult> DownloadAsync(string remotePath, Stream destination, CancellationToken cancellationToken);

    Task<SshResult> WriteFileAsync(RemoteWriteRequest request, CancellationToken cancellationToken);

    Task<SshResult> ReadFileAsync(RemoteReadRequest request, CancellationToken cancellationToken);

    Task<SshResult> ReplaceAsync(RemoteRenameRequest request, CancellationToken cancellationToken);
}
