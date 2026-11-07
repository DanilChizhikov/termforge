namespace DTech.Entrypoint.Updates.Installation;

internal static class UpdateStartup
{
    public static FileStream? AcquireApplicationLease(UpdateJournal journal)
    {
        var executable = Path.Combine(journal.InstallationDirectory, UpdateProcess.ExecutableName);
        var processPath = Environment.ProcessPath is { } path ? UpdateFiles.NormalizePath(path) : null;
        if (processPath != executable || File.Exists(Path.Combine(journal.InstallationDirectory, "TermForge.dll"))) return null;
        try
        {
            UpdateFiles.CreatePrivateDirectory(journal.Root);
            var lease = Path.Combine(journal.Root, "application.lock");
            return new FileStream(lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static async Task<bool> TryRunWorkerAsync(string[] args)
    {
        if (args.Length == 0 || args[0] != "--update-worker") return false;
        if (args.Length != 3) throw new ArgumentException("The update worker requires an installation directory and operation identifier.");
        var journal = new UpdateJournal(args[1]);
        var worker = new UpdateWorker(journal);
        await worker.RunAsync(args[2]);
        return true;
    }

    public static UpdateJob? FindRecovery(UpdateJournal journal)
    {
        var job = journal.Active();
        if (!journal.NeedsRecovery(job)) return null;
        var candidate = Environment.GetEnvironmentVariable("TERMFORGE_UPDATE_MODE") == "candidate"
            && Environment.GetEnvironmentVariable("TERMFORGE_UPDATE_ID") == job!.Id && job.Phase == "starting";
        if (candidate) return null;
        var recoveryLaunch = Environment.GetEnvironmentVariable("TERMFORGE_UPDATE_MODE") == "recovery"
            && Environment.GetEnvironmentVariable("TERMFORGE_UPDATE_ID") == job!.Id && job.Phase == "recovery";
        if (recoveryLaunch) return job;
        if (UpdateProcess.IsAlive(job!.ParentProcessId, job.ParentStartTicks) || UpdateProcess.IsAlive(job.ChildProcessId, job.ChildStartTicks))
            throw new IOException("An update process is still running. Wait for it to finish before starting another instance.");
        var lockPath = Path.Combine(journal.Root, "worker.lock");
        using var workerLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        return job;
    }

    public static void ConfirmStartup(UpdateJournal journal)
    {
        if (Environment.GetEnvironmentVariable("TERMFORGE_UPDATE_MODE") != "candidate") return;
        var id = Environment.GetEnvironmentVariable("TERMFORGE_UPDATE_ID");
        if (id is null) return;
        var job = journal.Active();
        if (job?.Id != id || job.Phase != "starting") throw new IOException("The update startup transaction is no longer active.");
        var provider = new AssemblyVersionProvider();
        var expectedVersion = job.Mode == "restored" ? job.CurrentVersion : job.TargetVersion;
        if (provider.Current.ToString() != expectedVersion)
            throw new IOException("The running application version does not match the verified update.");
        journal.MarkReady(id);
    }
}
