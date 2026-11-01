using DTech.Core.Connections;
using DTech.HostKeys.Decisions;
using DTech.HostKeys.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DTech.HostKeys.Tests.Decisions;

[TestClass]
public sealed class HostKeyDecisionHandlerTests
{
    private static readonly TimeSpan LongTimeout = TimeSpan.FromMinutes(1);

    private HostKeyDecisionBroker _broker = null!;
    private ITrustedHostKeyStore _trustedStore = null!;
    private HostKeyDecisionHandler _handler = null!;

    [TestInitialize]
    public void Initialize()
    {
        _broker = TestChallenges.CreateBroker(LongTimeout);
        _trustedStore = Substitute.For<ITrustedHostKeyStore>();
        _handler = new HostKeyDecisionHandler(_broker, _trustedStore, NullLogger<HostKeyDecisionHandler>.Instance);
    }

    [TestMethod]
    public async Task AcceptOnce_UnknownKey_AcceptsWithoutSaving()
    {
        var (askTask, pending) = await StartAsync(HostKeyStatus.Unknown);

        var result = await DecideAsync(pending, HostKeyUserDecision.AcceptOnce, false);

        result.IsSuccess.Should().BeTrue();
        (await askTask).Should().Be(HostKeyPromptOutcome.Accepted);
        await _trustedStore.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        await _trustedStore.DidNotReceiveWithAnyArgs().ReplaceAsync(default!, default);
    }

