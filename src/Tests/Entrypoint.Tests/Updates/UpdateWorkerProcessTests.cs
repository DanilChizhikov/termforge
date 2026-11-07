using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Formats.Tar;
using System.IO.Compression;
using DTech.Entrypoint.Updates;
using DTech.Entrypoint.Updates.Installation;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Updates;

[TestClass]
[TestCategory("UpdateProcess")]
public sealed class UpdateWorkerProcessTests
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"termforge-update-process-{Guid.NewGuid():N}");
    private UpdateJournal? _journal;

    [TestCleanup]
    public async Task Cleanup()
    {
        var active = _journal?.Active();
        if (active is not null && UpdateProcess.IsAlive(active.ChildProcessId, active.ChildStartTicks))
        {
            using var child = Process.GetProcessById(active.ChildProcessId);
            child.Kill(true);
            await child.WaitForExitAsync();
        }
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [TestMethod]
    public async Task Published_worker_installs_and_keeps_a_complete_previous_version()
    {
        var job = Prepare();
        _journal!.Activate(job);
        var worker = new UpdateWorker(_journal);

        await worker.RunAsync(job.Id);

        _journal.Active()!.Phase.Should().Be("completed");
        _journal.Previous()!.Id.Should().Be(job.Id);
        var previous = Path.Combine(_journal.JobDirectory(job.Id), "old", UpdateProcess.ExecutableName);
        File.Exists(previous).Should().BeTrue();
        var snapshot = Path.Combine(_journal.JobDirectory(job.Id), "data", "0");
        File.ReadAllText(snapshot).Should().Be("draft before update");
    }

    [TestMethod]
    public async Task Failed_candidate_opens_recovery_without_restoring_current_data()
    {
        var job = Prepare() with { TargetVersion = "99.0.0" };
        _journal!.Activate(job);
        var worker = new UpdateWorker(_journal);

        await worker.RunAsync(job.Id);

        var active = _journal.Active()!;
        active.Phase.Should().Be("recovery");
        File.ReadAllText(job.DataPaths[0].Path).Should().Be("draft before update");
        File.WriteAllText(job.DataPaths[0].Path, "keep until confirmed");
        await Task.Delay(1500);
        UpdateProcess.IsAlive(active.ChildProcessId, active.ChildStartTicks).Should().BeTrue();
        File.ReadAllText(job.DataPaths[0].Path).Should().Be("keep until confirmed");
        _journal.Previous().Should().BeNull();
    }

    [TestMethod]
    public async Task Interrupted_switch_restores_application_files_and_keeps_data_for_confirmation()
    {
        var job = Prepare() with { Phase = "switching" };
        var old = Path.Combine(_journal!.JobDirectory(job.Id), "old");
        UpdateFiles.CopyApplication(job.InstallationDirectory, old);
        var config = Path.Combine(job.InstallationDirectory, "appsettings.json");
        File.WriteAllText(config, "invalid json");
        _journal.Activate(job);
        var worker = new UpdateWorker(_journal);

        await worker.RecoverAsync(job.Id, "Interrupted switch");

        _journal.Active()!.Phase.Should().Be("recovery");
        File.ReadAllText(config).Should().NotBe("invalid json");
        File.ReadAllText(job.DataPaths[0].Path).Should().Be("draft before update");
    }

    [TestMethod]
    public async Task Confirmed_recovery_restores_snapshot_and_retains_current_data()
    {
        var job = Prepare() with { TargetVersion = "99.0.0" };
        _journal!.Activate(job);
        var worker = new UpdateWorker(_journal);
        await worker.RunAsync(job.Id);
        await StopChildAsync();
        File.WriteAllText(job.DataPaths[0].Path, "data after failed update");
        var recovery = _journal.Read(job.Id) with { Mode = "restore", Phase = "stopping", ChildProcessId = 0, ChildStartTicks = 0 };
        _journal.Save(recovery);

        await worker.RunAsync(job.Id);

        _journal.Active()!.Phase.Should().Be("completed");
        File.ReadAllText(job.DataPaths[0].Path).Should().Be("draft before update");
        var retained = Path.Combine(_journal.JobDirectory(job.Id), "retained-data", "0");
        File.ReadAllText(retained).Should().Be("data after failed update");
    }

    [TestMethod]
    public async Task Manual_rollback_restores_previous_data_and_retains_the_replaced_data()
    {
        var first = Prepare();
        _journal!.Activate(first);
        var worker = new UpdateWorker(_journal);
        await worker.RunAsync(first.Id);
        await StopChildAsync();
        var rollback = Prepare() with { Mode = "rollback", RestoreId = first.Id, PreviousId = first.Id, SaveCurrentData = true };
        File.WriteAllText(rollback.DataPaths[0].Path, "changes after update");
        _journal!.Activate(rollback);

        await worker.RunAsync(rollback.Id);

        _journal.Active()!.Phase.Should().Be("completed");
        _journal.Previous().Should().BeNull();
        File.ReadAllText(rollback.DataPaths[0].Path).Should().Be("draft before update");
        var retained = Path.Combine(_journal.JobDirectory(rollback.Id), "data", "0");
        File.ReadAllText(retained).Should().Be("changes after update");
    }

    [TestMethod]
    public async Task Missing_completed_snapshot_blocks_migrations_and_keeps_recovery_available()
    {
        var job = Prepare() with { Mode = "restore", DataBackupComplete = true };
        _journal!.Activate(job);
        var worker = new UpdateWorker(_journal);

        await worker.RunAsync(job.Id);

        var active = _journal.Active()!;
        active.Phase.Should().Be("recovery");
        active.Error.Should().Contain("missing or incomplete");
        File.Exists(job.DataPaths[1].Path).Should().BeFalse();
    }

    [TestMethod]
    public async Task External_worker_waits_for_parent_exit_before_backing_up_and_installing()
    {
        var job = Prepare() with { Phase = "recovery" };
        _journal!.Activate(job);
        using var parent = UpdateProcess.Launch(job, "recovery");
        try
        {
            var lockPath = Path.Combine(_journal.Root, "application.lock");
            var deadline = DateTime.UtcNow.AddSeconds(30);
            var acquired = false;
            while (!parent.HasExited && DateTime.UtcNow < deadline)
            {
                try { using var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) { acquired = true; break; }
                await Task.Delay(100);
            }
            acquired.Should().BeTrue("the parent must hold the installation lease before an update can be requested");
            job = job with { Phase = "stopping", ParentProcessId = parent.Id, ParentStartTicks = parent.StartTime.ToUniversalTime().Ticks };
            _journal.Save(job);
            UpdateProcess.LaunchWorker(_journal, job);
            await Task.Delay(500);
            _journal.Active()!.Phase.Should().Be("stopping");
            File.Exists(Path.Combine(_journal.JobDirectory(job.Id), "data", "complete")).Should().BeFalse();
        }
        finally
        {
            if (!parent.HasExited) parent.Kill(true);
            await parent.WaitForExitAsync();
        }
        var finish = DateTime.UtcNow.AddSeconds(90);
        while (_journal.Active()!.Phase is not ("completed" or "recovery") && DateTime.UtcNow < finish) await Task.Delay(100);

        _journal.Active()!.Phase.Should().Be("completed");
        var snapshot = Path.Combine(_journal.JobDirectory(job.Id), "data", "0");
        File.ReadAllText(snapshot).Should().Be("draft before update");
    }

    [TestMethod]
    public async Task Published_package_roundtrips_through_the_safe_extractor()
    {
        var job = Prepare();
        var package = Path.GetFullPath(Environment.GetEnvironmentVariable("TERMFORGE_UPDATE_PACKAGE")!);
        var archive = Path.Combine(_directory, OperatingSystem.IsWindows() ? "package.zip" : "package.tar.gz");
        if (OperatingSystem.IsWindows()) ZipFile.CreateFromDirectory(package, archive, CompressionLevel.NoCompression, true);
        else
        {
            await using var file = File.Create(archive);
            await using var gzip = new GZipStream(file, CompressionLevel.Fastest);
            TarFile.CreateFromDirectory(package, gzip, true);
        }
        var destination = Path.Combine(_directory, "extracted");
        var extraction = new UpdateArchive.Extraction(archive, destination, $"termforge-{job.TargetVersion}-{job.RuntimeIdentifier}/", OperatingSystem.IsWindows());

        await UpdateArchive.ExtractAsync(extraction, CancellationToken.None);

        File.Exists(Path.Combine(destination, UpdateProcess.ExecutableName)).Should().BeTrue();
        File.Exists(Path.Combine(destination, "TermForge.staticwebassets.endpoints.json")).Should().BeTrue();
    }

    [TestMethod]
    public async Task Recovery_runs_from_retained_helper_when_application_files_cannot_be_restored()
    {
        var job = Prepare() with { Phase = "switching" };
        var old = Path.Combine(_journal!.JobDirectory(job.Id), "old");
        UpdateFiles.CopyApplication(job.InstallationDirectory, old);
        _journal.Activate(job);
        var applicationLock = Path.Combine(_journal.Root, "application.lock");
        await using var blockedInstallation = new FileStream(applicationLock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var worker = new UpdateWorker(_journal);

        await worker.RecoverAsync(job.Id, "Interrupted update");

        var active = _journal.Active()!;
        active.Phase.Should().Be("recovery");
        active.Error.Should().Contain("retained helper");
        await Task.Delay(1500);
        UpdateProcess.IsAlive(active.ChildProcessId, active.ChildStartTicks).Should().BeTrue();
        File.Exists(job.DataPaths[1].Path).Should().BeFalse();
    }

    private async Task StopChildAsync()
    {
        var job = _journal!.Active()!;
        if (!UpdateProcess.IsAlive(job.ChildProcessId, job.ChildStartTicks)) return;
        using var child = Process.GetProcessById(job.ChildProcessId);
        child.Kill(true);
        await child.WaitForExitAsync();
    }

    private UpdateJob Prepare()
    {
        var package = Environment.GetEnvironmentVariable("TERMFORGE_UPDATE_PACKAGE");
        if (string.IsNullOrWhiteSpace(package)) Assert.Inconclusive("Set TERMFORGE_UPDATE_PACKAGE to a published package directory to run process tests.");
        package = Path.GetFullPath(package);
        Directory.CreateDirectory(_directory);
        var installation = Path.Combine(_directory, "application");
        UpdateFiles.CopyApplication(package, installation);
        _journal = new UpdateJournal(installation);
        installation = _journal.InstallationDirectory;
        var data = Path.Combine(_directory, "draft.txt");
        File.WriteAllText(data, "draft before update");
        var database = Path.Combine(_directory, "database.db");
        var versionProvider = new AssemblyVersionProvider();
        var version = versionProvider.Current.ToString();
        var job = new UpdateJob
        {
            Id = Guid.NewGuid().ToString("N"), InstallationDirectory = installation,
            WorkingDirectory = _directory, CurrentVersion = version, TargetVersion = version,
            RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier, Phase = "stopping", StartupTimeoutSeconds = 60,
            DataPaths = [new(data, false), new(database, false), new($"{database}-wal", false), new($"{database}-shm", false)],
            Arguments = [$"--Persistence:DatabasePath={database}", $"--Credentials:VaultPath={_directory}/vault", "--Updates:CheckOnStartup=false",
                "--Lifecycle:OpenBrowser=false", "--Kestrel:Endpoints:Http:Url=http://127.0.0.1:0"],
        };
        var staging = Path.Combine(_journal.JobDirectory(job.Id), "new");
        UpdateFiles.CopyApplication(package, staging);
        var helper = Path.Combine(_journal.JobDirectory(job.Id), "helper");
        UpdateFiles.CopyApplication(package, helper);
        return job;
    }
}
