using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.SavedConnections;

namespace DTech.Core.Monitoring;

public sealed class ServerMonitoringService
{
    private readonly ISavedConnectionRepository _repository;
    private readonly ConnectionTargetFactory _targetFactory;
    private readonly IRemoteCommandRunnerFactory _runnerFactory;
    private readonly ServerMetricsCollector _collector;
    private readonly TimeProvider _timeProvider;

    public ServerMonitoringService(
        ISavedConnectionRepository repository,
        ConnectionTargetFactory targetFactory,
        IRemoteCommandRunnerFactory runnerFactory,
        ServerMetricsCollector collector,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _targetFactory = targetFactory;
        _runnerFactory = runnerFactory;
        _collector = collector;
        _timeProvider = timeProvider;
    }

    public async Task<SavedConnectionResult<ServerMetrics>> CollectAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var all = await _repository.ListAsync(cancellationToken);
        var connection = JumpHostGuard.Find(all, connectionId);
        if (connection is null)
        {
            var notFound = SavedConnectionError.NotFound(connectionId);
            return SavedConnectionResult<ServerMetrics>.Failure(notFound);
        }

        var target = await _targetFactory.CreateAsync(connection, all, cancellationToken);
        if (!target.IsSuccess)
        {
            var unavailable = CreateMetrics(connectionId, MonitoringState.CredentialsUnavailable, target.Error.Message);
            return SavedConnectionResult<ServerMetrics>.Success(unavailable);
        }

        var metrics = await CollectFromTargetAsync(connectionId, target.Value, cancellationToken);
        return SavedConnectionResult<ServerMetrics>.Success(metrics);
    }

    private async Task<ServerMetrics> CollectFromTargetAsync(Guid connectionId, SshConnectionTarget target, CancellationToken cancellationToken)
    {
        var connected = await _runnerFactory.ConnectAsync(target, cancellationToken);
        if (!connected.IsSuccess)
        {
            return CreateMetrics(connectionId, MonitoringState.ConnectionFailed, connected.Error!.Message);
        }

        await using var runner = connected.Value;
        var collected = await _collector.CollectAsync(runner, cancellationToken);
        if (!collected.IsSuccess)
        {
            return CreateMetrics(connectionId, MonitoringState.ConnectionFailed, collected.Error!.Message);
        }

        return new ServerMetrics(connectionId, UtcNow(), MonitoringState.Connected, collected.Value);
    }

    private ServerMetrics CreateMetrics(Guid connectionId, MonitoringState state, string message)
    {
        var readings = MetricReadings.AllUnavailable("Not measured: the server is not connected.");
        return new ServerMetrics(connectionId, UtcNow(), state, readings) { Message = RemoteText.Sanitize(message, 300) };
    }

    private DateTime UtcNow()
    {
        return _timeProvider.GetUtcNow().UtcDateTime;
    }
}
