using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Ssh.Connections;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace DTech.Ssh.Files;

internal sealed class SshNetRemoteFileClient(SshConnection<SftpClient> connection) : IRemoteFileClient
{
    private const string CurrentDirectoryName = ".";
    private const string ParentDirectoryName = "..";

    public async Task<SshResult<IReadOnlyList<RemoteFileEntry>>> ListAsync(string remotePath, CancellationToken cancellationToken)
    {
        try
        {
            var entries = new List<RemoteFileEntry>();
            await foreach (var file in connection.Client.ListDirectoryAsync(remotePath, cancellationToken))
            {
                if (file.Name is CurrentDirectoryName or ParentDirectoryName)
                {
                    continue;
                }

                var entry = ToEntry(file);
                entries.Add(entry);
            }

            return SshResult<IReadOnlyList<RemoteFileEntry>>.Success(entries);
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Listing {remotePath}");
            return SshResult<IReadOnlyList<RemoteFileEntry>>.Failure(error);
        }
    }

    public async Task<SshResult> UploadAsync(Stream source, string remotePath, CancellationToken cancellationToken)
    {
        try
        {
            await connection.Client.UploadFileAsync(source, remotePath, cancellationToken);
            return SshResult.Success();
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Uploading {remotePath}");
            return SshResult.Failure(error);
        }
    }

    public async Task<SshResult> DownloadAsync(string remotePath, Stream destination, CancellationToken cancellationToken)
    {
        try
        {
            await connection.Client.DownloadFileAsync(remotePath, destination, cancellationToken);
            return SshResult.Success();
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Downloading {remotePath}");
            return SshResult.Failure(error);
        }
    }

    public ValueTask DisposeAsync()
    {
        connection.Dispose();
        return ValueTask.CompletedTask;
    }

    private static RemoteFileEntry ToEntry(ISftpFile file)
    {
        var kind = ToKind(file);
        return new RemoteFileEntry(file.Name, file.FullName, kind)
        {
            Size = file.Length,
            LastWriteTimeUtc = file.LastWriteTimeUtc,
        };
    }

    private static RemoteFileKind ToKind(ISftpFile file)
    {
        if (file.IsSymbolicLink)
        {
            return RemoteFileKind.SymbolicLink;
        }

        if (file.IsDirectory)
        {
            return RemoteFileKind.Directory;
        }

        return file.IsRegularFile ? RemoteFileKind.File : RemoteFileKind.Other;
    }
}
