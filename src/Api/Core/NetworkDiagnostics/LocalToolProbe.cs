using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace DTech.Core.NetworkDiagnostics;

internal static class LocalToolProbe
{
    public static async Task<NetworkProbeOutcome> RunAsync(NetworkProbe probe, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo
        {
            FileName = probe.Protocol == "ssh" ? "ssh-keyscan" : OperatingSystem.IsWindows() ? "tracert" : "traceroute",
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
        };
        start.Environment["LC_ALL"] = "C";
        string[] arguments = probe.Protocol == "ssh" ? ["-T", "3", "-p", probe.Port!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), probe.Host]
            : OperatingSystem.IsWindows() ? ["-d", "-h", "12", "-w", "300", probe.Host] : ["-n", "-m", "12", "-w", "1", probe.Host];
        for (var i = 0; i < arguments.Length; i++) start.ArgumentList.Add(arguments[i]);
        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            return new NetworkProbeOutcome(NetworkProbeOutcome.Unavailable, $"The required {start.FileName} utility is not available. No software was installed.");
        }
        var output = ReadBoundedAsync(process.StandardOutput, cancellationToken);
        var error = ReadBoundedAsync(process.StandardError, cancellationToken);
        var collected = Task.WhenAll(output, error);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            var streams = await collected;
            var text = streams[0];
            var diagnostic = streams[1];
            if (probe.Protocol == "ssh")
            {
                var result = new DTech.Core.Diagnostics.RemoteCommandResult(process.ExitCode, text, diagnostic);
                return RemoteNetworkCommands.Parse(probe, result);
            }
            var status = process.ExitCode != 0 ? NetworkProbeOutcome.Error : text.Contains('*') ? NetworkProbeOutcome.NoResponse : NetworkProbeOutcome.Responded;
            var detail = string.IsNullOrWhiteSpace(diagnostic) ? text : diagnostic;
            return new NetworkProbeOutcome(status, detail);
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(true); }
                catch (InvalidOperationException) { }
            }
            await process.WaitForExitAsync(CancellationToken.None);
            try { await collected; }
            catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException) { }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        var buffer = new char[1024];
        while (true)
        {
            var count = await reader.ReadAsync(buffer, cancellationToken);
            if (count == 0) return text.ToString();
            var kept = Math.Min(count, 16 * 1024 - text.Length);
            text.Append(buffer, 0, kept);
        }
    }
}
