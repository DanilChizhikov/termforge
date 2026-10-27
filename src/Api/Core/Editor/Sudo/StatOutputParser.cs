using System.Globalization;
using DTech.Core.Files;

namespace DTech.Core.Editor.Sudo;

public static class StatOutputParser
{
    private const int FieldCount = 5;
    private const int FileTypeMask = 0xF000;
    private const int RegularFileType = 0x8000;
    private const int DirectoryType = 0x4000;
    private const int SymbolicLinkType = 0xA000;
    private const int PermissionMask = 0xFFF;
    private const char FieldSeparator = ' ';

    public static RemoteFileAttributes? Parse(string output)
    {
        var fields = output.Trim().Split(FieldSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != FieldCount)
        {
            return null;
        }

        var parsed = int.TryParse(fields[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rawMode)
            & long.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var size)
            & long.TryParse(fields[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var modifiedSeconds)
            & int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out var userId)
            & int.TryParse(fields[4], NumberStyles.None, CultureInfo.InvariantCulture, out var groupId);
        if (!parsed)
        {
            return null;
        }

        var kind = ToKind(rawMode);
        var modifiedUtc = DateTime.UnixEpoch.AddSeconds(modifiedSeconds);
        return new RemoteFileAttributes(kind, size, modifiedUtc)
        {
            Mode = rawMode & PermissionMask,
            UserId = userId,
            GroupId = groupId,
        };
    }

    private static RemoteFileKind ToKind(int rawMode)
    {
        return (rawMode & FileTypeMask) switch
        {
            RegularFileType => RemoteFileKind.File,
            DirectoryType => RemoteFileKind.Directory,
            SymbolicLinkType => RemoteFileKind.SymbolicLink,
            _ => RemoteFileKind.Other,
        };
    }
}
