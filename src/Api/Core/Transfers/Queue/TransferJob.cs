using DTech.Core.Operations;

namespace DTech.Core.Transfers.Queue;

public sealed class TransferJob
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private OperationStatus _status = OperationStatus.Queued;
    private long _completedBytes;
    private long _currentFileBytes;
    private long _totalBytes;
    private int _completedFiles;
    private int _totalFiles;
    private double _bytesPerSecond;
    private double? _remainingSeconds;
    private string? _error;
    private bool _retryable;
    private long _partialBytes;
    private string? _partialPath;
    private int _version;

    public TransferJob(Guid id, TransferRequest request, string targetPath)
    {
        Id = id;
        Request = request;
        TargetPath = targetPath;
    }

    public Guid Id { get; }

    public TransferRequest Request { get; }

    public string TargetPath { get; }

    public bool ResumeExisting { get; init; }

    public long Sequence { get; internal set; }

    public CancellationToken CancellationToken => _cancellation.Token;

    public int Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    public OperationStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public string? PartialPath
    {
        get
        {
            lock (_gate)
            {
                return _partialPath;
            }
        }
    }

    public long CurrentFileBytes
    {
        get
        {
            lock (_gate)
            {
                return _currentFileBytes;
            }
        }
    }

    public long TransferredBytes
    {
        get
        {
            lock (_gate)
            {
                return _completedBytes + _currentFileBytes;
            }
        }
    }

    public bool TryStart()
    {
        lock (_gate)
        {
            if (_status != OperationStatus.Queued)
            {
                return false;
            }

            _status = OperationStatus.Running;
            _version++;
            return true;
        }
    }

    public TransferCancelOutcome RequestCancel()
    {
        lock (_gate)
        {
            if (_status == OperationStatus.Queued)
            {
                _status = OperationStatus.Cancelled;
                _version++;
                return TransferCancelOutcome.CancelledWhileQueued;
            }

            if (_status != OperationStatus.Running)
            {
                return TransferCancelOutcome.AlreadyFinished;
            }
        }

        _cancellation.Cancel();
        return TransferCancelOutcome.CancellationRequested;
    }

    public void Plan(long totalBytes, int totalFiles)
    {
        lock (_gate)
        {
            _totalBytes = totalBytes;
            _totalFiles = totalFiles;
            _version++;
        }
    }

    public void BeginFile(string partialPath, long offset)
    {
        lock (_gate)
        {
            _partialPath = partialPath;
            _currentFileBytes = offset;
            _version++;
        }
    }

    public void ReportFileBytes(long fileBytes)
    {
        lock (_gate)
        {
            _currentFileBytes = fileBytes;
            _version++;
        }
    }

    public void CompleteFile(long size)
    {
        lock (_gate)
        {
            _completedBytes += size;
            _currentFileBytes = 0;
            _completedFiles++;
            _partialPath = null;
            _version++;
        }
    }

    public void UpdateRate(double bytesPerSecond, double? remainingSeconds)
    {
        lock (_gate)
        {
            _bytesPerSecond = bytesPerSecond;
            _remainingSeconds = remainingSeconds;
            _version++;
        }
    }

    public void Succeed()
    {
        lock (_gate)
        {
            _status = OperationStatus.Succeeded;
            _bytesPerSecond = 0;
            _remainingSeconds = null;
            _partialPath = null;
            _version++;
        }
    }

    public void Fail(TransferFailure failure, string? partialPath)
    {
        lock (_gate)
        {
            _status = OperationStatus.Failed;
            _error = failure.Error;
            _retryable = failure.Retryable;
            _partialBytes = failure.PartialBytes;
            _partialPath = partialPath;
            _bytesPerSecond = 0;
            _remainingSeconds = null;
            _version++;
        }
    }

    public void MarkCancelled()
    {
        lock (_gate)
        {
            _status = OperationStatus.Cancelled;
            _retryable = true;
            _partialPath = null;
            _bytesPerSecond = 0;
            _remainingSeconds = null;
            _version++;
        }
    }

    public void ForgetPartial()
    {
        lock (_gate)
        {
            _partialPath = null;
            _partialBytes = 0;
            _version++;
        }
    }

    public TransferProgress Progress()
    {
        lock (_gate)
        {
            return CreateProgress();
        }
    }

    public TransferSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new TransferSnapshot
            {
                Id = Id,
                Request = Request,
                TargetPath = TargetPath,
                Status = _status,
                Progress = CreateProgress(),
                Error = _error,
                Retryable = _retryable || _status == OperationStatus.Cancelled,
                PartialBytes = _partialBytes,
            };
        }
    }

    private TransferProgress CreateProgress()
    {
        var transferred = _completedBytes + _currentFileBytes;
        return new TransferProgress(transferred, _totalBytes, _completedFiles, _totalFiles, _bytesPerSecond, _remainingSeconds);
    }
}
