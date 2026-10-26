namespace DTech.Core.Files.Local;

public sealed class LocalRootsProvider
{
    public IReadOnlyList<LocalRoot> GetRoots()
    {
        var roots = new List<LocalRoot>();
        var home = GetHomeDirectory();
        if (home.Length > 0)
        {
            var homeRoot = new LocalRoot("Home", home);
            roots.Add(homeRoot);
        }

        if (OperatingSystem.IsWindows())
        {
            AddDrives(roots);
        }
        else
        {
            var filesystemRoot = new LocalRoot("/", "/");
            roots.Add(filesystemRoot);
        }

        return roots;
    }

    public string GetHomeDirectory()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static void AddDrives(List<LocalRoot> roots)
    {
        var drives = DriveInfo.GetDrives();
        for (var i = 0; i < drives.Length; i++)
        {
            var drive = drives[i];
            if (!drive.IsReady)
            {
                continue;
            }

            var root = new LocalRoot(drive.Name, drive.Name);
            roots.Add(root);
        }
    }
}
