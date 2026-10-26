using System.Globalization;
using DTech.Core.Editor;
using DTech.Core.Files;
using Renci.SshNet.Sftp;

namespace DTech.Ssh.Editor;

internal static class SftpAttributesMapper
{
    private const int PermissionMask = 0b111_111_111_111;
    private const int OctalBase = 8;

    public static RemoteFileAttributes Map(SftpFileAttributes attributes)
    {
        var kind = ToKind(attributes);
        return new RemoteFileAttributes(kind, attributes.Size, attributes.LastWriteTimeUtc)
        {
            Mode = ToMode(attributes),
            UserId = attributes.UserId,
            GroupId = attributes.GroupId,
        };
    }

    public static short ToOctalDigits(int mode)
    {
        var octal = Convert.ToString(mode & PermissionMask, OctalBase);
        return short.Parse(octal, CultureInfo.InvariantCulture);
    }

    private static RemoteFileKind ToKind(SftpFileAttributes attributes)
    {
        if (attributes.IsSymbolicLink)
        {
            return RemoteFileKind.SymbolicLink;
        }

        if (attributes.IsDirectory)
        {
            return RemoteFileKind.Directory;
        }

        return attributes.IsRegularFile ? RemoteFileKind.File : RemoteFileKind.Other;
    }

    private static int ToMode(SftpFileAttributes attributes)
    {
        var mode = UnixFileMode.None;
        mode |= attributes.IsUIDBitSet ? UnixFileMode.SetUser : UnixFileMode.None;
        mode |= attributes.IsGroupIDBitSet ? UnixFileMode.SetGroup : UnixFileMode.None;
        mode |= attributes.IsStickyBitSet ? UnixFileMode.StickyBit : UnixFileMode.None;
        mode |= attributes.OwnerCanRead ? UnixFileMode.UserRead : UnixFileMode.None;
        mode |= attributes.OwnerCanWrite ? UnixFileMode.UserWrite : UnixFileMode.None;
        mode |= attributes.OwnerCanExecute ? UnixFileMode.UserExecute : UnixFileMode.None;
        mode |= attributes.GroupCanRead ? UnixFileMode.GroupRead : UnixFileMode.None;
        mode |= attributes.GroupCanWrite ? UnixFileMode.GroupWrite : UnixFileMode.None;
        mode |= attributes.GroupCanExecute ? UnixFileMode.GroupExecute : UnixFileMode.None;
        mode |= attributes.OthersCanRead ? UnixFileMode.OtherRead : UnixFileMode.None;
        mode |= attributes.OthersCanWrite ? UnixFileMode.OtherWrite : UnixFileMode.None;
        mode |= attributes.OthersCanExecute ? UnixFileMode.OtherExecute : UnixFileMode.None;
        return (int)mode;
    }
}
