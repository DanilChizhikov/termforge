using DTech.Core.Connections;
using DTech.Core.SavedConnections;

namespace DTech.Core.Diagnostics;

public sealed class CapabilityDiagnosticsService
{
    private readonly ISavedConnectionRepository _repository;
    private readonly ConnectionTargetFactory _targetFactory;
    private readonly IRemoteCommandRunnerFactory _runnerFactory;
    private readonly IReadOnlyList<ICapabilityProbe> _probes;
    private readonly CapabilityReportCache _cache;
    private readonly TimeProvider _timeProvider;

    public CapabilityDiagnosticsService(
        ISavedConnectionRepository repository,
        ConnectionTargetFactory targetFactory,
        IRemoteCommandRunnerFactory runnerFactory,
        IEnumerable<ICapabilityProbe> probes,
        CapabilityReportCache cache,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _targetFactory = targetFactory;
        _runnerFactory = runnerFactory;
        _probes = probes.ToList();
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public async Task<SavedConnectionResult<CapabilityReport>> GetAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var cached = _cache.Find(connectionId);
        if (cached is not null)
        {
            return SavedConnectionResult<CapabilityReport>.Success(cached);
        }

        return await RefreshAsync(connectionId, cancellationToken);
    }

    public async Task<SavedConnectionResult<CapabilityReport>> RefreshAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var all = await _repository.ListAsync(cancellationToken);
        var connection = JumpHostGuard.Find(all, connectionId);
        if (connection is null)
        {
            var notFound = SavedConnectionError.NotFound(connectionId);
            return SavedConnectionResult<CapabilityReport>.Failure(notFound);
        }

        var target = await _targetFactory.CreateAsync(connection, all, cancellationToken);
        if (!target.IsSuccess)
        {
            var unavailable = CreateReport(connectionId, CapabilityReportState.CredentialsUnavailable, []) with { Message = target.Error.Message };
            return SavedConnectionResult<CapabilityReport>.Success(unavailable);
        }

        var report = await CollectAsync(connectionId, target.Value, cancellationToken);
        if (report.State == CapabilityReportState.Completed)
        {
            _cache.Store(report);
        }

        return SavedConnectionResult<CapabilityReport>.Success(report);
    }

    private async Task<CapabilityReport> CollectAsync(Guid connectionId, SshConnectionTarget target, CancellationToken cancellationToken)
    {
        var connected = await _runnerFactory.ConnectAsync(target, cancellationToken);
        if (!connected.IsSuccess)
        {
            return CreateReport(connectionId, CapabilityReportState.ConnectionFailed, []) with { Message = connected.Error!.Message };
        }

        await using var runner = connected.Value;
        var context = new CapabilityProbeContext(target, runner);
        var capabilities = new List<CapabilityResult>();
        for (var i = 0; i < _probes.Count; i++)
        {
            var results = await RunProbeAsync(_probes[i], context, cancellationToken);
            capabilities.AddRange(results);
        }

        return CreateReport(connectionId, CapabilityReportState.Completed, capabilities);
    }

    private static async Task<IReadOnlyList<CapabilityResult>> RunProbeAsync(ICapabilityProbe probe, CapabilityProbeContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await probe.ProbeAsync(context, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var probeName = probe.GetType().Name;
            var failed = CapabilityResult.Unknown(probeName, probeName, $"The check failed unexpectedly: {RemoteText.Sanitize(exception.Message, 200)}");
            return [failed];
        }
    }

    private CapabilityReport CreateReport(Guid connectionId, CapabilityReportState state, IReadOnlyList<CapabilityResult> capabilities)
    {
        var checkedAt = _timeProvider.GetUtcNow().UtcDateTime;
        return new CapabilityReport(connectionId, checkedAt, state, capabilities);
    }
}
