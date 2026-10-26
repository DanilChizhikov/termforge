using DTech.Core.Connections;
using DTech.Core.Editor;
using DTech.Core.Files;
using DTech.Ssh.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Editor;

internal sealed class SshNetRemoteEditorFileClient(SshConnection<SftpClient> connection) : IRemoteEditorFileClient
{
    private const int PrivateMode = 0b110_000_000;
    private const int ReadBufferSize = 64 * 1024;
    private const string CurrentDirectoryName = ".";
    private const string ParentDirectoryName = "..";

    private SftpClient Client => connection.Client;

    public async Task<SshResult<RemoteFileAttributes>> GetAttributesAsync(string remotePath, CancellationToken cancellationToken)
    {
        try
        {
            var attributes = await Client.GetAttributesAsync(remotePath, cancellationToken);
            var mapped = SftpAttributesMapper.Map(attributes);
            return SshResult<RemoteFileAttributes>.Success(mapped);
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Reading attributes of {remotePath}");
            return SshResult<RemoteFileAttributes>.Failure(error);
        }
    }

    public async Task<SshResult<byte[]>> ReadAsync(EditorReadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await Client.OpenAsync(request.Path, FileMode.Open, FileAccess.Read, cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[ReadBufferSize];
            var limit = request.MaxBytes + 1;
            while (buffer.Length < limit)
            {
                var read = await stream.ReadAsync(chunk, cancellationToken);
                if (read == 0)
                {
                    break;
                }

                buffer.Write(chunk, 0, read);
            }

            var content = buffer.ToArray();
            return SshResult<byte[]>.Success(content);
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Reading {request.Path}");
            return SshResult<byte[]>.Failure(error);
        }
    }

    public async Task<SshResult<RemoteFileAttributes>> CreateFileAsync(RemoteFileCreation creation, CancellationToken cancellationToken)
    {
        var created = false;
        try
        {
            await using (var stream = await Client.OpenAsync(creation.Path, FileMode.CreateNew, FileAccess.Write, cancellationToken))
            {
                created = true;
                await ChangeModeAsync(creation.Path, PrivateMode, cancellationToken);
                await stream.WriteAsync(creation.Content, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            await ChangeModeAsync(creation.Path, creation.Mode, cancellationToken);
            var attributes = await Client.GetAttributesAsync(creation.Path, cancellationToken);
            var mapped = SftpAttributesMapper.Map(attributes);
            return SshResult<RemoteFileAttributes>.Success(mapped);
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Writing {creation.Path}");
            if (created)
            {
                var removed = await DeleteFileAsync(creation.Path, CancellationToken.None);
                error = removed.IsSuccess ? error : error with { Message = $"{error.Message} The partial file could not be removed: {removed.Error!.Message}" };
            }

            return SshResult<RemoteFileAttributes>.Failure(error);
        }
    }

    public async Task<SshResult> OverwriteAsync(RemoteFileContent content, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await Client.OpenAsync(content.Path, FileMode.Truncate, FileAccess.Write, cancellationToken);
            await stream.WriteAsync(content.Content, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return SshResult.Success();
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Writing {content.Path}");
            return SshResult.Failure(error);
        }
    }

    public async Task<SshResult> ChangeOwnerAsync(RemoteOwnership ownership, CancellationToken cancellationToken)
    {
        try
        {
            var attributes = await Client.GetAttributesAsync(ownership.Path, cancellationToken);
            attributes.UserId = ownership.UserId;
            attributes.GroupId = ownership.GroupId;
            await Task.Run(() => Client.SetAttributes(ownership.Path, attributes), cancellationToken);
            return SshResult.Success();
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Changing the owner of {ownership.Path}");
            return SshResult.Failure(error);
        }
    }

    public async Task<SshResult<AtomicReplaceOutcome>> ReplaceAtomicallyAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(() => Client.RenameFile(request.FromPath, request.ToPath, isPosix: true), cancellationToken);
            return SshResult<AtomicReplaceOutcome>.Success(AtomicReplaceOutcome.Replaced);
        }
        catch (NotSupportedException)
        {
            return SshResult<AtomicReplaceOutcome>.Success(AtomicReplaceOutcome.Unsupported);
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Replacing {request.ToPath}");
            return SshResult<AtomicReplaceOutcome>.Failure(error);
        }
    }

    public async Task<SshResult> RenameAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await Client.RenameFileAsync(request.FromPath, request.ToPath, cancellationToken);
            return SshResult.Success();
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Renaming {request.FromPath}");
            return SshResult.Failure(error);
        }
    }

    public async Task<SshResult> DeleteFileAsync(string remotePath, CancellationToken cancellationToken)
    {
        try
        {
            await Client.DeleteFileAsync(remotePath, cancellationToken);
            return SshResult.Success();
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Deleting {remotePath}");
            return SshResult.Failure(error);
        }
    }

    public async Task<SshResult<IReadOnlyList<string>>> ListNamesAsync(string remoteDirectory, CancellationToken cancellationToken)
    {
        try
        {
            var names = new List<string>();
            await foreach (var entry in Client.ListDirectoryAsync(remoteDirectory, cancellationToken))
            {
                if (entry.Name is not (CurrentDirectoryName or ParentDirectoryName))
                {
                    names.Add(entry.Name);
                }
            }

            return SshResult<IReadOnlyList<string>>.Success(names);
        }
        catch (Exception exception)
        {
            var error = SshExceptionMapper.Map(exception, $"Listing {remoteDirectory}");
            return SshResult<IReadOnlyList<string>>.Failure(error);
        }
    }

    public ValueTask DisposeAsync()
    {
        connection.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task ChangeModeAsync(string remotePath, int mode, CancellationToken cancellationToken)
    {
        var octalDigits = SftpAttributesMapper.ToOctalDigits(mode);
        return Task.Run(() => Client.ChangePermissions(remotePath, octalDigits), cancellationToken);
    }
}
