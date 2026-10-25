using DTech.Core.Connections;

namespace DTech.Core.Files;

public interface IRemoteFileClient : IAsyncDisposable
{
    Task<SshResult<IReadOnlyList<RemoteFileEntry>>> ListAsync(string remotePath, CancellationToken cancellationToken);

    Task<SshResult> UploadAsync(Stream source, string remotePath, CancellationToken cancellationToken);

    Task<SshResult> DownloadAsync(string remotePath, Stream destination, CancellationToken cancellationToken);
}
