using System.ComponentModel;
using System.Diagnostics;

namespace DTech.Ssh.IntegrationTests.Harness;

internal sealed class LocalSshAgent : IAsyncDisposable
{
    private const string AuthSocketVariable = "SSH_AUTH_SOCK";

    private static readonly TimeSpan SocketWaitTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SocketPollInterval = TimeSpan.FromMilliseconds(100);

    private readonly Process _agentProcess;

    private LocalSshAgent(Process agentProcess, string socketPath)
    {
        _agentProcess = agentProcess;
        SocketPath = socketPath;
    }

    public string SocketPath { get; }

    public static async Task<LocalSshAgent?> TryStartAsync(string workingDirectory)
    {
        var socketName = $"agent-{Guid.NewGuid():N}"[..14];
        var socketPath = Path.Combine(workingDirectory, $"{socketName}.sock");
        var startInfo = new ProcessStartInfo("ssh-agent")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-D");
        startInfo.ArgumentList.Add("-a");
        startInfo.ArgumentList.Add(socketPath);
        var agentProcess = TryStart(startInfo);
        if (agentProcess is null)
        {
            return null;
        }

        var agent = new LocalSshAgent(agentProcess, socketPath);
        await agent.WaitForSocketAsync();
        return agent;
    }

    public async Task AddIdentityAsync(string privateKeyPath)
    {
        var startInfo = new ProcessStartInfo("ssh-add")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(privateKeyPath);
        startInfo.Environment[AuthSocketVariable] = SocketPath;
        using var process = Process.Start(startInfo)!;
        var errorOutput = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ssh-add failed with {process.ExitCode}: {errorOutput}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_agentProcess.HasExited)
        {
            _agentProcess.Kill(true);
            await _agentProcess.WaitForExitAsync();
        }

        _agentProcess.Dispose();
        if (File.Exists(SocketPath))
        {
            File.Delete(SocketPath);
        }
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

    private async Task WaitForSocketAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        while (!File.Exists(SocketPath))
        {
            if (_agentProcess.HasExited || stopwatch.Elapsed > SocketWaitTimeout)
            {
                if (!_agentProcess.HasExited)
                {
                    _agentProcess.Kill(true);
                }

                var errorOutput = await _agentProcess.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"ssh-agent did not create {SocketPath}: {errorOutput}");
            }

            await Task.Delay(SocketPollInterval);
        }
    }
}
