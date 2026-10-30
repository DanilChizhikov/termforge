using DTech.Core.AiBridge.Approvals;
using DTech.Core.Tests.AiBridge.Support;
using FluentAssertions;

namespace DTech.Core.Tests.AiBridge;

[TestClass]
public sealed class AiApprovalBrokerTests
{
    private static readonly AiActionDescription Action = new(Guid.NewGuid(), "prod-web", AiActionKind.Command, "uptime");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(AiApprovalOutcome.Approved)]
    [DataRow(AiApprovalOutcome.Denied)]
    public async Task Request_ReturnsTheUserDecision(AiApprovalOutcome answer)
    {
        var notifier = new ScriptedApprovalNotifier { Answer = answer };
        var broker = CreateBroker(notifier, TimeSpan.FromSeconds(5));

        var outcome = await broker.RequestAsync(Action, TestContext.CancellationToken);

        outcome.Should().Be(answer);
        notifier.Requested.Should().ContainSingle().Which.Should().Be(Action);
        broker.GetPending().Should().BeEmpty();
        notifier.PendingCounts.Should().Equal(1, 0);
    }

    [TestMethod]
    public async Task Request_WithoutDecision_TimesOut()
    {
        var notifier = new ScriptedApprovalNotifier();
        var broker = CreateBroker(notifier, TimeSpan.FromMilliseconds(50));

        var outcome = await broker.RequestAsync(Action, TestContext.CancellationToken);

        outcome.Should().Be(AiApprovalOutcome.TimedOut);
        broker.GetPending().Should().BeEmpty();
    }

    [TestMethod]
    public async Task Request_WhenCancelled_RemovesThePendingApproval()
    {
        var notifier = new ScriptedApprovalNotifier();
        var broker = CreateBroker(notifier, TimeSpan.FromSeconds(30));
        using var cancellation = new CancellationTokenSource();

        var request = broker.RequestAsync(Action, cancellation.Token);
        broker.GetPending().Should().ContainSingle();
        await cancellation.CancelAsync();

        await request.Invoking(task => task).Should().ThrowAsync<OperationCanceledException>();
        broker.GetPending().Should().BeEmpty();
    }

    [TestMethod]
    public void TryResolve_UnknownApproval_ReturnsFalse()
    {
        var notifier = new ScriptedApprovalNotifier();
        var broker = CreateBroker(notifier, TimeSpan.FromSeconds(5));

        broker.TryResolve(Guid.NewGuid(), AiApprovalOutcome.Approved).Should().BeFalse();
    }

    private static AiApprovalBroker CreateBroker(ScriptedApprovalNotifier notifier, TimeSpan timeout)
    {
        var broker = new AiApprovalBroker(new AiApprovalTimeout(timeout), notifier, TimeProvider.System);
        notifier.Broker = broker;
        return broker;
    }
}
