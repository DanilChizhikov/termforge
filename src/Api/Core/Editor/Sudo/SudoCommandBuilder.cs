using System.Globalization;
using DTech.Core.Diagnostics;

namespace DTech.Core.Editor.Sudo;

public sealed class SudoCommandBuilder
{
    public const string StatFormat = "%f %s %Y %u %g";
    public const string NoOperation = "true";

    private const string NonInteractivePrefix = "LC_ALL=C sudo -n";
    private const string PasswordPrefix = "LC_ALL=C sudo -S -p ''";
    private const int PermissionMask = 0b111_111_111_111;
    private const int OctalBase = 8;

    private const string StageScript =
        "umask 077; staged=$(mktemp) || exit 1; if cat > \"$staged\"; then printf '%s' \"$staged\"; else rm -f -- \"$staged\"; exit 1; fi";

    private const string InstallScript =
        "exec </dev/null; if [ -e \"$2\" ] || [ -h \"$2\" ]; then echo \"$2: File exists\" >&2; exit 1; fi; install -o \"$4\" -g \"$5\" -m \"$3\" -- \"$1\" \"$2\"";

    private const string OverwriteScript = "exec </dev/null; cat -- \"$1\" > \"$2\"";

    private const string MoveScript =
        "exec </dev/null; if [ -d \"$2\" ] && [ ! -h \"$2\" ]; then echo \"$2: Is a directory\" >&2; exit 1; fi; mv -f -- \"$1\" \"$2\"";

    private const string ListScript =
        "exec </dev/null; if [ ! -d \"$1\" ]; then echo \"$1: No such file or directory\" >&2; exit 1; fi; cd \"$1\" || exit 1; for name in .* *; do case $name in .|..) continue ;; esac; if [ -f \"$name\" ] && [ ! -h \"$name\" ]; then printf '%s\\0' \"$name\"; fi; done";

    public string Probe()
    {
        return $"{NonInteractivePrefix} {NoOperation}";
    }

    public string Elevate(bool withPassword, string command)
    {
        var prefix = withPassword ? PasswordPrefix : NonInteractivePrefix;
        return $"{prefix} {command}";
    }

    public string WithCleanup(string command, string stagedPath)
    {
        var quotedPath = ShellArgument.Quote(stagedPath);
        return $"{command}; status=$?; rm -f -- {quotedPath}; exit $status";
    }

    public string StageUpload()
    {
        return StageScript;
    }

    public string Stat(string path)
    {
        var quotedFormat = ShellArgument.Quote(StatFormat);
        var quotedPath = ShellArgument.Quote(path);
        return $"stat -c {quotedFormat} -- {quotedPath}";
    }

    public string ReadHead(string path, long byteCount)
    {
        var count = byteCount.ToString(CultureInfo.InvariantCulture);
        var quotedPath = ShellArgument.Quote(path);
        return $"head -c {count} -- {quotedPath}";
    }

    public string Install(StagedInstall install)
    {
        var mode = FormatMode(install.Mode);
        var userId = install.Owner.UserId.ToString(CultureInfo.InvariantCulture);
        var groupId = install.Owner.GroupId.ToString(CultureInfo.InvariantCulture);
        var quotedScript = ShellArgument.Quote(InstallScript);
        var quotedStagedPath = ShellArgument.Quote(install.StagedPath);
        var quotedTargetPath = ShellArgument.Quote(install.TargetPath);
        return $"sh -c {quotedScript} termforge-install {quotedStagedPath} {quotedTargetPath} {mode} {userId} {groupId}";
    }

    public string Overwrite(string stagedPath, string targetPath)
    {
        var quotedScript = ShellArgument.Quote(OverwriteScript);
        var quotedStagedPath = ShellArgument.Quote(stagedPath);
        var quotedTargetPath = ShellArgument.Quote(targetPath);
        return $"sh -c {quotedScript} termforge-overwrite {quotedStagedPath} {quotedTargetPath}";
    }

    public string Move(string fromPath, string toPath)
    {
        var quotedScript = ShellArgument.Quote(MoveScript);
        var quotedFromPath = ShellArgument.Quote(fromPath);
        var quotedToPath = ShellArgument.Quote(toPath);
        return $"sh -c {quotedScript} termforge-move {quotedFromPath} {quotedToPath}";
    }

    public string ChangeOwner(RemoteOwnership ownership)
    {
        var userId = ownership.UserId.ToString(CultureInfo.InvariantCulture);
        var groupId = ownership.GroupId.ToString(CultureInfo.InvariantCulture);
        var quotedPath = ShellArgument.Quote(ownership.Path);
        return $"chown -- {userId}:{groupId} {quotedPath}";
    }

    public string Delete(string path)
    {
        var quotedPath = ShellArgument.Quote(path);
        return $"rm -f -- {quotedPath}";
    }

    public string ListNames(string directory)
    {
        var quotedScript = ShellArgument.Quote(ListScript);
        var quotedDirectory = ShellArgument.Quote(directory);
        return $"sh -c {quotedScript} termforge-list {quotedDirectory}";
    }

    private static string FormatMode(int mode)
    {
        var permissions = mode & PermissionMask;
        return Convert.ToString(permissions, OctalBase);
    }
}
