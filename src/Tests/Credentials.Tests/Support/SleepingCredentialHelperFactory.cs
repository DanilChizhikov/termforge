using System.Collections.Concurrent;
using System.Diagnostics;
using DTech.Credentials.Native.Processes;

namespace DTech.Credentials.Tests.Support;

internal sealed class SleepingCredentialHelperFactory : ICredentialHelperProcessFactory
{
    private readonly ConcurrentQueue<int> _startedIds = new();

    public IReadOnlyList<int> StartedIds => _startedIds.ToArray();

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Process Start()
    {
        var info = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (OperatingSystem.IsWindows())
        {
            info.ArgumentList.Add("/c");
            info.ArgumentList.Add("ping -n 60 127.0.0.1 >nul");
        }
        else
        {
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add("exec sleep 60");
        }

        var process = Process.Start(info) ?? throw new InvalidOperationException("Test helper did not start.");
        _startedIds.Enqueue(process.Id);
        Started.TrySetResult();
        return process;
    }

    public bool AnyRunning()
    {
        foreach (var id in _startedIds)
        {
            try
            {
                using var process = Process.GetProcessById(id);
                if (!process.HasExited)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
            }
        }

        return false;
    }
}
