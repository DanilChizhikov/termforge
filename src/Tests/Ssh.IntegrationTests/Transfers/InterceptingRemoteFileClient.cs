using DTech.Core.Connections;
using DTech.Core.Files;

namespace DTech.Ssh.IntegrationTests.Transfers;

internal sealed class InterceptingRemoteFileClient(IRemoteFileClient inner, ClientInterception interception) : IRemoteFileClient
{
    public Task<SshResult<IReadOnlyList<RemoteFileEntry>>> ListAsync(string remotePath, CancellationToken cancellationToken)
    {
        return inner.ListAsync(remotePath, cancellationToken);
    }

    public Task<SshResult<RemoteFileEntry>> StatAsync(string remotePath, CancellationToken cancellationToken)
    {
        return inner.StatAsync(remotePath, cancellationToken);
    }

    public Task<SshResult<string>> GetHomeDirectoryAsync(CancellationToken cancellationToken)
    {
        return inner.GetHomeDirectoryAsync(cancellationToken);
    }

    public Task<SshResult> CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken)
    {
        return inner.CreateDirectoryAsync(remotePath, cancellationToken);
    }

    public Task<SshResult> RenameAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        return inner.RenameAsync(request, cancellationToken);
    }

    public Task<SshResult> DeleteAsync(string remotePath, CancellationToken cancellationToken)
    {
        return inner.DeleteAsync(remotePath, cancellationToken);
    }

    public Task<SshResult> UploadAsync(Stream source, string remotePath, CancellationToken cancellationToken)
    {
        return inner.UploadAsync(source, remotePath, cancellationToken);
    }

    public Task<SshResult> DownloadAsync(string remotePath, Stream destination, CancellationToken cancellationToken)
    {
        return inner.DownloadAsync(remotePath, destination, cancellationToken);
    }

    public async Task<SshResult> WriteFileAsync(RemoteWriteRequest request, CancellationToken cancellationToken)
    {
        interception.WriteOffsets.Enqueue(request.Offset);
        var upload = interception.Upload;
        var source = upload is null ? request.Source : new InterceptingStream(request.Source, upload);
        var progress = new RecordingProgress(request.Progress, interception);
        var intercepted = request with { Source = source, Progress = progress };
        var result = await inner.WriteFileAsync(intercepted, cancellationToken);
        if (!result.IsSuccess || !interception.FailUploadAfterSource)
        {
            return result;
        }

        var reset = new SshError(SshErrorKind.ConnectionFailed, "Simulated connection reset during upload.");
        return SshResult.Failure(reset);
    }

    public Task<SshResult> ReadFileAsync(RemoteReadRequest request, CancellationToken cancellationToken)
    {
        interception.ReadOffsets.Enqueue(request.Offset);
        var download = interception.Download;
        var destination = download is null ? request.Destination : new InterceptingStream(request.Destination, download);
        var progress = new RecordingProgress(request.Progress, interception);
        var intercepted = request with { Destination = destination, Progress = progress };
        return inner.ReadFileAsync(intercepted, cancellationToken);
    }

    public Task<SshResult> ReplaceAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        return inner.ReplaceAsync(request, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return inner.DisposeAsync();
    }

    private sealed class RecordingProgress(IProgress<long>? next, ClientInterception interception) : IProgress<long>
    {
        public void Report(long value)
        {
            interception.ProgressReports.Enqueue(value);
            next?.Report(value);
        }
    }
}
