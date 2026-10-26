using DTech.Core.Files;
using Renci.SshNet;

namespace DTech.Ssh.Files;

internal sealed class SftpFileReplacer(SftpClient client)
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
        if (await client.ExistsAsync(request.ToPath, cancellationToken))
        {
            await client.DeleteFileAsync(request.ToPath, cancellationToken);
        }

        await client.RenameFileAsync(request.FromPath, request.ToPath, cancellationToken);
    }
}
