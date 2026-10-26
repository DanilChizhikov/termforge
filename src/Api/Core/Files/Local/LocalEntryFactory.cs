namespace DTech.Core.Files.Local;

public sealed class LocalEntryFactory
{
    public FileEntry Create(FileSystemInfo info)
    {
        var linkTarget = ReadLinkTarget(info);
        var kind = linkTarget is not null ? FileEntryKind.SymbolicLink : ReadKind(info);
        var entry = new FileEntry(info.Name, info.FullName, kind)
        {
            Size = ReadSize(info),
            ModifiedUtc = ReadModified(info),
            Permissions = ReadPermissions(info),
        };
        return linkTarget is null ? entry : DescribeLink(entry, info);
    }

    private static FileEntry DescribeLink(FileEntry entry, FileSystemInfo info)
    {
        try
        {
            var finalTarget = info.ResolveLinkTarget(true);
            if (finalTarget is null)
            {
                return entry with { IsBrokenLink = true };
            }

            var targetPath = finalTarget.FullName;
            if (Directory.Exists(targetPath))
            {
                return entry with { LinkTargetKind = FileEntryKind.Directory };
            }

            var targetIsFile = File.Exists(targetPath);
            return targetIsFile ? entry with { LinkTargetKind = FileEntryKind.File } : entry with { IsBrokenLink = true };
        }
        catch (IOException)
        {
            return entry with { IsBrokenLink = true };
        }
        catch (UnauthorizedAccessException)
        {
            return entry with { IsBrokenLink = true };
        }
    }

    private static string? ReadLinkTarget(FileSystemInfo info)
    {
        try
        {
            return info.LinkTarget;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static FileEntryKind ReadKind(FileSystemInfo info)
    {
        try
        {
            if (info.Attributes.HasFlag(FileAttributes.Directory))
            {
                return FileEntryKind.Directory;
            }

            var isRegular = info is FileInfo && !info.Attributes.HasFlag(FileAttributes.Device);
            return isRegular ? FileEntryKind.File : FileEntryKind.Other;
        }
        catch (IOException)
        {
            return FileEntryKind.Other;
        }
        catch (UnauthorizedAccessException)
        {
            return FileEntryKind.Other;
        }
    }

    private static long ReadSize(FileSystemInfo info)
    {
        try
        {
            return info is FileInfo file ? file.Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static DateTime? ReadModified(FileSystemInfo info)
    {
        try
        {
            return info.LastWriteTimeUtc;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? ReadPermissions(FileSystemInfo info)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return PermissionFormatter.Format(info.UnixFileMode);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
