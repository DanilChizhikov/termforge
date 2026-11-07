using DTech.Core.Updates;
using DTech.Entrypoint.Lifecycle;

namespace DTech.Entrypoint.Updates.Installation;

internal sealed class UpdateInstaller : IDisposable
{
    private readonly UpdateInstallation _installation;
    private readonly IUpdateChecker _checker;
    private readonly VerifiedReleaseDownloader _downloader;
    private readonly WorkAdmissionGate _gate;
    private readonly ActiveTransferWatcher _transfers;
    private readonly GracefulShutdown _shutdown;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<UpdateInstaller> _logger;
    private readonly Lock _sync = new();
    private CancellationTokenSource? _preparation;
    private UpdateJob? _job;
    private string _state = "idle";
    private string? _error;

    public UpdateInstaller(UpdateInstallation installation, IUpdateChecker checker, VerifiedReleaseDownloader downloader,
        WorkAdmissionGate gate, ActiveTransferWatcher transfers, GracefulShutdown shutdown,
        IHostApplicationLifetime lifetime, ILogger<UpdateInstaller> logger)
    {
        _installation = installation;
        _checker = checker;
        _downloader = downloader;
        _gate = gate;
        _transfers = transfers;
        _shutdown = shutdown;
        _lifetime = lifetime;
        _logger = logger;
    }

    public InstallationStatus Status()
    {
        lock (_sync)
        {
            var reason = _installation.UnavailableReason;
            if (reason is not null) return new InstallationStatus(_state, null, _error, false, reason, null);
            var journal = _installation.Journal;
            var previous = journal.Previous();
            var active = journal.Active();
            var error = _error ?? (active?.Phase == "aborted" ? active.Error : null);
            return new InstallationStatus(_state, _job?.TargetVersion, error, true, null, previous?.CurrentVersion, _state == "waitingForTransfers" && TransfersFinished, _job?.Mode);
        }
    }

    public void Prepare(bool rollback)
    {
        lock (_sync)
        {
            EnsureIdle();
            if (_installation.UnavailableReason is { } reason) throw new InvalidOperationException(reason);
            _preparation?.Dispose();
            _preparation = new CancellationTokenSource();
            _error = null;
            _state = "preparing";
            var token = _preparation.Token;
            _ = PrepareAsync(rollback, token);
        }
    }

    public void Cancel()
    {
        lock (_sync)
        {
            if (_state is not ("preparing" or "prepared" or "waitingForTransfers"))
                throw new InvalidOperationException("There is no cancellable update.");
            _preparation?.Cancel();
            if (_state == "waitingForTransfers") _gate.TryReopen();
            if (_state == "preparing") return;
            CleanupPrepared();
            _state = "idle";
        }
    }

    public void WaitForTransfers()
    {
        lock (_sync)
        {
            if (_state != "prepared" || !_gate.TryBeginWaiting())
                throw new InvalidOperationException("An update or application exit is already in progress.");
            _state = "waitingForTransfers";
        }
    }

    public bool TransfersFinished => !_transfers.HasActiveTransfers();

    public void Commit(HttpContext context, RollbackUpdateRequest request)
    {
        lock (_sync)
        {
            if (_job is null || _state is not ("prepared" or "waitingForTransfers"))
                throw new InvalidOperationException("Prepare an update before restarting.");
            if (_state == "prepared" && !_gate.TryBeginWaiting())
                throw new InvalidOperationException("Application exit is already in progress.");
            var job = _job with { SaveCurrentData = request.SaveCurrentData, Phase = "armed", PreviousId = _installation.Journal.Previous()?.Id };
            try
            {
                _installation.Journal.Activate(job);
                UpdateProcess.LaunchWorker(_installation.Journal, job);
            }
            catch
            {
                _gate.TryReopen();
                _state = "prepared";
                _installation.Journal.Save(job with { Phase = "aborted", Error = "The update worker could not be started." });
                throw;
            }
            _job = job;
            _state = "restarting";
            _gate.TryBeginShutdown();
            context.Response.OnCompleted(() => StopAsync(job));
        }
    }

