using DTech.Core.Files;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace DTech.Ssh.Files;

internal sealed class SftpEntryMapper(SftpClient client)
{
    public async Task<RemoteFileEntry> MapAsync(ISftpFile file, CancellationToken cancellationToken)
    {
        var kind = ToKind(file);
        var entry = new RemoteFileEntry(file.Name, file.FullName, kind)
        {
            Size = file.Length,
            LastWriteTimeUtc = file.LastWriteTimeUtc,
            Permissions = FormatPermissions(file),
        };
        if (kind != RemoteFileKind.SymbolicLink)
        {
            return entry;
        }

        return await DescribeLinkAsync(entry, cancellationToken);
    }

    private async Task<RemoteFileEntry> DescribeLinkAsync(RemoteFileEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var target = await client.GetAttributesAsync(entry.FullPath, cancellationToken);
            var targetKind = target.IsDirectory ? RemoteFileKind.Directory : target.IsRegularFile ? RemoteFileKind.File : RemoteFileKind.Other;
            return entry with { LinkTargetKind = targetKind };
        }
        catch (SftpPathNotFoundException)
        {
            return entry with { IsBrokenLink = true };
        }
        catch (SftpPermissionDeniedException)
        {
            return entry;
        }
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

    private static string FormatPermissions(ISftpFile file)
    {
        var mode = UnixFileMode.None;
        mode |= file.OwnerCanRead ? UnixFileMode.UserRead : UnixFileMode.None;
        mode |= file.OwnerCanWrite ? UnixFileMode.UserWrite : UnixFileMode.None;
        mode |= file.OwnerCanExecute ? UnixFileMode.UserExecute : UnixFileMode.None;
        mode |= file.GroupCanRead ? UnixFileMode.GroupRead : UnixFileMode.None;
        mode |= file.GroupCanWrite ? UnixFileMode.GroupWrite : UnixFileMode.None;
        mode |= file.GroupCanExecute ? UnixFileMode.GroupExecute : UnixFileMode.None;
        mode |= file.OthersCanRead ? UnixFileMode.OtherRead : UnixFileMode.None;
        mode |= file.OthersCanWrite ? UnixFileMode.OtherWrite : UnixFileMode.None;
        mode |= file.OthersCanExecute ? UnixFileMode.OtherExecute : UnixFileMode.None;
        return PermissionFormatter.Format(mode);
    }
}
