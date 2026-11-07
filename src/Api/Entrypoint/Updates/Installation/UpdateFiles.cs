using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DTech.Entrypoint.Updates.Installation;

internal static class UpdateFiles
{
    public static string NormalizePath(string path)
    {
        var full = Path.GetFullPath(path);
        if (!OperatingSystem.IsMacOS()) return full;
        string[] aliases = ["/var", "/tmp", "/etc"];
        for (var i = 0; i < aliases.Length; i++)
        {
            var alias = aliases[i];
            if ((full == alias || full.StartsWith(alias + "/", StringComparison.Ordinal))
                && Directory.ResolveLinkTarget(alias, true)?.FullName == $"/private{alias}")
                return $"/private{full}";
        }
        return full;
    }

    public static void CreatePrivateDirectory(string path)
    {
        RejectLinksInPath(path);
        if (OperatingSystem.IsWindows()) CreateWindowsDirectory(path);
        else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [SupportedOSPlatform("windows")]
    private static void CreateWindowsDirectory(string path)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new IOException("The current user is unavailable.");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(user);
        var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var rule = new FileSystemAccessRule(user, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow);
        security.AddAccessRule(rule);
        var directory = new DirectoryInfo(path);
        directory.Create(security);
    }

    public static void RejectLinksInPath(string path)
    {
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrEmpty(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                var systemAlias = OperatingSystem.IsMacOS() && current is "/var" or "/tmp" or "/etc"
                    && Directory.ResolveLinkTarget(current, true)?.FullName == $"/private{current}";
                if (!systemAlias) throw new InvalidDataException("Updates cannot operate on symbolic links or junctions.");
            }
            current = Path.GetDirectoryName(current);
        }
    }

    public static long Size(string path)
    {
        RejectLinksInPath(path);
        if (File.Exists(path)) return new FileInfo(path).Length;
        if (!Directory.Exists(path)) return 0;
        long size = 0;
        foreach (var entry in Directory.EnumerateFileSystemEntries(path)) size = checked(size + Size(entry));
        return size;
    }

    public static void RequireSpace(string path, long bytes)
    {
        var fullPath = Path.GetFullPath(path);
        var drive = DriveInfo.GetDrives()
            .Where(candidate => fullPath.StartsWith(candidate.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Name.Length).FirstOrDefault();
        if (bytes < 0 || bytes > long.MaxValue - 64L * 1024 * 1024 || drive is null || drive.AvailableFreeSpace < bytes + 64L * 1024 * 1024)
            throw new IOException("There is not enough free space for the update and its backup.");
    }

    public static void CopyDirectory(string source, string destination)
    {
        RejectLinksInPath(source);
        CreatePrivateDirectory(destination);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            RejectLinksInPath(entry);
            var target = Path.Combine(destination, Path.GetFileName(entry));
            if (Directory.Exists(entry)) CopyDirectory(entry, target);
            else CopyFile(entry, target);
        }
    }

    public static void CopyFile(string source, string destination)
    {
        RejectLinksInPath(source);
        RejectLinksInPath(destination);
        var parent = Path.GetDirectoryName(destination)!;
        CreatePrivateDirectory(parent);
        File.Copy(source, destination, true);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(destination, File.GetUnixFileMode(source));
    }

    public static void Delete(string path)
    {
        RejectLinksInPath(path);
        if (Directory.Exists(path)) Directory.Delete(path, true);
        else File.Delete(path);
    }

    public static void CopyApplication(string source, string destination)
    {
        CreatePrivateDirectory(destination);
        var webSource = Path.Combine(source, "wwwroot");
        var webTarget = Path.Combine(destination, "wwwroot");
        CopyDirectory(webSource, webTarget);
        var files = Directory.GetFiles(source);
        for (var i = 0; i < files.Length; i++)
        {
            var name = Path.GetFileName(files[i]);
            if (!IsApplicationFile(name)) continue;
            var target = Path.Combine(destination, name);
            CopyFile(files[i], target);
        }
    }

    public static void ReplaceApplication(string source, string destination)
    {
        RejectLinksInPath(source);
        RejectLinksInPath(destination);
        var webTarget = Path.Combine(destination, "wwwroot");
        Delete(webTarget);
        var webSource = Path.Combine(source, "wwwroot");
        CopyDirectory(webSource, webTarget);
        var files = Directory.GetFiles(source);
        for (var i = 0; i < files.Length; i++)
        {
            var name = Path.GetFileName(files[i]);
            if (!IsApplicationFile(name)) continue;
            var target = Path.Combine(destination, name);
            var temporary = $"{target}.termforge-update";
            CopyFile(files[i], temporary);
            File.Move(temporary, target, true);
        }
    }

    private static bool IsApplicationFile(string name)
    {
        return name == UpdateProcess.ExecutableName || name is "TermForge.staticwebassets.endpoints.json" or "web.config"
            || (name.StartsWith("appsettings", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal));
    }

    public static void SaveData(UpdateDataPath[] targets, string destination)
    {
        CreatePrivateDirectory(destination);
        for (var i = 0; i < targets.Length; i++)
        {
            var source = targets[i].Path;
            RejectLinksInPath(source);
            var target = Path.Combine(destination, i.ToString());
            if (targets[i].Directory && Directory.Exists(source)) CopyDirectory(source, target);
            else if (File.Exists(source)) CopyFile(source, target);
        }
        File.WriteAllText(Path.Combine(destination, "complete"), "complete");
    }

    public static void RestoreData(UpdateDataPath[] targets, string source)
    {
        if (!File.Exists(Path.Combine(source, "complete"))) throw new IOException("The data backup is incomplete.");
        for (var i = 0; i < targets.Length; i++)
        {
            var target = targets[i].Path;
            RejectLinksInPath(target);
            var saved = Path.Combine(source, i.ToString());
            if (targets[i].Directory)
            {
                Delete(target);
                if (Directory.Exists(saved)) CopyDirectory(saved, target);
            }
            else if (File.Exists(saved))
            {
                var temporary = $"{target}.termforge-restore";
                CopyFile(saved, temporary);
                File.Move(temporary, target, true);
            }
            else Delete(target);
        }
    }
}
