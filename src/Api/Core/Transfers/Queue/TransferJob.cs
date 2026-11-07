using DTech.Core.Operations;

namespace DTech.Core.Transfers.Queue;

public sealed class TransferJob
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly CancellationToken _cancellationToken;
    private readonly List<string> _partialPaths = [];
    private IReadOnlyList<TransferSourceVersion> _sourceVersions = [];
    private TransferVerification? _verification;
    private bool _processingFinished;
    private int _cancelCalls;
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
        _cancellationToken = _cancellation.Token;
    }

    public Guid Id { get; }

    public TransferRequest Request { get; }

    public string TargetPath { get; }

    public bool ResumeExisting { get; init; }

    public long Sequence { get; internal set; }

    public CancellationToken CancellationToken => _cancellationToken;

    public bool ProcessingFinished
    {
        get
        {
            lock (_gate)
            {
                return _processingFinished;
            }
        }
    }

    public void FinishProcessing()
    {
        lock (_gate)
        {
            if (!_processingFinished)
            {
                _processingFinished = true;
                if (_cancelCalls == 0)
                {
                    _cancellation.Dispose();
                }
            }
        }
    }

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

    public bool HasPartial
    {
        get
        {
            lock (_gate)
            {
                return _partialPaths.Count > 0;
            }
        }
    }

    public TransferRecoveryState CaptureRecovery()
    {
        lock (_gate)
        {
            var paths = _partialPaths.ToArray();
            return new TransferRecoveryState(Request, TargetPath, paths) { PartialBytes = _partialBytes, SourceVersions = _sourceVersions };
        }
    }

    public void RestorePartials(IReadOnlyList<string> paths)
    {
        lock (_gate)
        {
            for (var i = 0; i < paths.Count; i++)
            {
                AddPartialPath(paths[i]);
            }
        }
    }

    public void RestoreSourceVersions(IReadOnlyList<TransferSourceVersion> versions)
    {
        lock (_gate) { _sourceVersions = versions; }
    }

    public bool CheckSourceVersions(IReadOnlyList<DTech.Core.Transfers.Execution.TransferItem> items)
    {
        var versions = items.Where(item => !item.IsDirectory)
            .Select(item => new TransferSourceVersion(item.SourcePath, item.Size, item.ModifiedUtc))
            .OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();
        lock (_gate)
        {
            if (ResumeExisting && _sourceVersions.Count > 0 && !_sourceVersions.SequenceEqual(versions)) return false;
            _sourceVersions = versions;
            return true;
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
            if (_processingFinished)
            {
                return TransferCancelOutcome.AlreadyFinished;
            }

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

            _cancelCalls++;
        }

        try
        {
            _cancellation.Cancel();
            return TransferCancelOutcome.CancellationRequested;
        }
        finally
        {
            lock (_gate)
            {
                _cancelCalls--;
                if (_processingFinished && _cancelCalls == 0)
                {
                    _cancellation.Dispose();
                }
            }
        }
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
            _verification = null;
            AddPartialPath(partialPath);
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

    public void BeginVerification(long totalBytes)
    {
        lock (_gate)
        {
            _verification = new TransferVerification(0, totalBytes);
            _version++;
        }
    }

    public void ReportVerification(long bytes)
    {
        lock (_gate)
        {
            if (_verification is not { } verification) return;
            var checkedBytes = Math.Clamp(bytes, 0, verification.TotalBytes);
            _verification = verification with { CheckedBytes = checkedBytes };
            _version++;
        }
    }

    public void CompleteFile(long size)
    {
        lock (_gate)
        {
            _verification = null;
            _completedBytes += size;
            _currentFileBytes = 0;
            _completedFiles++;
            if (_partialPath is not null)
            {
                _partialPaths.Remove(_partialPath);
            }

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
            _verification = null;
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
            _verification = null;
            _status = OperationStatus.Failed;
            _error = failure.Error;
            _retryable = failure.Retryable;
            _partialBytes = failure.PartialBytes;
            _partialPath = partialPath;
            if (partialPath is not null)
            {
                AddPartialPath(partialPath);
            }

            _bytesPerSecond = 0;
            _remainingSeconds = null;
            _version++;
        }
    }

    public void MarkCancelled()
    {
        lock (_gate)
        {
            _verification = null;
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
            _partialPaths.Clear();
            _partialBytes = 0;
            _version++;
        }
    }

    public void ForgetPartialPath(string path)
    {
        lock (_gate)
        {
            _partialPaths.Remove(path);
            if (_partialPath == path)
            {
                _partialPath = null;
                _partialBytes = 0;
            }

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
                HasPartial = _partialPaths.Count > 0,
                Verification = _verification,
            };
        }
    }

    private TransferProgress CreateProgress()
    {
        var transferred = _completedBytes + _currentFileBytes;
        return new TransferProgress(transferred, _totalBytes, _completedFiles, _totalFiles, _bytesPerSecond, _remainingSeconds);
    }

    private void AddPartialPath(string path)
    {
        if (!_partialPaths.Contains(path))
        {
            _partialPaths.Add(path);
        }
    }
}
