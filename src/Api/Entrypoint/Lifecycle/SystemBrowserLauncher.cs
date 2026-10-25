using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class SystemBrowserLauncher(IOptions<LifecycleOptions> options) : IBrowserLauncher
{
    private const string MacOsOpenCommand = "open";
    private const string LinuxOpenCommand = "xdg-open";

    public void Open(Uri address)
    {
        var startInfo = CreateStartInfo(address.AbsoluteUri);
        using var process = Process.Start(startInfo);
    }

    private ProcessStartInfo CreateStartInfo(string url)
    {
        var browserCommand = options.Value.BrowserCommand;
        if (!string.IsNullOrWhiteSpace(browserCommand))
        {
            return CreateCommandStartInfo(browserCommand, url);
        }

        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo(url) { UseShellExecute = true };
        }

        var openCommand = OperatingSystem.IsMacOS() ? MacOsOpenCommand : LinuxOpenCommand;
        return CreateCommandStartInfo(openCommand, url);
    }

    private static ProcessStartInfo CreateCommandStartInfo(string command, string url)
    {
        var startInfo = new ProcessStartInfo(command) { UseShellExecute = false };
        startInfo.ArgumentList.Add(url);
        return startInfo;
    }
}
