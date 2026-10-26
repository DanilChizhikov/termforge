using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Core.Terminal;
using DTech.HostKeys.Decisions;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class HostKeyTrustFlowTests
{
    private static readonly TerminalSize InitialSize = new(80, 24);
    private static readonly TimeSpan LongDecisionTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ShortDecisionTimeout = TimeSpan.FromSeconds(1);

    [TestMethod]
    public async Task OpenTerminal_WithKeyInKnownHosts_ConnectsWithoutPrompt()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var context = HostKeyTrustContext.Create(LongDecisionTimeout);
        var target = cluster.Target.CreateKeyTarget();
        var knownHostsLine = OpenSshPublicKey.ToKnownHostsLine(cluster.Target.Identity.HostPublicKeyLine, target.Endpoint);
        context.WriteKnownHosts(knownHostsLine);

        var result = await OpenTerminalAsync(context, target);
        await using var session = result.IsSuccess ? result.Value : null;

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        context.Broker.GetPending().Should().BeEmpty();
    }

    [TestMethod]
    public async Task ConnectSftp_WithHashedKnownHostsEntry_ConnectsWithoutPrompt()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var context = HostKeyTrustContext.Create(ShortDecisionTimeout);
        var target = cluster.Target.CreateKeyTarget();
        var hashedHost = HostKeyTrustContext.CreateHashedHost(target.Endpoint);
        var keyFields = cluster.Target.Identity.HostPublicKeyLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        context.WriteKnownHosts($"{hashedHost} {keyFields[0]} {keyFields[1]}");

        var result = await ConnectSftpAsync(context, target);
        await using var client = result.IsSuccess ? result.Value : null;

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [TestMethod]
    public async Task ConnectSftp_WithUnknownKeyAndNoDecision_FailsClosedAfterTimeout()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var context = HostKeyTrustContext.Create(ShortDecisionTimeout);
        var target = cluster.Target.CreateKeyTarget();

        var result = await ConnectSftpAsync(context, target);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.HostKeyRejected);
        result.Error.Message.Should().Contain("no decision was made in time");
        result.Error.PresentedHostKey.Should().Be(cluster.Target.Identity.HostKey);
        context.Broker.GetPending().Should().BeEmpty();
        context.TrustedStore.GetTrustedHostKeys(target.Endpoint).Should().BeEmpty();
    }

    [TestMethod]
    public async Task OpenTerminal_WithUnknownKeyRejectedByUser_IsRejected()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var context = HostKeyTrustContext.Create(LongDecisionTimeout);
        var target = cluster.Target.CreateKeyTarget();

        var connectTask = OpenTerminalAsync(context, target);
        var pending = await context.WaitForPendingAsync(target.Endpoint);
        var decision = await context.DecideAsync(pending, HostKeyUserDecision.Reject);
        var result = await connectTask;

        decision.IsSuccess.Should().BeTrue();
        pending.Challenge.Verification.Status.Should().Be(HostKeyStatus.Unknown);
        pending.Challenge.Request.HostKey.Should().Be(cluster.Target.Identity.HostKey);
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.HostKeyRejected);
    }

    [TestMethod]
    public async Task OpenTerminal_WithUnknownKeyAcceptedAndSaved_ConnectsAndIsTrustedNextTime()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var context = HostKeyTrustContext.Create(LongDecisionTimeout);
        var target = cluster.Target.CreateKeyTarget();

        var connectTask = OpenTerminalAsync(context, target);
        var pending = await context.WaitForPendingAsync(target.Endpoint);
        await context.DecideAsync(pending, HostKeyUserDecision.AcceptAndSave);
        var result = await connectTask;
        await using var session = result.IsSuccess ? result.Value : null;
        var secondResult = await ConnectSftpAsync(context, target);
        await using var client = secondResult.IsSuccess ? secondResult.Value : null;

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        session!.IsOpen.Should().BeTrue();
        secondResult.IsSuccess.Should().BeTrue(secondResult.Error?.Message);
        context.TrustedStore.GetTrustedHostKeys(target.Endpoint).Should().Equal(cluster.Target.Identity.HostKey);
    }

    [TestMethod]
    public async Task ConnectSftp_WithUnknownKeyAcceptedOnce_ConnectsAndPromptsAgainNextTime()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var context = HostKeyTrustContext.Create(LongDecisionTimeout);
        var target = cluster.Target.CreateKeyTarget();

        var connectTask = ConnectSftpAsync(context, target);
        var pending = await context.WaitForPendingAsync(target.Endpoint);
        await context.DecideAsync(pending, HostKeyUserDecision.AcceptOnce);
        var result = await connectTask;
        await using var client = result.IsSuccess ? result.Value : null;
        var secondConnectTask = ConnectSftpAsync(context, target);
        var secondPending = await context.WaitForPendingAsync(target.Endpoint);
        await context.DecideAsync(secondPending, HostKeyUserDecision.Reject);
        var secondResult = await secondConnectTask;

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        secondPending.Id.Should().NotBe(pending.Id);
        secondResult.IsSuccess.Should().BeFalse();
        context.TrustedStore.GetTrustedHostKeys(target.Endpoint).Should().BeEmpty();
    }

    [TestMethod]
    public async Task ConnectSftp_WithChangedKey_IsBlockedAndCannotBeAcceptedOnce()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var context = HostKeyTrustContext.Create(LongDecisionTimeout);
        var target = cluster.Target.CreateKeyTarget();
        var staleKnownHostsLine = OpenSshPublicKey.ToKnownHostsLine(cluster.Jump.Identity.HostPublicKeyLine, target.Endpoint);
        context.WriteKnownHosts(staleKnownHostsLine);

        var connectTask = ConnectSftpAsync(context, target);
        var pending = await context.WaitForPendingAsync(target.Endpoint);
        var acceptOnce = await context.DecideAsync(pending, HostKeyUserDecision.AcceptOnce);
        var replaceWithoutConfirmation = await context.DecideAsync(pending, HostKeyUserDecision.Replace);
        await context.DecideAsync(pending, HostKeyUserDecision.Reject);
        var result = await connectTask;

        pending.Challenge.Verification.Status.Should().Be(HostKeyStatus.Changed);
        pending.Challenge.Verification.KnownHostKeys.Should().Equal(cluster.Jump.Identity.HostKey);
        acceptOnce.ErrorCode.Should().Be(HostKeyDecisionErrorCode.NotAllowed);
        replaceWithoutConfirmation.ErrorCode.Should().Be(HostKeyDecisionErrorCode.ConfirmationRequired);
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.HostKeyChanged);
        result.Error.Message.Should().Contain("WARNING").And.Contain(cluster.Jump.Identity.HostKey.Sha256Fingerprint);
        result.Error.PresentedHostKey.Should().Be(cluster.Target.Identity.HostKey);
    }

    [TestMethod]
    public async Task ConnectSftp_WithChangedKeyAndNoDecision_FailsClosedAsChanged()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var context = HostKeyTrustContext.Create(ShortDecisionTimeout);
        var target = cluster.Target.CreateKeyTarget();
        var staleKnownHostsLine = OpenSshPublicKey.ToKnownHostsLine(cluster.Jump.Identity.HostPublicKeyLine, target.Endpoint);
        context.WriteKnownHosts(staleKnownHostsLine);

        var result = await ConnectSftpAsync(context, target);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.HostKeyChanged);
    }

    [TestMethod]
    public async Task OpenTerminal_WithChangedKeyReplacedAfterConfirmation_Connects()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var context = HostKeyTrustContext.Create(LongDecisionTimeout);
        var target = cluster.Target.CreateKeyTarget();
        var staleKnownHostsLine = OpenSshPublicKey.ToKnownHostsLine(cluster.Jump.Identity.HostPublicKeyLine, target.Endpoint);
        context.WriteKnownHosts(staleKnownHostsLine);

        var connectTask = OpenTerminalAsync(context, target);
        var pending = await context.WaitForPendingAsync(target.Endpoint);
        var replace = await context.DecideAsync(pending, HostKeyUserDecision.Replace, true);
        var result = await connectTask;
        await using var session = result.IsSuccess ? result.Value : null;

        replace.IsSuccess.Should().BeTrue(replace.ErrorMessage);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        context.TrustedStore.GetTrustedHostKeys(target.Endpoint).Should().Equal(cluster.Target.Identity.HostKey);
        File.ReadAllText(context.KnownHostsPath).Trim().Should().Be(staleKnownHostsLine);
    }

    [TestMethod]
    public async Task ConnectSftp_WithRevokedKey_IsRejectedWithoutPrompt()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var context = HostKeyTrustContext.Create(LongDecisionTimeout);
        var target = cluster.Target.CreateKeyTarget();
        var knownHostsLine = OpenSshPublicKey.ToKnownHostsLine(cluster.Target.Identity.HostPublicKeyLine, target.Endpoint);
        context.WriteKnownHosts(knownHostsLine, $"@revoked {knownHostsLine}");

        var result = await ConnectSftpAsync(context, target);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.HostKeyRejected);
        result.Error.Message.Should().Contain("revoked");
        context.Broker.GetPending().Should().BeEmpty();
    }

    [TestMethod]
    public async Task ConnectSftp_ThroughJumpHostWithUnknownKeys_PromptsForEachHop()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var context = HostKeyTrustContext.Create(LongDecisionTimeout);
        var jumpHost = cluster.Jump.CreateKeyTarget();
        var target = cluster.Target.CreateNetworkKeyTarget() with { JumpHost = jumpHost };

        var connectTask = ConnectSftpAsync(context, target);
        var jumpPending = await context.WaitForPendingAsync(jumpHost.Endpoint);
        await context.DecideAsync(jumpPending, HostKeyUserDecision.AcceptOnce);
        var targetPending = await context.WaitForPendingAsync(target.Endpoint);
        await context.DecideAsync(targetPending, HostKeyUserDecision.AcceptOnce);
        var result = await connectTask;
        await using var client = result.IsSuccess ? result.Value : null;

        jumpPending.Challenge.Request.HostKey.Should().Be(cluster.Jump.Identity.HostKey);
        targetPending.Challenge.Request.HostKey.Should().Be(cluster.Target.Identity.HostKey);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    private static Task<SshResult<ITerminalSession>> OpenTerminalAsync(HostKeyTrustContext context, SshConnectionTarget target)
    {
        var factory = context.Get<ITerminalSessionFactory>();
        var request = new TerminalSessionRequest(target, InitialSize, TerminalSessionRequest.DefaultTerminalType);
        return factory.OpenAsync(request, CancellationToken.None);
    }

    private static Task<SshResult<IRemoteFileClient>> ConnectSftpAsync(HostKeyTrustContext context, SshConnectionTarget target)
    {
        var factory = context.Get<IRemoteFileClientFactory>();
        return factory.ConnectAsync(target, CancellationToken.None);
    }
}