    [TestMethod]
    public async Task AcceptAndSave_UnknownKey_SavesAndAccepts()
    {
        var (askTask, pending) = await StartAsync(HostKeyStatus.Unknown);

        var result = await DecideAsync(pending, HostKeyUserDecision.AcceptAndSave, false);

        result.IsSuccess.Should().BeTrue();
        (await askTask).Should().Be(HostKeyPromptOutcome.Accepted);
        await _trustedStore.Received(1).AddAsync(pending.Challenge.Request, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    [DataRow(HostKeyStatus.Unknown)]
    [DataRow(HostKeyStatus.Changed)]
    public async Task Reject_Rejects(HostKeyStatus status)
    {
        var (askTask, pending) = await StartAsync(status);

        var result = await DecideAsync(pending, HostKeyUserDecision.Reject, false);

        result.IsSuccess.Should().BeTrue();
        (await askTask).Should().Be(HostKeyPromptOutcome.Rejected);
    }

    [TestMethod]
    public async Task Replace_UnknownKey_IsNotAllowed()
    {
        var (_, pending) = await StartAsync(HostKeyStatus.Unknown);

        var result = await DecideAsync(pending, HostKeyUserDecision.Replace, true);

        result.ErrorCode.Should().Be(HostKeyDecisionErrorCode.NotAllowed);
        _broker.GetPending().Should().ContainSingle();
        _broker.TryResolve(pending.Id, HostKeyPromptOutcome.Rejected);
    }

    [TestMethod]
    [DataRow(HostKeyUserDecision.AcceptOnce)]
    [DataRow(HostKeyUserDecision.AcceptAndSave)]
    public async Task Accept_ChangedKey_IsNotAllowedAndStaysPending(HostKeyUserDecision decision)
    {
        var (_, pending) = await StartAsync(HostKeyStatus.Changed);

        var result = await DecideAsync(pending, decision, true);

        result.ErrorCode.Should().Be(HostKeyDecisionErrorCode.NotAllowed);
        _broker.GetPending().Should().ContainSingle();
        await _trustedStore.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        _broker.TryResolve(pending.Id, HostKeyPromptOutcome.Rejected);
    }

    [TestMethod]
    public async Task Replace_ChangedKeyWithoutConfirmation_RequiresConfirmation()
    {
        var (_, pending) = await StartAsync(HostKeyStatus.Changed);

        var result = await DecideAsync(pending, HostKeyUserDecision.Replace, false);

        result.ErrorCode.Should().Be(HostKeyDecisionErrorCode.ConfirmationRequired);
        await _trustedStore.DidNotReceiveWithAnyArgs().ReplaceAsync(default!, default);
        _broker.TryResolve(pending.Id, HostKeyPromptOutcome.Rejected);
    }

    [TestMethod]
    public async Task Replace_ChangedKeyWithConfirmation_ReplacesAndAccepts()
    {
        var (askTask, pending) = await StartAsync(HostKeyStatus.Changed);

        var result = await DecideAsync(pending, HostKeyUserDecision.Replace, true);

        result.IsSuccess.Should().BeTrue();
        (await askTask).Should().Be(HostKeyPromptOutcome.Accepted);
        await _trustedStore.Received(1).ReplaceAsync(pending.Challenge.Request, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Decide_UnknownPendingId_ReturnsNotFound()
    {
        var command = new HostKeyDecisionCommand(Guid.NewGuid(), HostKeyUserDecision.AcceptOnce, false);

        var result = await _handler.DecideAsync(command, CancellationToken.None);

        result.ErrorCode.Should().Be(HostKeyDecisionErrorCode.NotFound);
    }

    [TestMethod]
    public async Task AcceptAndSave_WhenRejectAlreadyWon_DoesNotPersist()
    {
        var (askTask, pending) = await StartAsync(HostKeyStatus.Unknown);
        await DecideAsync(pending, HostKeyUserDecision.Reject, false);

        var result = await DecideAsync(pending, HostKeyUserDecision.AcceptAndSave, false);

        result.ErrorCode.Should().Be(HostKeyDecisionErrorCode.NotFound);
        (await askTask).Should().Be(HostKeyPromptOutcome.Rejected);
        await _trustedStore.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [TestMethod]
    public async Task AcceptAndSave_WhilePersisting_OwnsDecisionUntilCommit()
    {
        var persisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _trustedStore.AddAsync(default!, default).ReturnsForAnyArgs(persisted.Task);
        var (askTask, pending) = await StartAsync(HostKeyStatus.Unknown);

        var saving = DecideAsync(pending, HostKeyUserDecision.AcceptAndSave, false);
        var rejection = await DecideAsync(pending, HostKeyUserDecision.Reject, false);
        var duplicate = await DecideAsync(pending, HostKeyUserDecision.AcceptAndSave, false);

        rejection.ErrorCode.Should().Be(HostKeyDecisionErrorCode.NotFound);
        duplicate.ErrorCode.Should().Be(HostKeyDecisionErrorCode.NotFound);
        askTask.IsCompleted.Should().BeFalse();
        persisted.SetResult();
        (await saving).IsSuccess.Should().BeTrue();
        (await askTask).Should().Be(HostKeyPromptOutcome.Accepted);
        await _trustedStore.Received(1).AddAsync(pending.Challenge.Request, CancellationToken.None);
    }

    [TestMethod]
    public async Task AcceptAndSave_WhenPersistenceFails_RejectsPrompt()
    {
        _trustedStore.AddAsync(default!, default).ReturnsForAnyArgs(Task.FromException(new IOException("database failed")));
        var (askTask, pending) = await StartAsync(HostKeyStatus.Unknown);

        var save = () => DecideAsync(pending, HostKeyUserDecision.AcceptAndSave, false);

        await save.Should().ThrowAsync<IOException>();
        (await askTask).Should().Be(HostKeyPromptOutcome.Rejected);
        _broker.GetPending().Should().BeEmpty();
    }

    [TestMethod]
    public async Task AcceptAndSave_AfterClaim_TimeoutAndCancellationDoNotRejectCommittedKey()
    {
        var persisted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _trustedStore.AddAsync(default!, default).ReturnsForAnyArgs(persisted.Task);
        using var cancellation = new CancellationTokenSource();
        var askTask = _broker.AskAsync(TestChallenges.Create(HostKeyStatus.Unknown), cancellation.Token);
        var pending = await TestChallenges.WaitForPendingAsync(_broker);

        var saving = DecideAsync(pending, HostKeyUserDecision.AcceptAndSave, false);
        pending.Expire();
        await cancellation.CancelAsync();

        askTask.IsCompleted.Should().BeFalse();
        persisted.SetResult();
        (await saving).IsSuccess.Should().BeTrue();
        (await askTask).Should().Be(HostKeyPromptOutcome.Accepted);
    }

    [TestMethod]
    public async Task AcceptAndSave_WhenPromptExpired_DoesNotPersist()
    {
        _broker = TestChallenges.CreateBroker(TimeSpan.FromMilliseconds(50));
        _handler = new HostKeyDecisionHandler(_broker, _trustedStore, NullLogger<HostKeyDecisionHandler>.Instance);
        var (askTask, pending) = await StartAsync(HostKeyStatus.Unknown);
        (await askTask).Should().Be(HostKeyPromptOutcome.TimedOut);

        var result = await DecideAsync(pending, HostKeyUserDecision.AcceptAndSave, false);

        result.ErrorCode.Should().Be(HostKeyDecisionErrorCode.NotFound);
        await _trustedStore.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [TestMethod]
    public async Task AcceptAndSave_WhenPromptCancelledBeforeClaim_DoesNotPersist()
    {
        using var cancellation = new CancellationTokenSource();
        var askTask = _broker.AskAsync(TestChallenges.Create(HostKeyStatus.Unknown), cancellation.Token);
        var pending = await TestChallenges.WaitForPendingAsync(_broker);
        await cancellation.CancelAsync();

        var result = await DecideAsync(pending, HostKeyUserDecision.AcceptAndSave, false);

        result.ErrorCode.Should().Be(HostKeyDecisionErrorCode.NotFound);
        await _trustedStore.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        var ask = async () => await askTask;
        await ask.Should().ThrowAsync<OperationCanceledException>();
    }

    private async Task<(Task<HostKeyPromptOutcome> AskTask, PendingHostKeyDecision Pending)> StartAsync(HostKeyStatus status)
    {
        var challenge = TestChallenges.Create(status);
        var askTask = _broker.AskAsync(challenge, CancellationToken.None);
        var pending = await TestChallenges.WaitForPendingAsync(_broker);
        return (askTask, pending);
    }

    private Task<HostKeyDecisionResult> DecideAsync(PendingHostKeyDecision pending, HostKeyUserDecision decision, bool confirmReplace)
    {
        var command = new HostKeyDecisionCommand(pending.Id, decision, confirmReplace);
        return _handler.DecideAsync(command, CancellationToken.None);
    }
}
