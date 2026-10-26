using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DTech.Core.Terminal.Sessions;

public sealed class TerminalSessionManager(ITerminalSessionFactory sessionFactory, TimeProvider timeProvider, TerminalSessionOptions options) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<TerminalSessionId, ManagedTerminalSession> _sessions = new();
    private readonly Dictionary<Guid, int> _sessionNumbers = new();
    private readonly Lock _registrationGate = new();
    private bool _isShuttingDown;

    public int Count => _sessions.Count;

    public async Task<TerminalResult<ManagedTerminalSession>> OpenAsync(TerminalOpenRequest request, CancellationToken cancellationToken)
    {
        if (!request.InitialSize.IsValid)
        {
            var invalidSize = new TerminalError(TerminalErrorCode.InvalidRequest, $"Terminal size {request.InitialSize.Columns}x{request.InitialSize.Rows} is invalid.");
            return TerminalResult<ManagedTerminalSession>.Failure(invalidSize);
        }

        var rejection = FindAdmissionRejection(request.OwnerKey);
        if (rejection is not null)
        {
            return TerminalResult<ManagedTerminalSession>.Failure(rejection);
        }

        var sessionRequest = new TerminalSessionRequest(request.Server.Target, request.InitialSize, TerminalSessionRequest.DefaultTerminalType);
        var opened = await sessionFactory.OpenAsync(sessionRequest, cancellationToken);
        if (!opened.IsSuccess)
        {
            var connectionError = TerminalError.FromSsh(opened.Error!);
            return TerminalResult<ManagedTerminalSession>.Failure(connectionError);
        }

        return await RegisterAsync(request, opened.Value);
    }

    public ManagedTerminalSession? Find(TerminalSessionId id, string ownerKey)
    {
        if (!_sessions.TryGetValue(id, out var session))
        {
            return null;
        }

        return IsOwnedBy(session, ownerKey) ? session : null;
    }

    public IReadOnlyList<TerminalSessionDescriptor> List(string ownerKey)
    {
        return _sessions.Values
            .Where(session => IsOwnedBy(session, ownerKey))
            .Select(session => session.Descriptor)
            .OrderBy(descriptor => descriptor.OpenedAt)
            .ThenBy(descriptor => descriptor.SessionNumber)
            .ToList();
    }

    public async Task<bool> CloseAsync(TerminalSessionId id, string ownerKey, string reason)
    {
        var session = Find(id, ownerKey);
        if (session is null)
        {
            return false;
        }

        await ReleaseAsync(session, reason);
        return true;
    }

    public async Task ReleaseAsync(ManagedTerminalSession session, string reason)
    {
        var entry = new KeyValuePair<TerminalSessionId, ManagedTerminalSession>(session.Descriptor.Id, session);
        _sessions.TryRemove(entry);
        await session.CloseAsync(reason);
    }

    public async Task CloseAllAsync(string reason)
    {
        ManagedTerminalSession[] openSessions;
        lock (_registrationGate)
        {
            _isShuttingDown = true;
            openSessions = _sessions.Values.ToArray();
        }

        var releases = new Task[openSessions.Length];
        for (var i = 0; i < openSessions.Length; i++)
        {
            releases[i] = ReleaseAsync(openSessions[i], reason);
        }

        await Task.WhenAll(releases);
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAllAsync(TerminalCloseReasons.ShuttingDown);
    }

    private async Task<TerminalResult<ManagedTerminalSession>> RegisterAsync(TerminalOpenRequest request, ITerminalSession session)
    {
        ManagedTerminalSession? registered = null;
        TerminalError? rejection;
        lock (_registrationGate)
        {
            rejection = FindAdmissionRejection(request.OwnerKey);
            if (rejection is null)
            {
                var descriptor = CreateDescriptor(request.Server);
                registered = new ManagedTerminalSession(descriptor, request.OwnerKey, session);
                _sessions[descriptor.Id] = registered;
            }
        }

        if (registered is null)
        {
            await session.DisposeAsync();
            return TerminalResult<ManagedTerminalSession>.Failure(rejection!);
        }

        _ = CloseIfNotAttachedAsync(registered);
        return TerminalResult<ManagedTerminalSession>.Success(registered);
    }

    private TerminalError? FindAdmissionRejection(string ownerKey)
    {
        lock (_registrationGate)
        {
            if (_isShuttingDown)
            {
                return new TerminalError(TerminalErrorCode.ShuttingDown, TerminalCloseReasons.ShuttingDown);
            }

            var ownedCount = _sessions.Values.Count(session => IsOwnedBy(session, ownerKey));
            if (ownedCount >= options.MaxSessionsPerOwner)
            {
                return new TerminalError(TerminalErrorCode.SessionLimitReached, $"At most {options.MaxSessionsPerOwner} terminal sessions can be open at the same time. Close a terminal first.");
            }

            return null;
        }
    }

    private TerminalSessionDescriptor CreateDescriptor(TerminalServer server)
    {
        _sessionNumbers.TryGetValue(server.ConnectionId, out var previousNumber);
        var sessionNumber = previousNumber + 1;
        _sessionNumbers[server.ConnectionId] = sessionNumber;
        var id = TerminalSessionId.Create();
        var openedAt = timeProvider.GetUtcNow().UtcDateTime;
        return new TerminalSessionDescriptor(id, server.ConnectionId, server.Name, sessionNumber, openedAt);
    }

    private async Task CloseIfNotAttachedAsync(ManagedTerminalSession session)
    {
        try
        {
            await Task.Delay(options.AttachTimeout, timeProvider, session.Closing);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!session.IsAttached)
        {
            await ReleaseAsync(session, TerminalCloseReasons.NotAttached);
        }
    }

    private static bool IsOwnedBy(ManagedTerminalSession session, string ownerKey)
    {
        var expected = Encoding.UTF8.GetBytes(session.OwnerKey);
        var actual = Encoding.UTF8.GetBytes(ownerKey);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
