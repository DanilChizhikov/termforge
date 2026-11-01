using System.Collections.Concurrent;
using System.Threading.Channels;
using DTech.Core.Operations;

namespace DTech.Core.Transfers.Queue;

public sealed class TransferQueue
{
    public const int MaximumFinishedJobs = 1000;

    private readonly ConcurrentDictionary<Guid, TransferJob> _jobs = new();
    private readonly Channel<TransferJob> _pending = Channel.CreateUnbounded<TransferJob>();
    private readonly Lock _admission = new();
    private readonly HashSet<TransferReservation> _reservations = [];
    private bool _isClosed;
    private long _sequence;

    public ChannelReader<TransferJob> Pending => _pending.Reader;

    public TransferAdmission TryAdd(TransferJob job)
    {
        TransferTarget[] targets = [new(job.Request, job.TargetPath)];
        var reserved = TryReserve(targets);
        using var reservation = reserved.Reservation;
        if (reservation is null)
        {
            return reserved.Status;
        }

        TransferJob[] jobs = [job];
        return reservation.Commit(jobs);
    }

    public (TransferAdmission Status, TransferReservation? Reservation) TryReserve(IReadOnlyList<TransferTarget> targets)
    {
        lock (_admission)
        {
            if (_isClosed)
            {
                return (TransferAdmission.Closed, null);
            }

            for (var i = 0; i < targets.Count; i++)
            {
                if (IsTargetBusy(targets[i]))
                {
                    return (TransferAdmission.TargetBusy, null);
                }

                for (var j = 0; j < i; j++)
                {
                    if (targets[i].Matches(targets[j]))
                    {
                        return (TransferAdmission.TargetBusy, null);
                    }
                }
            }

            var snapshot = targets.ToArray();
            var reservation = new TransferReservation(this, snapshot);
            _reservations.Add(reservation);
            return (TransferAdmission.Added, reservation);
        }
    }

    internal TransferAdmission Commit(TransferReservation reservation, IReadOnlyList<TransferJob> jobs)
    {
        lock (_admission)
        {
            if (_isClosed || !_reservations.Remove(reservation))
            {
                return TransferAdmission.Closed;
            }

            if (jobs.Count != reservation.Targets.Count)
            {
                throw new ArgumentException("Every reserved target must have a transfer job.", nameof(jobs));
            }

            var identities = new HashSet<Guid>();
            for (var i = 0; i < jobs.Count; i++)
            {
                var target = new TransferTarget(jobs[i].Request, jobs[i].TargetPath);
                if (!target.Matches(reservation.Targets[i]) || !identities.Add(jobs[i].Id) || _jobs.ContainsKey(jobs[i].Id))
                {
                    throw new ArgumentException("Jobs must match their reserved targets and have unique identities.", nameof(jobs));
                }
            }

            for (var i = 0; i < jobs.Count; i++)
            {
                var job = jobs[i];
                job.Sequence = ++_sequence;
                _jobs[job.Id] = job;
            }

            for (var i = 0; i < jobs.Count; i++)
            {
                _pending.Writer.TryWrite(jobs[i]);
            }

            return TransferAdmission.Added;
        }
    }

    internal void Release(TransferReservation reservation)
    {
        lock (_admission)
        {
            _reservations.Remove(reservation);
        }
    }

    public bool IsTargetBusy(TransferJob candidate)
    {
        var target = new TransferTarget(candidate.Request, candidate.TargetPath);
        lock (_admission)
        {
            return IsTargetBusy(target, candidate.Id);
        }
    }

    private bool IsTargetBusy(TransferTarget target, Guid? excludedId = null)
    {
        foreach (var job in _jobs.Values)
        {
            var existing = new TransferTarget(job.Request, job.TargetPath);
            if (job.Id != excludedId && IsActive(job) && target.Matches(existing))
            {
                return true;
            }
        }

        foreach (var reservation in _reservations)
        {
            for (var i = 0; i < reservation.Targets.Count; i++)
            {
                if (target.Matches(reservation.Targets[i]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public IReadOnlyList<Guid> Finish(TransferJob job)
    {
        job.FinishProcessing();
        lock (_admission)
        {
            var finished = _jobs.Values.Where(item => item.ProcessingFinished && item.PartialPath is null).ToList();
            finished.Sort(CompareBySequence);
            var removed = new List<Guid>();
            for (var i = 0; i < finished.Count - MaximumFinishedJobs; i++)
            {
                if (_jobs.TryRemove(finished[i].Id, out _))
                {
                    removed.Add(finished[i].Id);
                }
            }

            return removed;
        }
    }

    public TransferJob? Find(Guid id)
    {
        return _jobs.TryGetValue(id, out var job) ? job : null;
    }

    public IReadOnlyList<TransferJob> List()
    {
        lock (_admission)
        {
            var jobs = new List<TransferJob>(_jobs.Values);
            jobs.Sort(CompareBySequence);
            return jobs;
        }
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
}
