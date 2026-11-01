using DTech.Core.Files;
using DTech.Core.Operations;
using DTech.Core.Safety;
using DTech.Core.Transfers.Execution;
using DTech.Core.Transfers.Queue;

namespace DTech.Core.Transfers;

public sealed class TransferEnqueuer
{
    public const int MaxBatchSize = 200;

    private readonly TransferQueue _queue;
    private readonly ITransferConnectionOpener _opener;
    private readonly TransferStrategySelector _strategies;
    private readonly ITransferJournal _journal;
    private readonly ITransferNotifier _notifier;
    private readonly ServerWriteGate _writeGate;

    public TransferEnqueuer(TransferQueue queue, ITransferConnectionOpener opener, TransferStrategySelector strategies, ITransferJournal journal, ITransferNotifier notifier, ServerWriteGate writeGate)
    {
        _queue = queue;
        _opener = opener;
        _strategies = strategies;
        _journal = journal;
        _notifier = notifier;
        _writeGate = writeGate;
    }

    public async Task<TransferEnqueueResult> EnqueueAsync(TransferBatchRequest batch, CancellationToken cancellationToken)
    {
        if (batch.SourcePaths.Count == 0 || batch.SourcePaths.Count > MaxBatchSize)
        {
            var invalid = FileError.Invalid($"Select between 1 and {MaxBatchSize} items to transfer.");
            return TransferEnqueueResult.Failed(invalid);
        }

        var denied = await CheckBeforeInspectionAsync(batch, cancellationToken);
        if (denied is not null)
        {
            return TransferEnqueueResult.Failed(denied);
        }

        var requests = CreateRequests(batch);
        var inspected = await InspectAsync(requests, cancellationToken);
        if (!inspected.IsSuccess)
        {
            return TransferEnqueueResult.Failed(inspected.Error);
        }

        var candidates = inspected.Value;
        var conflicts = FindConflicts(candidates, batch.Overwrite);
        if (conflicts.Count > 0)
        {
            return TransferEnqueueResult.Conflicted(conflicts);
        }

        return await AdmitAsync(candidates, batch, cancellationToken);
    }

    public Task<TransferEnqueueResult> RetryAsync(Operation operation, CancellationToken cancellationToken)
    {
        var options = new OperationRetryOptions();
        return RetryAsync(operation, options, cancellationToken);
    }

    public async Task<TransferEnqueueResult> RetryAsync(Operation operation, OperationRetryOptions options, CancellationToken cancellationToken)
    {
        var request = TransferOperationDetails.TryRead(operation);
        if (request is null)
        {
            var unreadable = FileError.Invalid("The operation does not describe a transfer.");
            return TransferEnqueueResult.Failed(unreadable);
        }

        request = request with { Overwrite = options.Overwrite };
        var batch = new TransferBatchRequest
        {
            ConnectionId = request.ConnectionId,
            ServerName = request.ServerName,
            Direction = request.Direction,
            SourcePaths = [request.SourcePath],
            TargetDirectory = request.TargetDirectory,
            Overwrite = options.Overwrite,
            ConfirmationToken = options.ConfirmationToken,
        };
        var denied = await CheckBeforeInspectionAsync(batch, cancellationToken);
        if (denied is not null)
        {
            return TransferEnqueueResult.Failed(denied);
        }

        TransferRequest[] requests = [request];
        var inspected = await InspectAsync(requests, cancellationToken);
        if (!inspected.IsSuccess)
        {
            return TransferEnqueueResult.Failed(inspected.Error);
        }

        var candidate = inspected.Value[0] with { ResumeExisting = true };
        TransferCandidate[] candidates = [candidate];
        var conflicts = FindConflicts(candidates, request.Overwrite);
        if (conflicts.Count > 0)
        {
            return TransferEnqueueResult.Conflicted(conflicts);
        }

        var admitted = await AdmitAsync(candidates, batch, cancellationToken);
        if (admitted.IsSuccess && _queue.Remove(operation.Id))
        {
            await _notifier.TransferRemoved(operation.Id, CancellationToken.None);
        }

        return admitted;
    }

    private async Task<FileError?> CheckBeforeInspectionAsync(TransferBatchRequest batch, CancellationToken cancellationToken)
    {
        if (batch.Direction != TransferDirection.Upload)
        {
            return null;
        }

        if (await _writeGate.IsReadOnlyAsync(batch.ConnectionId, cancellationToken))
        {
            return FileError.ReadOnlyServer();
        }

        return batch.Overwrite && string.IsNullOrEmpty(batch.ConfirmationToken)
            ? FileError.OverwriteConfirmationRequired(batch.TargetDirectory)
            : null;
    }

    private async Task<FileError?> CheckUploadAsync(TransferBatchRequest batch, CancellationToken cancellationToken)
    {
        if (batch.Direction != TransferDirection.Upload)
        {
            return null;
        }

        var writeIntent = new WriteIntent(batch.ConnectionId);
        if (batch.Overwrite)
        {
            writeIntent = writeIntent with
            {
                Operation = DangerousOperation.TransferOverwrite,
                Target = batch.TargetDirectory,
                ConfirmationToken = batch.ConfirmationToken,
            };
        }

        var denial = await _writeGate.CheckAsync(writeIntent, cancellationToken);
        return denial switch
        {
            WriteDenial.ReadOnlyServer => FileError.ReadOnlyServer(),
            WriteDenial.ConfirmationRequired => FileError.OverwriteConfirmationRequired(batch.TargetDirectory),
            _ => null,
        };
    }

