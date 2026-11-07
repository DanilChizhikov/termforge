using System.Diagnostics;

namespace DTech.Entrypoint.Updates.Installation;

internal static class UpdateProcess
{
    public static string ExecutableName => OperatingSystem.IsWindows() ? "TermForge.exe" : "TermForge";

    public static bool IsAlive(int id, long startTicks)
    {
        if (id <= 0) return false;
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited && Math.Abs(process.StartTime.ToUniversalTime().Ticks - startTicks) < TimeSpan.TicksPerSecond;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    public static Process Launch(UpdateJob job, string? mode = null, bool useHelper = false)
    {
        var journal = new UpdateJournal(job.InstallationDirectory);
        var application = useHelper ? Path.Combine(journal.JobDirectory(job.Id), "helper") : job.InstallationDirectory;
        var executable = Path.Combine(application, ExecutableName);
        var start = new ProcessStartInfo(executable) { WorkingDirectory = job.WorkingDirectory, UseShellExecute = false };
        for (var i = 0; i < job.Arguments.Length; i++) start.ArgumentList.Add(job.Arguments[i]);
        if (mode is not null)
        {
            start.Environment["TERMFORGE_UPDATE_ID"] = job.Id;
            start.Environment["TERMFORGE_UPDATE_MODE"] = mode;
            if (mode == "recovery") start.Environment["TERMFORGE_UPDATE_INSTALLATION"] = job.InstallationDirectory;
        }
        else
        {
            start.Environment.Remove("TERMFORGE_UPDATE_ID");
            start.Environment.Remove("TERMFORGE_UPDATE_MODE");
            start.Environment.Remove("TERMFORGE_UPDATE_INSTALLATION");
        }
        return Process.Start(start) ?? throw new IOException("TermForge could not be started.");
    }

    public static void LaunchWorker(UpdateJournal journal, UpdateJob job)
    {
        var helperDirectory = Path.Combine(journal.JobDirectory(job.Id), "helper");
        if (!Directory.Exists(helperDirectory)) UpdateFiles.CopyApplication(job.InstallationDirectory, helperDirectory);
        var executable = Path.Combine(helperDirectory, ExecutableName);
        var start = new ProcessStartInfo(executable) { WorkingDirectory = helperDirectory, UseShellExecute = false };
        start.ArgumentList.Add("--update-worker");
        start.ArgumentList.Add(job.InstallationDirectory);
        start.ArgumentList.Add(job.Id);
        start.Environment.Remove("TERMFORGE_UPDATE_ID");
        start.Environment.Remove("TERMFORGE_UPDATE_MODE");
        start.Environment.Remove("TERMFORGE_UPDATE_INSTALLATION");
        using var process = Process.Start(start) ?? throw new IOException("The update worker could not be started.");
    }

    public static async Task WaitForExitAsync(UpdateJob job)
    {
        var deadline = DateTime.UtcNow.AddMinutes(2);
        while (IsAlive(job.ParentProcessId, job.ParentStartTicks))
        {
            if (DateTime.UtcNow >= deadline) throw new IOException("TermForge did not stop; application files were not replaced.");
            await Task.Delay(100);
        }
    }
}
