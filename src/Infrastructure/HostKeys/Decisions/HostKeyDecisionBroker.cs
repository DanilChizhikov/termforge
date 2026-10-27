using System.Collections.Concurrent;
using DTech.Core.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DTech.HostKeys.Decisions;

public sealed class HostKeyDecisionBroker : IHostKeyTrustPrompt
{
    private readonly ConcurrentDictionary<Guid, PendingHostKeyDecision> _pendingDecisions = new();
    private readonly IOptions<HostKeyVerificationOptions> _options;
    private readonly IPendingHostKeyDecisionNotifier _notifier;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HostKeyDecisionBroker> _logger;

    public HostKeyDecisionBroker(IOptions<HostKeyVerificationOptions> options, IPendingHostKeyDecisionNotifier notifier, TimeProvider timeProvider, ILogger<HostKeyDecisionBroker> logger)
    {
        _options = options;
        _notifier = notifier;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<HostKeyPromptOutcome> AskAsync(HostKeyChallenge challenge, CancellationToken cancellationToken)
    {
        var timeout = _options.Value.DecisionTimeout;
        var expiresAt = _timeProvider.GetUtcNow() + timeout;
        var pendingDecision = new PendingHostKeyDecision(Guid.NewGuid(), challenge, expiresAt);
        _pendingDecisions[pendingDecision.Id] = pendingDecision;
        var request = challenge.Request;
        _logger.LogInformation($"Waiting for a decision on {challenge.Verification.Status} host key {request.HostKey} of {request.Endpoint}");
        try
        {
            await NotifyAsync();
            return await pendingDecision.Completion.WaitAsync(timeout, _timeProvider, cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning($"No decision on host key {request.HostKey} of {request.Endpoint} within {timeout}; the connection fails closed");
            return HostKeyPromptOutcome.TimedOut;
        }
        finally
        {
            _pendingDecisions.TryRemove(pendingDecision.Id, out _);
            await NotifyAsync();
        }
    }

    public IReadOnlyList<PendingHostKeyDecision> GetPending()
    {
        return _pendingDecisions.Values.OrderBy(decision => decision.ExpiresAt).ToList();
    }

    public PendingHostKeyDecision? Find(Guid id)
    {
        return _pendingDecisions.GetValueOrDefault(id);
    }

    public bool TryResolve(Guid id, HostKeyPromptOutcome outcome)
    {
        return _pendingDecisions.TryGetValue(id, out var pendingDecision) && pendingDecision.TryComplete(outcome);
    }

    private async Task NotifyAsync()
    {
        var pendingDecisions = GetPending();
        try
        {
            await _notifier.PendingDecisionsChanged(pendingDecisions, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning($"Could not notify the UI about pending host key decisions: {exception.Message}");
        }
    }
}
