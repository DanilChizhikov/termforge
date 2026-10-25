using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class SystemOpenSshProbeTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);
    private static readonly Regex SizePattern = new(@"SIZE\[([0-9 ]*)\]");
    private static readonly Regex TtyPattern = new(@"TTY\[(/dev/pts/\d+)\]");

    [TestMethod]
    public async Task SystemSshProcessWithoutLocalPty_GetsRemotePtyWithoutWindowSize()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var server = cluster.Target;
        var knownHostsPath = Path.Combine(cluster.KeyDirectory, $"known_hosts_{Guid.NewGuid():N}");
        var knownHostsLine = OpenSshPublicKey.ToKnownHostsLine(server.Identity.HostPublicKeyLine, server.Endpoint);
        await File.WriteAllTextAsync(knownHostsPath, $"{knownHostsLine}\n");
        var startInfo = CreateSshStartInfo(server, knownHostsPath);

        using var process = TryStart(startInfo);
        if (process is null)
        {
            Assert.Inconclusive("OpenSSH client 'ssh' is not installed on this machine");
        }

        await process!.StandardInput.WriteAsync("printf 'TTY[%s]\\n' \"$(tty)\"; printf 'SIZE[%s]\\n' \"$(stty size 2>/dev/null)\"; exit\n");
        await process.StandardInput.FlushAsync();
        using var timeout = new CancellationTokenSource(ProcessTimeout);
        var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);
        var ttyMatch = TtyPattern.Match(output);
        var sizeMatch = SizePattern.Match(output);

        ttyMatch.Success.Should().BeTrue(output);
        sizeMatch.Success.Should().BeTrue(output);
        sizeMatch.Groups[1].Value.Should().BeOneOf(string.Empty, "0 0");
    }

    private static ProcessStartInfo CreateSshStartInfo(SshServerContainer server, string knownHostsPath)
    {
        var startInfo = new ProcessStartInfo("ssh")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        string[] arguments =
        [
            "-tt",
            "-F", "none",
            "-o", "BatchMode=yes",
            "-o", "IdentitiesOnly=yes",
            "-o", "StrictHostKeyChecking=yes",
            "-o", $"UserKnownHostsFile={knownHostsPath}",
            "-i", server.Identity.PrivateKeyPath,
            "-p", $"{server.Endpoint.Port}",
            $"{SshServerContainer.UserName}@{server.Endpoint.Host}",
        ];
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static Process? TryStart(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