    private static List<TransferRequest> CreateRequests(TransferBatchRequest batch)
    {
        var requests = new List<TransferRequest>(batch.SourcePaths.Count);
        for (var i = 0; i < batch.SourcePaths.Count; i++)
        {
            var request = new TransferRequest
            {
                ConnectionId = batch.ConnectionId,
                ServerName = batch.ServerName,
                Direction = batch.Direction,
                SourcePath = batch.SourcePaths[i],
                TargetDirectory = batch.TargetDirectory,
                Overwrite = batch.Overwrite,
            };
            requests.Add(request);
        }

        return requests;
    }

    private static List<string> FindConflicts(IReadOnlyList<TransferCandidate> candidates, bool overwrite)
    {
        var conflicts = new List<string>();
        if (overwrite)
        {
            return conflicts;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate.Inspection.TargetExists)
            {
                conflicts.Add(candidate.Inspection.TargetPath);
            }
        }

        return conflicts;
    }

    private async Task<FileResult<IReadOnlyList<TransferCandidate>>> InspectAsync(IReadOnlyList<TransferRequest> requests, CancellationToken cancellationToken)
    {
        var connectionId = requests[0].ConnectionId;
        var opened = await _opener.OpenAsync(connectionId, cancellationToken);
        if (!opened.IsSuccess)
        {
            return FileResult<IReadOnlyList<TransferCandidate>>.Failure(opened.Error);
        }

        await using var client = opened.Value;
        var candidates = new List<TransferCandidate>(requests.Count);
        for (var i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            var strategy = _strategies.For(request.Direction);
            var context = new TransferContext(client, request);
            var inspection = await strategy.InspectAsync(context, cancellationToken);
            if (!inspection.IsSuccess)
            {
                return FileResult<IReadOnlyList<TransferCandidate>>.Failure(inspection.Error);
            }

            var candidate = new TransferCandidate(request, inspection.Value);
            candidates.Add(candidate);
        }

        return FileResult<IReadOnlyList<TransferCandidate>>.Success(candidates);
    }

    private async Task<TransferEnqueueResult> AdmitAsync(IReadOnlyList<TransferCandidate> candidates, TransferBatchRequest batch, CancellationToken cancellationToken)
    {
        var targets = new TransferTarget[candidates.Count];
        for (var i = 0; i < candidates.Count; i++)
        {
            targets[i] = new TransferTarget(candidates[i].Request, candidates[i].Inspection.TargetPath);
        }

        var reserved = _queue.TryReserve(targets);
        using var reservation = reserved.Reservation;
        if (reservation is null)
        {
            return AdmissionFailed(reserved.Status);
        }

        if (batch.Overwrite && batch.Direction == TransferDirection.Download && candidates[0].ResumeExisting)
        {
            var scope = new ConfirmationScope(DangerousOperation.LocalTransferOverwrite, batch.ConnectionId, batch.TargetDirectory);
            if (!_writeGate.ConsumeConfirmation(scope, batch.ConfirmationToken))
            {
                var confirmation = FileError.OverwriteConfirmationRequired(batch.TargetDirectory);
                return TransferEnqueueResult.Failed(confirmation);
            }
        }

        var denied = await CheckUploadAsync(batch, cancellationToken);
        if (denied is not null)
        {
            return TransferEnqueueResult.Failed(denied);
        }

        var jobs = new List<TransferJob>(candidates.Count);
        var committed = false;
        try
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var targetPath = candidate.Inspection.TargetPath;
                var id = await _journal.CreateAsync(candidate.Request, targetPath, cancellationToken);
                var job = new TransferJob(id, candidate.Request, targetPath) { ResumeExisting = candidate.ResumeExisting };
                jobs.Add(job);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var admission = reservation.Commit(jobs);
            if (admission != TransferAdmission.Added)
            {
                return AdmissionFailed(admission);
            }

            committed = true;
        }
        finally
        {
            if (!committed)
            {
                for (var i = 0; i < jobs.Count; i++)
                {
                    jobs[i].FinishProcessing();
                    await _journal.CancelAsync(jobs[i].Id, CancellationToken.None);
                }
            }
        }

        var snapshots = new List<TransferSnapshot>(jobs.Count);
        for (var i = 0; i < jobs.Count; i++)
        {
            var snapshot = jobs[i].Snapshot();
            snapshots.Add(snapshot);
            await _notifier.TransferChanged(snapshot, CancellationToken.None);
        }

        return TransferEnqueueResult.Queued(snapshots);
    }

    private static TransferEnqueueResult AdmissionFailed(TransferAdmission admission)
    {
        var message = admission == TransferAdmission.Closed
            ? "The application is shutting down and does not accept new transfers."
            : "The selection contains duplicate targets or a target is already queued or running.";
        var error = new FileError(FileErrorCode.AlreadyExists, message);
        return TransferEnqueueResult.Failed(error);
    }

    private sealed record TransferCandidate(TransferRequest Request, TransferInspection Inspection)
    {
        public bool ResumeExisting { get; init; }
    }
}
