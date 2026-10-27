namespace DTech.Core.Updates;

public sealed class UpdateChecker : IUpdateChecker
{
    private readonly IReleaseSource _source;
    private readonly ICurrentVersionProvider _versionProvider;
    private readonly TimeProvider _timeProvider;
    private readonly UpdateCacheSettings _cacheSettings;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _cacheLock = new();
    private UpdateStatus? _cached;
    private DateTimeOffset _cacheExpiresAt;

    public UpdateChecker(IReleaseSource source, ICurrentVersionProvider versionProvider, TimeProvider timeProvider, UpdateCacheSettings cacheSettings)
    {
        _source = source;
        _versionProvider = versionProvider;
        _timeProvider = timeProvider;
        _cacheSettings = cacheSettings;
    }

    public UpdateStatus Peek()
    {
        lock (_cacheLock)
        {
            return _cached ?? UpdateStatus.NotChecked(_versionProvider.Current.ToString());
        }
    }

    public async Task<UpdateStatus> Check(bool force, CancellationToken cancellationToken)
    {
        if (!force && TryGetFresh(out var fresh))
        {
            return fresh;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!force && TryGetFresh(out var refreshed))
            {
                return refreshed;
            }

            var status = await Lookup(cancellationToken);
            Store(status);
            return status;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<UpdateStatus> Lookup(CancellationToken cancellationToken)
    {
        var current = _versionProvider.Current;
        var checkedAt = _timeProvider.GetUtcNow().UtcDateTime;
        ReleaseLookup lookup;
        try
        {
            lookup = await _source.GetLatestRelease(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            lookup = ReleaseLookup.Failed;
        }

        if (!lookup.Succeeded)
        {
            return new UpdateStatus(UpdateState.CheckFailed, current.ToString(), null, null, checkedAt);
        }

        var release = lookup.Release;
        if (release is null || release.Version.CompareTo(current) <= 0)
        {
            return new UpdateStatus(UpdateState.UpToDate, current.ToString(), release?.Version.ToString(), null, checkedAt);
        }

        return new UpdateStatus(UpdateState.UpdateAvailable, current.ToString(), release.Version.ToString(), release.Url, checkedAt);
    }

    private bool TryGetFresh(out UpdateStatus status)
    {
        lock (_cacheLock)
        {
            var isFresh = _cached is not null && _timeProvider.GetUtcNow() < _cacheExpiresAt;
            status = isFresh ? _cached! : UpdateStatus.NotChecked(string.Empty);
            return isFresh;
        }
    }

    private void Store(UpdateStatus status)
    {
        var duration = status.State == UpdateState.CheckFailed ? _cacheSettings.FailureDuration : _cacheSettings.SuccessDuration;
        lock (_cacheLock)
        {
            _cached = status;
            _cacheExpiresAt = _timeProvider.GetUtcNow() + duration;
        }
    }
}
