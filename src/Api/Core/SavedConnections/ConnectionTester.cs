using System.Diagnostics;
using DTech.Core.Connections;
using DTech.Core.Terminal;

namespace DTech.Core.SavedConnections;

public sealed class ConnectionTester
{
    private static readonly TerminalSize ProbeSize = new(80, 24);

    private readonly ISavedConnectionRepository _repository;
    private readonly ConnectionTargetFactory _targetFactory;
    private readonly ITerminalSessionFactory _sessionFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ConnectionTestOptions _options;

    public ConnectionTester(ISavedConnectionRepository repository, ConnectionTargetFactory targetFactory, ITerminalSessionFactory sessionFactory, TimeProvider timeProvider, ConnectionTestOptions options)
    {
        _repository = repository;
        _targetFactory = targetFactory;
        _sessionFactory = sessionFactory;
        _timeProvider = timeProvider;
        _options = options;
    }

    public async Task<SavedConnectionResult<ConnectionTestResult>> TestAsync(Guid id, CancellationToken cancellationToken)
    {
        var all = await _repository.ListAsync(cancellationToken);
        var connection = JumpHostGuard.Find(all, id);
        if (connection is null)
        {
            var notFound = SavedConnectionError.NotFound(id);
            return SavedConnectionResult<ConnectionTestResult>.Failure(notFound);
        }

        var target = await _targetFactory.CreateAsync(connection, all, cancellationToken);
        if (!target.IsSuccess)
        {
            var unavailable = CreateResult(ConnectionTestState.CredentialsUnavailable, connection);
            var unavailableWithMessage = unavailable with { Message = target.Error.Message };
            return SavedConnectionResult<ConnectionTestResult>.Success(unavailableWithMessage);
        }

        var result = await ProbeAsync(connection, target.Value, cancellationToken);
        return SavedConnectionResult<ConnectionTestResult>.Success(result);
    }

    private async Task<ConnectionTestResult> ProbeAsync(SavedConnection connection, SshConnectionTarget target, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.Timeout);
        var request = new TerminalSessionRequest(target, ProbeSize, TerminalSessionRequest.DefaultTerminalType);
        var stopwatch = Stopwatch.StartNew();
        var opened = await _sessionFactory.OpenAsync(request, timeout.Token);
        stopwatch.Stop();
        if (opened.IsSuccess)
        {
            await opened.Value.DisposeAsync();
            return await CreateConnectedResultAsync(connection, stopwatch.ElapsedMilliseconds, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return CreateFailureResult(connection, opened.Error!, timeout.IsCancellationRequested);
    }

    private async Task<ConnectionTestResult> CreateConnectedResultAsync(SavedConnection connection, long latencyMilliseconds, CancellationToken cancellationToken)
    {
        var testedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _repository.RecordConnectedAsync(connection.Id, testedAt, cancellationToken);
        return new ConnectionTestResult(ConnectionTestState.Connected, testedAt)
        {
            LatencyMilliseconds = latencyMilliseconds,
            LastConnectedAt = testedAt,
            Message = $"Connected to {connection.Details.Host}:{connection.Details.Port} as {connection.Details.UserName}.",
        };
    }

    private ConnectionTestResult CreateFailureResult(SavedConnection connection, SshError error, bool timedOut)
    {
        var state = timedOut ? ConnectionTestState.Timeout : MapState(error.Kind);
        var result = CreateResult(state, connection);
        return result with
        {
            Message = ConnectionTestMessages.Describe(state, connection.Details),
            Detail = error.Message,
        };
    }

    private ConnectionTestResult CreateResult(ConnectionTestState state, SavedConnection connection)
    {
        var testedAt = _timeProvider.GetUtcNow().UtcDateTime;
        return new ConnectionTestResult(state, testedAt)
        {
            LastConnectedAt = connection.LastConnectedAt,
        };
    }

    private static ConnectionTestState MapState(SshErrorKind kind)
    {
        return kind switch
        {
            SshErrorKind.AuthenticationFailed => ConnectionTestState.AuthenticationFailed,
            SshErrorKind.PrivateKeyUnusable => ConnectionTestState.AuthenticationFailed,
            SshErrorKind.AgentUnavailable => ConnectionTestState.AuthenticationFailed,
            SshErrorKind.ConnectionFailed => ConnectionTestState.Unreachable,
            SshErrorKind.HostKeyRejected => ConnectionTestState.HostKeyRejected,
            _ => ConnectionTestState.Failed,
        };
    }
}
