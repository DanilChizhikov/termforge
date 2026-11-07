using System.Diagnostics;

namespace DTech.Entrypoint.Updates.Installation;

internal sealed class UpdateWorker(UpdateJournal journal)
{
    public async Task RunAsync(string id)
    {
        var job = journal.Read(id);
        var directory = journal.JobDirectory(id);
        UpdateFiles.CreatePrivateDirectory(journal.Root);
        var lockPath = Path.Combine(journal.Root, "worker.lock");
        await using var workerLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            await UpdateProcess.WaitForExitAsync(job);
            job = journal.Read(id);
            if (job.Phase == "aborted")
            {
                using var unchanged = UpdateProcess.Launch(job);
                return;
            }
            if (job.Phase != "stopping")
            {
                if (journal.NeedsRecovery(job)) await RecoverAsync(id, "An update was interrupted. Data recovery requires your confirmation.");
                return;
            }
            if (job.Mode == "restore")
            {
                await RestoreAsync(job);
                return;
            }

            var applicationLock = Path.Combine(journal.Root, "application.lock");
            using (var lease = new FileStream(applicationLock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var snapshot = Path.Combine(directory, "data");
                journal.Save(job with { Phase = "backingUp" });
                long bytes = 0;
                for (var i = 0; i < job.DataPaths.Length; i++) bytes = checked(bytes + UpdateFiles.Size(job.DataPaths[i].Path));
                UpdateFiles.RequireSpace(directory, bytes * 2);
                UpdateFiles.SaveData(job.DataPaths, snapshot);
                job = job with { DataBackupComplete = true, Phase = "backingUp" };
                journal.Save(job);
                var old = Path.Combine(directory, "old");
                var staged = Path.Combine(directory, "new");
                if (job.Mode == "update")
                {
                    var configurations = Directory.GetFiles(job.InstallationDirectory, "appsettings*.json");
                    for (var i = 0; i < configurations.Length; i++)
                    {
                        var target = Path.Combine(staged, Path.GetFileName(configurations[i]));
                        UpdateFiles.CopyFile(configurations[i], target);
                    }
                }
                UpdateFiles.CopyApplication(job.InstallationDirectory, old);
                journal.Save(job with { Phase = "switching" });
                UpdateFiles.ReplaceApplication(staged, job.InstallationDirectory);
                if (job.Mode == "rollback")
                {
                    var previous = journal.Read(job.RestoreId!);
                    var previousData = Path.Combine(journal.JobDirectory(previous.Id), "data");
                    UpdateFiles.RestoreData(previous.DataPaths, previousData);
                }
                job = job with { Phase = "starting" };
                journal.Save(job);
            }
            await StartAndConfirmAsync(job);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Update failed: {exception.GetType().Name}: {exception.Message}");
            if (journal.Read(id).Phase == "completed") return;
            if (UpdateProcess.IsAlive(job.ParentProcessId, job.ParentStartTicks)) return;
            await RecoverAsync(id, exception.Message);
        }
    }

    private async Task StartAndConfirmAsync(UpdateJob job)
    {
        var ready = Path.Combine(journal.JobDirectory(job.Id), "ready");
        File.Delete(ready);
        using var child = UpdateProcess.Launch(job, "candidate");
        job = job with { ChildProcessId = child.Id, ChildStartTicks = child.StartTime.ToUniversalTime().Ticks };
        journal.Save(job);
        var deadline = DateTime.UtcNow.AddSeconds(Math.Clamp(job.StartupTimeoutSeconds, 1, 300));
        while (!child.HasExited && DateTime.UtcNow < deadline)
        {
            if (File.Exists(ready))
            {
                var previous = journal.Previous();
                if (job.Mode == "update") journal.SetPrevious(job);
                else if (job.Mode == "rollback") journal.SetPrevious(null);
                journal.Save(job with { Phase = "completed", Error = null });
                if (job.Mode == "update" && previous is not null && previous.Id != job.Id)
                {
                    try { UpdateFiles.Delete(journal.JobDirectory(previous.Id)); }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        Console.Error.WriteLine("The older backup could not be removed; the new recovery point is valid.");
                    }
                }
                if (job.Mode == "rollback" && !job.SaveCurrentData)
                    UpdateFiles.Delete(Path.Combine(journal.JobDirectory(job.Id), "data"));
                return;
            }
            await Task.Delay(100);
        }
        var failure = child.HasExited
            ? $"The new version exited with code {child.ExitCode} before confirming startup."
            : $"The new version did not confirm startup within {job.StartupTimeoutSeconds} seconds.";
        if (!child.HasExited)
        {
            child.Kill(true);
            await child.WaitForExitAsync();
        }
        throw new IOException($"{failure} Data recovery requires your confirmation.");
    }

    public async Task RecoverAsync(string id, string error)
    {
        var job = journal.Read(id);
        if (UpdateProcess.IsAlive(job.ChildProcessId, job.ChildStartTicks))
        {
            using var child = Process.GetProcessById(job.ChildProcessId);
            child.Kill(true);
            await child.WaitForExitAsync();
        }
        var old = Path.Combine(journal.JobDirectory(id), "old");
        var useHelper = false;
        if (Directory.Exists(old) && (job.Phase is "switching" or "starting" || job.Mode == "restore"))
        {
            try
            {
                var lockPath = Path.Combine(journal.Root, "application.lock");
                using var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                UpdateFiles.ReplaceApplication(old, job.InstallationDirectory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                useHelper = true;
                error = $"{error} Application files could not be restored: {exception.Message}. Recovery is running from the retained helper.";
            }
        }
        job = job with { Phase = "recovery", Error = error, ChildProcessId = 0, ChildStartTicks = 0 };
        journal.Save(job);
        var previous = job.PreviousId is null ? null : journal.Read(job.PreviousId);
        journal.SetPrevious(previous);
        using var recovery = UpdateProcess.Launch(job, "recovery", useHelper);
        journal.Save(job with { ChildProcessId = recovery.Id, ChildStartTicks = recovery.StartTime.ToUniversalTime().Ticks });
    }

    private async Task RestoreAsync(UpdateJob job)
    {
        var lockPath = Path.Combine(journal.Root, "application.lock");
        using (var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            var directory = journal.JobDirectory(job.Id);
            var data = Path.Combine(directory, "data");
            var old = Path.Combine(directory, "old");
            if (job.DataBackupComplete && !File.Exists(Path.Combine(data, "complete")))
                throw new IOException("The data backup is missing or incomplete. Recovery was stopped without starting migrations.");
            if (job.SaveCurrentData)
            {
                var retained = Path.Combine(directory, "retained-data");
                if (!File.Exists(Path.Combine(retained, "complete")))
                {
                    UpdateFiles.Delete(retained);
                    UpdateFiles.SaveData(job.DataPaths, retained);
                }
            }
            if (Directory.Exists(old)) UpdateFiles.ReplaceApplication(old, job.InstallationDirectory);
            if (File.Exists(Path.Combine(data, "complete"))) UpdateFiles.RestoreData(job.DataPaths, data);
            job = job with { Phase = "starting", Mode = "restored" };
            journal.Save(job);
        }
        await StartAndConfirmAsync(job);
    }
}
