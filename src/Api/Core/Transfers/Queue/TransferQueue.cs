using System.Collections.Concurrent;
using System.Threading.Channels;
using DTech.Core.Operations;

namespace DTech.Core.Transfers.Queue;

public sealed class TransferQueue
{
    private readonly ConcurrentDictionary<Guid, TransferJob> _jobs = new();
    private readonly Channel<TransferJob> _pending = Channel.CreateUnbounded<TransferJob>();
    private readonly Lock _admission = new();
    private bool _isClosed;
    private long _sequence;

    public ChannelReader<TransferJob> Pending => _pending.Reader;

    public TransferAdmission TryAdd(TransferJob job)
    {
        lock (_admission)
        {
            if (_isClosed)
            {
                return TransferAdmission.Closed;
            }

            if (IsTargetBusy(job))
            {
                return TransferAdmission.TargetBusy;
            }

            _sequence++;
            job.Sequence = _sequence;
            _jobs[job.Id] = job;
            _pending.Writer.TryWrite(job);
            return TransferAdmission.Added;
        }
    }

    public bool IsTargetBusy(TransferJob candidate)
    {
        foreach (var job in _jobs.Values)
        {
            if (job.Id != candidate.Id && IsActive(job) && IsSameTarget(job, candidate))
            {
                return true;
            }
        }

        return false;
    }

    public TransferJob? Find(Guid id)
    {
        return _jobs.TryGetValue(id, out var job) ? job : null;
    }

    public IReadOnlyList<TransferJob> List()
    {
        var jobs = new List<TransferJob>(_jobs.Values);
        jobs.Sort(CompareBySequence);
        return jobs;
    }

    public bool Remove(Guid id)
    {
        return _jobs.TryRemove(id, out _);
    }

    public void Close()
    {
        lock (_admission)
        {
            _isClosed = true;
            _pending.Writer.TryComplete();
        }
    }

    private static int CompareBySequence(TransferJob first, TransferJob second)
    {
        return first.Sequence.CompareTo(second.Sequence);
    }

    private static bool IsActive(TransferJob job)
    {
        var status = job.Status;
        return status is OperationStatus.Queued or OperationStatus.Running;
    }

    private static bool IsSameTarget(TransferJob first, TransferJob second)
    {
        if (first.Request.Direction != second.Request.Direction)
        {
            return false;
        }

        if (first.Request.Direction == TransferDirection.Upload && first.Request.ConnectionId != second.Request.ConnectionId)
        {
            return false;
        }

        var comparison = first.Request.Direction == TransferDirection.Download && !OperatingSystem.IsLinux()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(first.TargetPath, second.TargetPath, comparison);
    }
}
