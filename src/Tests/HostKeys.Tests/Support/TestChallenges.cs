using DTech.Core.Connections;
using DTech.HostKeys.Decisions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DTech.HostKeys.Tests.Support;

internal static class TestChallenges
{
    public static readonly SshEndpoint Endpoint = new("server.example", 22);

    public static HostKeyChallenge Create(HostKeyStatus status)
    {
        var presentedKey = TestPublicKey.CreateEd25519();
        var request = new HostKeyVerificationRequest(Endpoint, presentedKey.HostKey);
        IReadOnlyList<HostKey> knownKeys = status == HostKeyStatus.Changed ? [TestPublicKey.CreateEd25519().HostKey] : [];
        var verification = new HostKeyVerification(status, knownKeys);
        return new HostKeyChallenge(request, verification);
    }

    public static HostKeyDecisionBroker CreateBroker(TimeSpan decisionTimeout, IPendingHostKeyDecisionNotifier? notifier = null)
    {
        var options = new HostKeyVerificationOptions
        {
            DecisionTimeout = decisionTimeout,
        };
        var wrappedOptions = Options.Create(options);
        var effectiveNotifier = notifier ?? Substitute.For<IPendingHostKeyDecisionNotifier>();
        return new HostKeyDecisionBroker(wrappedOptions, effectiveNotifier, TimeProvider.System, NullLogger<HostKeyDecisionBroker>.Instance);
    }

    public static async Task<PendingHostKeyDecision> WaitForPendingAsync(HostKeyDecisionBroker broker)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var pending = broker.GetPending();
            if (pending.Count > 0)
            {
                return pending[0];
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }
    }
}
