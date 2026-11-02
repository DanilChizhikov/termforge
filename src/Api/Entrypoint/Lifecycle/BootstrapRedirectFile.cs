using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Encodings.Web;
using DTech.Entrypoint.Security;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class BootstrapRedirectFile : IDisposable
{
    private readonly Lock _gate = new();
    private readonly BootstrapToken _token;
    private readonly TimeProvider _timeProvider;
    private string? _directory;
    private ITimer? _expiry;

    public BootstrapRedirectFile(BootstrapToken token, TimeProvider timeProvider)
    {
        _token = token;
        _timeProvider = timeProvider;
        _token.Consumed += Delete;
    }

    public Uri Create(Uri destination)
    {
        lock (_gate)
        {
            Delete();
            var directoryName = $"termforge-launch-{Guid.NewGuid():N}";
            var parent = OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Path.GetTempPath();
            _directory = Path.Combine(parent, directoryName);
            try
            {
                CreatePrivateDirectory(_directory);
                var path = Path.Combine(_directory, "launch.html");
                var encodedDestination = HtmlEncoder.Default.Encode(destination.AbsoluteUri);
                var html = $"<!doctype html><meta charset=\"utf-8\"><meta name=\"referrer\" content=\"no-referrer\"><meta http-equiv=\"refresh\" content=\"0;url={encodedDestination}\"><title>TermForge</title><a href=\"{encodedDestination}\">Open TermForge</a>";
                var fileOptions = new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                };
                if (!OperatingSystem.IsWindows())
                {
                    fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                }

                using (var stream = new FileStream(path, fileOptions))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(html);
                }

                var remainingLifetime = _token.RemainingLifetime;
                _expiry = _timeProvider.CreateTimer(OnExpired, null, remainingLifetime, Timeout.InfiniteTimeSpan);
                return new Uri(path);
            }
            catch
            {
                Delete();
                throw;
            }
        }
    }

    public void Delete()
    {
        lock (_gate)
        {
            _expiry?.Dispose();
            _expiry = null;
            if (_directory is null)
            {
                return;
            }

            try
            {
                Directory.Delete(_directory, true);
                _directory = null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public void Dispose()
    {
        _token.Consumed -= Delete;
        Delete();
    }

    private void OnExpired(object? state)
    {
        Delete();
    }

    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            CreateWindowsDirectory(path);
            return;
        }

        Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [SupportedOSPlatform("windows")]
    private static void CreateWindowsDirectory(string path)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("The current Windows user is unavailable.");
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(user);
        var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var access = new FileSystemAccessRule(user, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow);
        security.AddAccessRule(access);
        var directory = new DirectoryInfo(path);
        directory.Create(security);
    }
}
