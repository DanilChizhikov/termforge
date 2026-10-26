using DTech.Core.Files;
using DTech.Core.Operations;
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

    public TransferEnqueuer(TransferQueue queue, ITransferConnectionOpener opener, TransferStrategySelector strategies, ITransferJournal journal, ITransferNotifier notifier)
    {
        _queue = queue;
        _opener = opener;
        _strategies = strategies;
        _journal = journal;
        _notifier = notifier;
    }

    public async Task<TransferEnqueueResult> EnqueueAsync(TransferBatchRequest batch, CancellationToken cancellationToken)
    {
        if (batch.SourcePaths.Count == 0 || batch.SourcePaths.Count > MaxBatchSize)
        {
            var invalid = FileError.Invalid($"Select between 1 and {MaxBatchSize} items to transfer.");
            return TransferEnqueueResult.Failed(invalid);
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

        var busy = FindBusyTarget(candidates);
        if (busy is not null)
        {
            var inProgress = new FileError(FileErrorCode.AlreadyExists, $"A transfer to {busy} is already queued or running.");
            return TransferEnqueueResult.Failed(inProgress);
        }

        var snapshots = new List<TransferSnapshot>(candidates.Count);
        for (var i = 0; i < candidates.Count; i++)
        {
            var admitted = await AdmitAsync(candidates[i], cancellationToken);
            if (!admitted.IsSuccess)
            {
                return TransferEnqueueResult.Failed(admitted.Error);
            }

            snapshots.Add(admitted.Value);
        }

        return TransferEnqueueResult.Queued(snapshots);
    }

    public async Task<TransferEnqueueResult> RetryAsync(Operation operation, CancellationToken cancellationToken)
    {
        var request = TransferOperationDetails.TryRead(operation);
        if (request is null)
        {
            var unreadable = FileError.Invalid("The operation does not describe a transfer.");
            return TransferEnqueueResult.Failed(unreadable);
        }

        TransferRequest[] requests = [request];
        var inspected = await InspectAsync(requests, cancellationToken);
        if (!inspected.IsSuccess)
        {
            return TransferEnqueueResult.Failed(inspected.Error);
        }

        var candidate = inspected.Value[0] with { ResumeExisting = true };
        if (_queue.Remove(operation.Id))
        {
            await _notifier.TransferRemoved(operation.Id, CancellationToken.None);
        }

        var admitted = await AdmitAsync(candidate, cancellationToken);
        if (!admitted.IsSuccess)
        {
            return TransferEnqueueResult.Failed(admitted.Error);
        }

        TransferSnapshot[] queued = [admitted.Value];
        return TransferEnqueueResult.Queued(queued);
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

    private string? FindBusyTarget(IReadOnlyList<TransferCandidate> candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var probe = new TransferJob(Guid.Empty, candidate.Request, candidate.Inspection.TargetPath);
            if (_queue.IsTargetBusy(probe))
            {
                return candidate.Inspection.TargetPath;
            }
        }

        return null;
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

    private async Task<FileResult<TransferSnapshot>> AdmitAsync(TransferCandidate candidate, CancellationToken cancellationToken)
    {
        var targetPath = candidate.Inspection.TargetPath;
        var id = await _journal.CreateAsync(candidate.Request, targetPath, cancellationToken);
        var job = new TransferJob(id, candidate.Request, targetPath) { ResumeExisting = candidate.ResumeExisting };
        var admission = _queue.TryAdd(job);
        if (admission != TransferAdmission.Added)
        {
            await _journal.CancelAsync(id, CancellationToken.None);
            var message = admission == TransferAdmission.Closed
                ? "The application is shutting down and does not accept new transfers."
                : $"A transfer to {targetPath} is already queued or running.";
            var rejected = new FileError(FileErrorCode.AlreadyExists, message);
            return FileResult<TransferSnapshot>.Failure(rejected);
        }

        var snapshot = job.Snapshot();
        await _notifier.TransferChanged(snapshot, CancellationToken.None);
        return FileResult<TransferSnapshot>.Success(snapshot);
    }

    private sealed record TransferCandidate(TransferRequest Request, TransferInspection Inspection)
    {
        public bool ResumeExisting { get; init; }
    }
}
