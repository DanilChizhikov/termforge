using System.Text;
using DTech.Core.Connections;
using DTech.Core.Credentials;
using DTech.Core.Editor.Sudo;
using DTech.Core.NetworkDiagnostics;
using DTech.Core.Safety;
using DTech.Core.SavedConnections;
using DTech.Entrypoint.NetworkDiagnostics;
using FluentAssertions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Diagnostics;

[TestClass]
public sealed class RemoteNetworkActionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Prepare_DoesNotInstallUntilConfirmedAndBindsPlanToServer()
    {
        var fixture = new Fixture();
        var request = fixture.Request();

        var plan = await fixture.Service.PrepareAsync(request, TestContext.CancellationToken);

        plan.Packages.Should().Contain("dnsutils");
        plan.Command.Should().StartWith("sudo -n ");
        fixture.Commands.Should().ContainSingle();
        var execution = new RemoteNetworkActionExecution { PlanId = plan.Id, ConfirmationToken = "wrong" };
        var refused = () => fixture.Service.ExecuteAsync(execution, TestContext.CancellationToken);
        await refused.Should().ThrowAsync<UnauthorizedAccessException>();
        fixture.Commands.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Execute_ConfirmationIsSingleUseAndPasswordIsOnlySentThroughStdin()
    {
        var fixture = new Fixture();
        var request = fixture.Request() with { UsePassword = true };
        var plan = await fixture.Service.PrepareAsync(request, TestContext.CancellationToken);
        var execution = new RemoteNetworkActionExecution { PlanId = plan.Id, ConfirmationToken = plan.ConfirmationToken, Password = "test-password" };

        var result = await fixture.Service.ExecuteAsync(execution, TestContext.CancellationToken);

        result.Outcome.Status.Should().Be(NetworkProbeOutcome.Responded);
        fixture.Commands.Should().OnlyContain(command => !command.Contains("test-password", StringComparison.Ordinal));
        fixture.Inputs.Should().Contain("test-password\n");
        var repeat = () => fixture.Service.ExecuteAsync(execution, TestContext.CancellationToken);
        await repeat.Should().ThrowAsync<ArgumentException>();
        execution.ToString().Should().NotContain("test-password");
    }

    [TestMethod]
    public async Task Execute_ReadOnlyOrChangedConnection_RequiresNewPlan()
    {
        var fixture = new Fixture();
        var plan = await fixture.Service.PrepareAsync(fixture.Request(), TestContext.CancellationToken);
        fixture.Access.IsReadOnlyAsync(fixture.Connection.Id, Arg.Any<CancellationToken>()).Returns(true);
        var execution = new RemoteNetworkActionExecution { PlanId = plan.Id, ConfirmationToken = plan.ConfirmationToken };

        var action = () => fixture.Service.ExecuteAsync(execution, TestContext.CancellationToken);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        fixture.Commands.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Execute_ChangedRevision_DoesNotRunApprovedCommand()
    {
        var fixture = new Fixture();
        var plan = await fixture.Service.PrepareAsync(fixture.Request(), TestContext.CancellationToken);
        fixture.Connection = fixture.Connection with { UpdatedAt = DateTime.UtcNow.AddSeconds(1) };
        var execution = new RemoteNetworkActionExecution { PlanId = plan.Id, ConfirmationToken = plan.ConfirmationToken };

        var action = () => fixture.Service.ExecuteAsync(execution, TestContext.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
        fixture.Commands.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Prepare_ReadOnlyServer_DoesNotOpenSsh()
    {
        var fixture = new Fixture();
        fixture.Access.IsReadOnlyAsync(fixture.Connection.Id, Arg.Any<CancellationToken>()).Returns(true);

        var action = () => fixture.Service.PrepareAsync(fixture.Request(), TestContext.CancellationToken);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        fixture.Commands.Should().BeEmpty();
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            var repository = Substitute.For<ISavedConnectionRepository>();
            var draft = new SavedConnectionDraft { Name = "point", Host = "example.test", UserName = "tester", AuthMethod = ConnectionAuthMethod.Agent };
            Connection = new SavedConnection { Id = Guid.NewGuid(), Details = draft, UpdatedAt = DateTime.UtcNow };
            repository.FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => Connection);
            repository.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => new[] { Connection });
            var credentials = Substitute.For<ICredentialStore>();
            var passwords = new ConnectionPasswordStore(credentials);
            var targets = new ConnectionTargetFactory(passwords);
            var runners = Substitute.For<IRemoteShellCommandRunnerFactory>();
            var runner = Substitute.For<IRemoteShellCommandRunner>();
            runner.RunAsync(Arg.Any<RemoteShellCommand>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                var command = call.Arg<RemoteShellCommand>();
                Commands.Add(command.Text);
                Inputs.Add(Encoding.UTF8.GetString(command.StandardInput.Span));
                var discovery = command.Text.Contains("uname -s", StringComparison.Ordinal);
                var output = discovery ? "Linux\napt-get\n1000\n" : "installed";
                var bytes = Encoding.UTF8.GetBytes(output);
                var result = new RemoteShellCommandResult(!discovery && FailInstall ? 1 : 0, bytes, !discovery && FailInstall ? "incorrect password: test-password" : "");
                return SshResult<RemoteShellCommandResult>.Success(result);
            });
            runners.ConnectAsync(Arg.Any<SshConnectionTarget>(), Arg.Any<CancellationToken>()).Returns(SshResult<IRemoteShellCommandRunner>.Success(runner));
            var connector = new RemoteShellConnector(repository, targets, runners);
            var confirmations = new ConfirmationTokenStore(Clock);
            var gate = new ServerWriteGate(Access, confirmations);
            var plans = new RemoteNetworkActionPlans(Clock);
            Service = new RemoteNetworkActionService(repository, connector, gate, confirmations, plans);
        }
        public SavedConnection Connection { get; set; }
        public IServerAccessModeReader Access { get; } = Substitute.For<IServerAccessModeReader>();
        public List<string> Commands { get; } = [];
        public List<string> Inputs { get; } = [];
        public bool FailInstall { get; set; }
        public TestClock Clock { get; } = new();
        public RemoteNetworkActionService Service { get; }
        public RemoteNetworkActionRequest Request() => new() { ConnectionId = Connection.Id, Protocols = ["dns"] };
    }

    [TestMethod]
    public async Task Execute_PasswordError_IsRedactedAndUtilitiesAreRechecked()
    {
        var fixture = new Fixture { FailInstall = true };
        var request = fixture.Request() with { UsePassword = true };
        var plan = await fixture.Service.PrepareAsync(request, TestContext.CancellationToken);
        var execution = new RemoteNetworkActionExecution { PlanId = plan.Id, ConfirmationToken = plan.ConfirmationToken, Password = "test-password" };

        var result = await fixture.Service.ExecuteAsync(execution, TestContext.CancellationToken);

        result.Outcome.Status.Should().Be(NetworkProbeOutcome.Error);
        result.Outcome.Detail.Should().NotContain("test-password");
        fixture.Commands.Should().HaveCount(3);
        fixture.Commands[^1].Should().Contain("uname -s");
    }

    [TestMethod]
    public async Task Execute_ExpiredPlan_IsRejected()
    {
        var fixture = new Fixture();
        var plan = await fixture.Service.PrepareAsync(fixture.Request(), TestContext.CancellationToken);
        fixture.Clock.Now += TimeSpan.FromMinutes(3);
        var execution = new RemoteNetworkActionExecution { PlanId = plan.Id, ConfirmationToken = plan.ConfirmationToken };
        var action = () => fixture.Service.ExecuteAsync(execution, TestContext.CancellationToken);

        await action.Should().ThrowAsync<ArgumentException>();
        fixture.Commands.Should().ContainSingle();
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