    private async Task PrepareAsync(bool rollback, CancellationToken token)
    {
        await Task.Yield();
        UpdateJob? job = null;
        try
        {
            var previous = rollback ? _installation.Journal.Previous() : null;
            if (rollback && previous is null) throw new InvalidOperationException("There is no previous version to restore.");
            if (previous is not null)
            {
                var snapshot = Path.Combine(_installation.Journal.JobDirectory(previous.Id), "data", "complete");
                if (!File.Exists(snapshot)) throw new IOException("The previous data snapshot is missing or incomplete. Rollback cannot continue.");
            }
            var status = rollback ? null : await _checker.Check(true, token);
            if (!rollback && status?.State != UpdateState.UpdateAvailable)
                throw new InvalidOperationException("No newer stable release is available.");
            var targetVersion = previous?.CurrentVersion ?? status!.LatestVersion!;
            job = _installation.Create(targetVersion);
            if (rollback)
            {
                var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                var dataPaths = job.DataPaths.Concat(previous!.DataPaths).DistinctBy(path => path.Path, comparer).ToArray();
                job = job with { Mode = "rollback", RestoreId = previous.Id, DataPaths = dataPaths };
            }
            _installation.Preflight(job);
            var directory = _installation.Journal.JobDirectory(job.Id);
            var staging = Path.Combine(directory, "new");
            if (rollback)
            {
                var old = Path.Combine(_installation.Journal.JobDirectory(previous!.Id), "old");
                UpdateFiles.CopyApplication(old, staging);
            }
            else
            {
                var downloads = Path.Combine(directory, "downloads");
                if (!SemanticVersion.TryParse(status!.LatestVersion, out var version) || status.ReleaseUrl is null)
                    throw new InvalidDataException("Invalid stable release metadata.");
                var release = new ReleaseInfo(version!, status.ReleaseUrl);
                var request = new ReleasePackageRequest(release, job.RuntimeIdentifier, downloads);
                var package = await _downloader.DownloadAsync(request, token);
                var extraction = new UpdateArchive.Extraction(package.Path, staging, $"termforge-{job.TargetVersion}-{job.RuntimeIdentifier}/", OperatingSystem.IsWindows());
                await UpdateArchive.ExtractAsync(extraction, token);
                UpdateFiles.Delete(downloads);
            }
            token.ThrowIfCancellationRequested();
            var configs = Directory.GetFiles(job.InstallationDirectory, "appsettings*.json");
            for (var i = 0; i < configs.Length; i++)
            {
                var target = Path.Combine(staging, Path.GetFileName(configs[i]));
                UpdateFiles.CopyFile(configs[i], target);
            }
            var helper = Path.Combine(directory, "helper");
            UpdateFiles.CopyApplication(job.InstallationDirectory, helper);
            lock (_sync)
            {
                token.ThrowIfCancellationRequested();
                _installation.Journal.Save(job);
                _job = job;
                _state = "prepared";
            }
        }
        catch (Exception exception)
        {
            if (job is not null)
            {
                try { UpdateFiles.Delete(_installation.Journal.JobDirectory(job.Id)); }
                catch (IOException) { }
            }
            lock (_sync)
            {
                _state = token.IsCancellationRequested ? "idle" : "failed";
                _error = token.IsCancellationRequested ? null : exception.Message;
            }
            if (!token.IsCancellationRequested) _logger.LogError(exception, "Update preparation failed");
        }
    }

    private async Task StopAsync(UpdateJob job)
    {
        try
        {
            var outcome = await _shutdown.RunAsync();
            if (outcome != ShutdownOutcome.Completed || _shutdown.HasFailures)
                throw new IOException("Shutdown did not complete successfully. The update was cancelled; application files are unchanged.");
            _installation.Journal.Save(job with { Phase = "stopping" });
        }
        catch (Exception exception)
        {
            _installation.Journal.Save(job with { Phase = "aborted", Error = exception.Message });
        }
        _lifetime.StopApplication();
    }

    private void EnsureIdle()
    {
        if (_state is not ("idle" or "failed")) throw new InvalidOperationException("Another update is already in progress.");
        if (!_gate.IsAcceptingWork) throw new InvalidOperationException("TermForge is exiting.");
    }

    private void CleanupPrepared()
    {
        if (_job is null) return;
        _installation.Journal.DeactivateAborted(_job.Id);
        UpdateFiles.Delete(_installation.Journal.JobDirectory(_job.Id));
        _job = null;
    }

    public void Dispose() => _preparation?.Cancel();
}
