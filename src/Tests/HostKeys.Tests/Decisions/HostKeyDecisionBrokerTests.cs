using DTech.Core.Connections;
using DTech.HostKeys.Decisions;
using DTech.HostKeys.Tests.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.HostKeys.Tests.Decisions;

[TestClass]
public sealed class HostKeyDecisionBrokerTests
{
    private static readonly TimeSpan LongTimeout = TimeSpan.FromMinutes(1);

    [TestMethod]
    public async Task AskAsync_WhenResolved_ReturnsOutcomeAndClearsPending()
    {
        var broker = TestChallenges.CreateBroker(LongTimeout);
        var challenge = TestChallenges.Create(HostKeyStatus.Unknown);

        var askTask = broker.AskAsync(challenge, CancellationToken.None);
        var pending = await TestChallenges.WaitForPendingAsync(broker);
        var resolved = broker.TryResolve(pending.Id, HostKeyPromptOutcome.Accepted);
        var outcome = await askTask;

        resolved.Should().BeTrue();
        pending.Challenge.Should().Be(challenge);
        outcome.Should().Be(HostKeyPromptOutcome.Accepted);
        broker.GetPending().Should().BeEmpty();
    }

    [TestMethod]
    public async Task AskAsync_WithoutDecision_TimesOutAndFailsClosed()
    {
        var broker = TestChallenges.CreateBroker(TimeSpan.FromMilliseconds(100));
        var challenge = TestChallenges.Create(HostKeyStatus.Unknown);

        var outcome = await broker.AskAsync(challenge, CancellationToken.None);

        outcome.Should().Be(HostKeyPromptOutcome.TimedOut);
        broker.GetPending().Should().BeEmpty();
    }

    [TestMethod]
    public async Task AskAsync_WhenCancelled_ThrowsAndClearsPending()
    {
        var broker = TestChallenges.CreateBroker(LongTimeout);
        var challenge = TestChallenges.Create(HostKeyStatus.Unknown);
        using var cancellation = new CancellationTokenSource();

        var askTask = broker.AskAsync(challenge, cancellation.Token);
        await TestChallenges.WaitForPendingAsync(broker);
        await cancellation.CancelAsync();
        var act = async () => await askTask;

        await act.Should().ThrowAsync<OperationCanceledException>();
        broker.GetPending().Should().BeEmpty();
    }

    [TestMethod]
    public async Task AskAsync_NotifiesWhenPendingDecisionsChange()
    {
        var notifier = Substitute.For<IPendingHostKeyDecisionNotifier>();
        var broker = TestChallenges.CreateBroker(TimeSpan.FromMilliseconds(50), notifier);
        var challenge = TestChallenges.Create(HostKeyStatus.Unknown);

        await broker.AskAsync(challenge, CancellationToken.None);

        await notifier.Received(1).PendingDecisionsChanged(Arg.Is<IReadOnlyList<PendingHostKeyDecision>>(list => list.Count == 1), Arg.Any<CancellationToken>());
        await notifier.Received(1).PendingDecisionsChanged(Arg.Is<IReadOnlyList<PendingHostKeyDecision>>(list => list.Count == 0), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task AskAsync_WhenNotifierFails_StillWaitsForDecision()
    {
        var notifier = Substitute.For<IPendingHostKeyDecisionNotifier>();
        var notifierFailure = new InvalidOperationException("socket closed");
        notifier.PendingDecisionsChanged(default!, default).ReturnsForAnyArgs(Task.FromException(notifierFailure));
        var broker = TestChallenges.CreateBroker(LongTimeout, notifier);
        var challenge = TestChallenges.Create(HostKeyStatus.Unknown);

        var askTask = broker.AskAsync(challenge, CancellationToken.None);
        var pending = await TestChallenges.WaitForPendingAsync(broker);
        broker.TryResolve(pending.Id, HostKeyPromptOutcome.Rejected);

        (await askTask).Should().Be(HostKeyPromptOutcome.Rejected);
    }

    [TestMethod]
    public void TryResolve_UnknownId_ReturnsFalse()
    {
        var broker = TestChallenges.CreateBroker(LongTimeout);
        var unknownId = Guid.NewGuid();

        broker.TryResolve(unknownId, HostKeyPromptOutcome.Accepted).Should().BeFalse();
    }

    [TestMethod]
    public async Task GetPending_ExposesExpiryBasedOnTimeout()
    {
        var broker = TestChallenges.CreateBroker(LongTimeout);
        var challenge = TestChallenges.Create(HostKeyStatus.Unknown);
        var before = DateTimeOffset.UtcNow;

        var askTask = broker.AskAsync(challenge, CancellationToken.None);
        var pending = await TestChallenges.WaitForPendingAsync(broker);
        broker.TryResolve(pending.Id, HostKeyPromptOutcome.Rejected);
        await askTask;

        pending.ExpiresAt.Should().BeOnOrAfter(before + LongTimeout);
    }
}
