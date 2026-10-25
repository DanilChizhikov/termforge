using DTech.Core.Connections;
using DTech.Core.Credentials;
using DTech.Core.SavedConnections;
using DTech.Core.Terminal;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.SavedConnections;

[TestClass]
public sealed class ConnectionTesterTests
{
    private const string Password = "never-leak-this-1";

    private InMemorySavedConnectionRepository _repository = null!;
    private InMemoryCredentialStore _credentials = null!;
    private ITerminalSessionFactory _sessionFactory = null!;
    private SavedConnectionService _service = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _repository = new InMemorySavedConnectionRepository();
        _credentials = new InMemoryCredentialStore();
        _sessionFactory = Substitute.For<ITerminalSessionFactory>();
        _service = new SavedConnectionService(_repository, new ConnectionPasswordStore(_credentials), TimeProvider.System);
    }

    [TestMethod]
    public async Task Test_WhenSessionOpens_ReturnsConnectedWithLatencyAndRecordsLastConnection()
    {
        var connection = await CreateAsync(AgentDraft("web"));
        var session = Substitute.For<ITerminalSession>();
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(SshResult<ITerminalSession>.Success(session));

        var result = await CreateTester().TestAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(ConnectionTestState.Connected);
        result.Value.LatencyMilliseconds.Should().NotBeNull();
        result.Value.LastConnectedAt.Should().NotBeNull();
        _repository.Snapshot.Single().LastConnectedAt.Should().Be(result.Value.LastConnectedAt);
        await session.Received(1).DisposeAsync();
    }

    [TestMethod]
    [DataRow(SshErrorKind.AuthenticationFailed, ConnectionTestState.AuthenticationFailed)]
    [DataRow(SshErrorKind.PrivateKeyUnusable, ConnectionTestState.AuthenticationFailed)]
    [DataRow(SshErrorKind.AgentUnavailable, ConnectionTestState.AuthenticationFailed)]
    [DataRow(SshErrorKind.ConnectionFailed, ConnectionTestState.Unreachable)]
    [DataRow(SshErrorKind.HostKeyRejected, ConnectionTestState.HostKeyRejected)]
    [DataRow(SshErrorKind.ChannelFailed, ConnectionTestState.Failed)]
    public async Task Test_WhenSessionFails_MapsErrorKindToState(SshErrorKind kind, ConnectionTestState expected)
    {
        var connection = await CreateAsync(AgentDraft("web"));
        var error = new SshError(kind, "transport said no");
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(SshResult<ITerminalSession>.Failure(error));

        var result = await CreateTester().TestAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(expected);
        result.Value.Message.Should().NotBeNullOrWhiteSpace();
        result.Value.Detail.Should().Be("transport said no");
        result.Value.LatencyMilliseconds.Should().BeNull();
        _repository.Snapshot.Single().LastConnectedAt.Should().BeNull();
    }

    [TestMethod]
    public async Task Test_WhenNoAnswerBeforeTimeout_ReturnsTimeout()
    {
        var connection = await CreateAsync(AgentDraft("web"));
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(call => WaitUntilCancelled(call.Arg<CancellationToken>()));
        var tester = CreateTester(TimeSpan.FromMilliseconds(100));

        var result = await tester.TestAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(ConnectionTestState.Timeout);
        result.Value.Message.Should().Contain("No answer");
    }

    [TestMethod]
    public async Task Test_WhenCallerCancels_Throws()
    {
        var connection = await CreateAsync(AgentDraft("web"));
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(call => WaitUntilCancelled(call.Arg<CancellationToken>()));
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var act = () => CreateTester().TestAsync(connection.Id, source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task Test_OfUnknownConnection_ReturnsNotFound()
    {
        var result = await CreateTester().TestAsync(Guid.NewGuid(), TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.NotFound);
    }

    [TestMethod]
    public async Task Test_WithPasswordAuthentication_PassesStoredPasswordToTransportAndNeverReturnsIt()
    {
        var connection = await CreateAsync(AgentDraft("db") with { AuthMethod = ConnectionAuthMethod.Password }, Password);
        TerminalSessionRequest? sent = null;
        var error = new SshError(SshErrorKind.AuthenticationFailed, "Connecting failed");
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(call =>
        {
            sent = call.Arg<TerminalSessionRequest>();
            return SshResult<ITerminalSession>.Failure(error);
        });

        var result = await CreateTester().TestAsync(connection.Id, TestContext.CancellationToken);

        sent!.Target.Authentication.Should().BeOfType<PasswordAuthentication>().Which.Password.Should().Be(Password);
        result.Value.State.Should().Be(ConnectionTestState.AuthenticationFailed);
        result.Value.Message.Should().Contain("password");
        $"{result.Value.Message}{result.Value.Detail}".Should().NotContain(Password);
    }

    [TestMethod]
    public async Task Test_WhenPasswordIsMissing_ReportsCredentialsUnavailableWithoutCallingTransport()
    {
        var draft = AgentDraft("db") with { AuthMethod = ConnectionAuthMethod.Password };
        var connection = await CreateAsync(draft);

        var result = await CreateTester().TestAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(ConnectionTestState.CredentialsUnavailable);
        result.Value.Message.Should().Contain("No password is stored");
        await _sessionFactory.DidNotReceiveWithAnyArgs().OpenAsync(default!, default);
    }

    [TestMethod]
    public async Task Test_WhenCredentialStoreIsLocked_ReportsCredentialsUnavailable()
    {
        var connection = await CreateAsync(AgentDraft("db") with { AuthMethod = ConnectionAuthMethod.Password }, Password);
        _credentials.FailureToReturn = CredentialErrors.VaultLocked();

        var result = await CreateTester().TestAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(ConnectionTestState.CredentialsUnavailable);
        result.Value.Message.Should().Contain("locked");
    }

    [TestMethod]
    public async Task Test_WhenKeyFileIsMissing_ReportsCredentialsUnavailable()
    {
        var draft = AgentDraft("web") with { AuthMethod = ConnectionAuthMethod.KeyFile, PrivateKeyPath = "/definitely/missing/key" };
        var connection = await CreateAsync(draft);

        var result = await CreateTester().TestAsync(connection.Id, TestContext.CancellationToken);

        result.Value.State.Should().Be(ConnectionTestState.CredentialsUnavailable);
        result.Value.Message.Should().Contain("/definitely/missing/key");
    }

    [TestMethod]
    public async Task Test_WithJumpHost_BuildsTargetChain()
    {
        var bastion = await CreateAsync(AgentDraft("bastion"));
        var inner = await CreateAsync(AgentDraft("inner") with { JumpHostId = bastion.Id });
        TerminalSessionRequest? sent = null;
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(call =>
        {
            sent = call.Arg<TerminalSessionRequest>();
            return SshResult<ITerminalSession>.Failure(new SshError(SshErrorKind.ConnectionFailed, "down"));
        });

        await CreateTester().TestAsync(inner.Id, TestContext.CancellationToken);

        sent!.Target.JumpHost.Should().NotBeNull();
        sent.Target.JumpHost!.Endpoint.Host.Should().Be("example.org");
    }

    private static SavedConnectionDraft AgentDraft(string name)
    {
        return new SavedConnectionDraft { Name = name, Host = "example.org", UserName = "deploy" };
    }

    private static async Task<SshResult<ITerminalSession>> WaitUntilCancelled(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return SshResult<ITerminalSession>.Failure(new SshError(SshErrorKind.Cancelled, "cancelled"));
        }

        return SshResult<ITerminalSession>.Failure(new SshError(SshErrorKind.OperationFailed, "unreachable"));
    }

    private ConnectionTester CreateTester(TimeSpan? timeout = null)
    {
        var passwordStore = new ConnectionPasswordStore(_credentials);
        var targetFactory = new ConnectionTargetFactory(passwordStore);
        var options = new ConnectionTestOptions(timeout ?? ConnectionTestOptions.DefaultTimeout);
        return new ConnectionTester(_repository, targetFactory, _sessionFactory, TimeProvider.System, options);
    }

    private async Task<SavedConnection> CreateAsync(SavedConnectionDraft draft, string? password = null)
    {
        var result = await _service.CreateAsync(new SavedConnectionSubmission(draft, password), TestContext.CancellationToken);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value;
    }
}
