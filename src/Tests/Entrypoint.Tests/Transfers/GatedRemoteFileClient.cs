using DTech.Core.Connections;
using DTech.Core.Files;

namespace DTech.Entrypoint.Tests.Transfers;

internal sealed class GatedRemoteFileClient(TaskCompletionSource gate) : IRemoteFileClient
{
    public static readonly byte[] Content = "remote-content"u8.ToArray();

    public Task<SshResult<IReadOnlyList<RemoteFileEntry>>> ListAsync(string remotePath, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<SshResult<RemoteFileEntry>> StatAsync(string remotePath, CancellationToken cancellationToken)
    {
        var name = remotePath[(remotePath.LastIndexOf('/') + 1)..];
        var entry = new RemoteFileEntry(name, remotePath, RemoteFileKind.File)
        {
            Size = Content.Length,
            LastWriteTimeUtc = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc),
        };
        var result = SshResult<RemoteFileEntry>.Success(entry);
        return Task.FromResult(result);
    }

    public Task<SshResult<string>> GetHomeDirectoryAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<SshResult> CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<SshResult> RenameAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<SshResult> DeleteAsync(string remotePath, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<SshResult> UploadAsync(Stream source, string remotePath, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<SshResult> DownloadAsync(string remotePath, Stream destination, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<SshResult> WriteFileAsync(RemoteWriteRequest request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public async Task<SshResult> ReadFileAsync(RemoteReadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await gate.Task.WaitAsync(cancellationToken);
            await request.Destination.WriteAsync(Content, cancellationToken);
            request.Progress?.Report(Content.Length);
            return SshResult.Success();
        }
        catch (OperationCanceledException)
        {
            var cancelled = new SshError(SshErrorKind.Cancelled, "cancelled");
            return SshResult.Failure(cancelled);
        }
    }

    public Task<SshResult> ReplaceAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
