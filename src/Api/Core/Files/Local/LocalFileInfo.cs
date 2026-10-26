namespace DTech.Core.Files.Local;

public static class LocalFileInfo
{
    public static FileSystemInfo? Open(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (info.Exists)
        {
            return info;
        }

        return HasLinkTarget(info) ? info : null;
    }

    private static bool HasLinkTarget(FileSystemInfo info)
    {
        try
        {
            return info.LinkTarget is not null;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
