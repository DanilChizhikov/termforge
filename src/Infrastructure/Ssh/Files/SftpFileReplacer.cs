using DTech.Core.Files;
using Renci.SshNet;

namespace DTech.Ssh.Files;

internal sealed class SftpFileReplacer(ISftpClient client)
{
    public async Task ReplaceAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(() => client.RenameFile(request.FromPath, request.ToPath, true), cancellationToken);
        }
        catch (NotSupportedException)
        {
            await ReplaceWithoutPosixRenameAsync(request, cancellationToken);
        }
    }

    private async Task ReplaceWithoutPosixRenameAsync(RemoteRenameRequest request, CancellationToken cancellationToken)
    {
        if (!await client.ExistsAsync(request.ToPath, cancellationToken))
        {
            await client.RenameFileAsync(request.FromPath, request.ToPath, cancellationToken);
            return;
        }

        var asidePath = $"{request.ToPath}.termforge-{Guid.NewGuid():N}.old";
        cancellationToken.ThrowIfCancellationRequested();
        await client.RenameFileAsync(request.ToPath, asidePath, CancellationToken.None);
        try
        {
            await client.RenameFileAsync(request.FromPath, request.ToPath, cancellationToken);
        }
        catch (Exception replaceError)
        {
            try
            {
                await client.RenameFileAsync(asidePath, request.ToPath, CancellationToken.None);
            }
            catch (Exception restoreError)
            {
                throw new IOException($"Replacement failed and the original file could not be restored. It is preserved at {asidePath}.",
                    new AggregateException(replaceError, restoreError));
            }

            throw;
        }

        await client.DeleteFileAsync(asidePath, CancellationToken.None);
    }
}
