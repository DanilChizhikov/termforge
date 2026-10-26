using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Ssh.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Files;

internal sealed class SshNetRemoteFileClient : IRemoteFileClient
{
    private const string CurrentDirectoryName = ".";
    private const string ParentDirectoryName = "..";

    private readonly SshConnection<SftpClient> _connection;
    private readonly SftpEntryMapper _entryMapper;
    private readonly SftpEntryLocator _entryLocator;

    public SshNetRemoteFileClient(SshConnection<SftpClient> connection)
    {
        _connection = connection;
        _entryMapper = new SftpEntryMapper(connection.Client);
        _entryLocator = new SftpEntryLocator(connection.Client);
    }

    public async Task<SshResult<IReadOnlyList<RemoteFileEntry>>> ListAsync(string remotePath, CancellationToken cancellationToken)
    {
        try
        {
            var entries = new List<RemoteFileEntry>();
            await foreach (var file in _connection.Client.ListDirectoryAsync(remotePath, cancellationToken))
            {
                if (file.Name is CurrentDirectoryName or ParentDirectoryName)
                {
                    continue;
                }

                var entry = await _entryMapper.MapAsync(file, cancellationToken);
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

    public async Task<SshResult<RemoteFileEntry>> StatAsync(string remotePath, CancellationToken cancellationToken)
    {
        try
        {
            var file = await _entryLocator.FindAsync(remotePath, cancellationToken);
            var entry = await _entryMapper.MapAsync(file, cancellationToken);
            return SshResult<RemoteFileEntry>.Success(entry);
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Reading {remotePath}");
            return SshResult<RemoteFileEntry>.Failure(error);
        }
    }

    public Task<SshResult<string>> GetHomeDirectoryAsync(CancellationToken cancellationToken)
    {
        var home = _connection.Client.WorkingDirectory;
        var result = SshResult<string>.Success(home);
        return Task.FromResult(result);
    }

    public async Task<SshResult> CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken)
    {
        try
        {
            if (await _connection.Client.ExistsAsync(remotePath, cancellationToken))
            {
                return Failure(SshErrorKind.RemotePathExists, $"Creating {remotePath} failed: path already exists");
            }

            await _connection.Client.CreateDirectoryAsync(remotePath, cancellationToken);
            return SshResult.Success();
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Creating directory {remotePath}");
            return SshResult.Failure(error);
        }
    }

    public async Task<SshResult> RenameAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var source = await _entryLocator.FindAsync(request.FromPath, cancellationToken);
            if (source.IsSymbolicLink)
            {
                return Failure(SshErrorKind.OperationFailed, $"Renaming {request.FromPath} failed: renaming symbolic links is not supported over SFTP");
            }

            if (await _connection.Client.ExistsAsync(request.ToPath, cancellationToken))
            {
                return Failure(SshErrorKind.RemotePathExists, $"Renaming to {request.ToPath} failed: path already exists");
            }

            await _connection.Client.RenameFileAsync(request.FromPath, request.ToPath, cancellationToken);
            return SshResult.Success();
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Renaming {request.FromPath}");
            return SshResult.Failure(error);
        }
    }

    public async Task<SshResult> DeleteAsync(string remotePath, CancellationToken cancellationToken)
    {
        try
        {
            var file = await _entryLocator.FindAsync(remotePath, cancellationToken);
            if (file.IsSymbolicLink)
            {
                return Failure(SshErrorKind.OperationFailed, $"Deleting {remotePath} failed: deleting symbolic links is not supported over SFTP");
            }

            if (!file.IsDirectory)
            {
                await _connection.Client.DeleteFileAsync(remotePath, cancellationToken);
                return SshResult.Success();
            }

            if (await HasChildrenAsync(remotePath, cancellationToken))
            {
                return Failure(SshErrorKind.DirectoryNotEmpty, $"Deleting {remotePath} failed: directory is not empty");
            }

            await _connection.Client.DeleteDirectoryAsync(remotePath, cancellationToken);
            return SshResult.Success();
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Deleting {remotePath}");
            return SshResult.Failure(error);
        }
    }

    public async Task<SshResult> UploadAsync(Stream source, string remotePath, CancellationToken cancellationToken)
    {
        try
        {
            await _connection.Client.UploadFileAsync(source, remotePath, cancellationToken);
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
            await _connection.Client.DownloadFileAsync(remotePath, destination, cancellationToken);
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
        _connection.Dispose();
        return ValueTask.CompletedTask;
    }

    private static SshResult Failure(SshErrorKind kind, string message)
    {
        var error = new SshError(kind, message);
        return SshResult.Failure(error);
    }

    private async Task<bool> HasChildrenAsync(string remotePath, CancellationToken cancellationToken)
    {
        await foreach (var child in _connection.Client.ListDirectoryAsync(remotePath, cancellationToken))
        {
            if (child.Name is not (CurrentDirectoryName or ParentDirectoryName))
            {
                return true;
            }
        }

        return false;
    }
}
