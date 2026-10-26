using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace DTech.Ssh.Files;

internal sealed class SftpEntryLocator(SftpClient client)
{
    private const string RootPath = "/";

    public async Task<ISftpFile> FindAsync(string remotePath, CancellationToken cancellationToken)
    {
        var trimmed = remotePath.Length > 1 ? remotePath.TrimEnd('/') : remotePath;
        var lastSeparator = trimmed.LastIndexOf('/');
        if (trimmed == RootPath || lastSeparator < 0)
        {
            return await client.GetAsync(remotePath, cancellationToken);
        }

        var parent = lastSeparator == 0 ? RootPath : trimmed[..lastSeparator];
        var name = trimmed[(lastSeparator + 1)..];
        await foreach (var candidate in client.ListDirectoryAsync(parent, cancellationToken))
        {
            if (candidate.Name == name)
            {
                return candidate;
            }
        }

        throw new SftpPathNotFoundException($"No such file: {remotePath}");
    }
}
