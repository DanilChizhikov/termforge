using DTech.Core.Connections;
using DTech.Core.SavedConnections;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Terminal;

[TestClass]
public sealed class TerminalLauncherTests
{
    private const string Owner = "owner-session-token";

    private static readonly TerminalSize Size = new(100, 30);

    private InMemorySavedConnectionRepository _repository = null!;
    private ITerminalSessionFactory _sessionFactory = null!;
    private TerminalSessionManager _manager = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _repository = new InMemorySavedConnectionRepository();
        _sessionFactory = Substitute.For<ITerminalSessionFactory>();
        var session = Substitute.For<ITerminalSession>();
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(SshResult<ITerminalSession>.Success(session));
        _manager = new TerminalSessionManager(_sessionFactory, TimeProvider.System, TerminalSessionOptions.Default);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _manager.DisposeAsync();
    }

    [TestMethod]
    public async Task Launch_OpensSessionForSavedConnectionAndRecordsLastConnection()
    {
        var connection = await AddAsync(AgentDraft("web"), false);

        var launched = await CreateLauncher().LaunchAsync(new TerminalLaunchRequest(connection.Id, Owner, Size), TestContext.CancellationToken);

        launched.IsSuccess.Should().BeTrue(launched.Error?.Message);
        launched.Value.Descriptor.ConnectionId.Should().Be(connection.Id);
        launched.Value.Descriptor.ConnectionName.Should().Be("web");
        _repository.Snapshot.Single().LastConnectedAt.Should().Be(launched.Value.Descriptor.OpenedAt);
        await _sessionFactory.Received(1).OpenAsync(
            Arg.Is<TerminalSessionRequest>(request => request.Target.Endpoint.Host == "web.example.test" && request.Target.UserName == "deploy" && request.InitialSize == Size),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Launch_ForUnknownConnection_ReturnsNotFound()
    {
        var launched = await CreateLauncher().LaunchAsync(new TerminalLaunchRequest(Guid.NewGuid(), Owner, Size), TestContext.CancellationToken);

        launched.Error!.Code.Should().Be(TerminalErrorCode.NotFound);
        await _sessionFactory.DidNotReceiveWithAnyArgs().OpenAsync(default!, default);
    }

    [TestMethod]
    public async Task Launch_WithoutStoredPassword_ReturnsCredentialsUnavailable()
    {
        var draft = AgentDraft("db") with { AuthMethod = ConnectionAuthMethod.Password };
        var connection = await AddAsync(draft, false);

        var launched = await CreateLauncher().LaunchAsync(new TerminalLaunchRequest(connection.Id, Owner, Size), TestContext.CancellationToken);

        launched.Error!.Code.Should().Be(TerminalErrorCode.CredentialsUnavailable);
        launched.Error.Message.Should().Contain("No password is stored");
        _repository.Snapshot.Single().LastConnectedAt.Should().BeNull();
    }

    private TerminalLauncher CreateLauncher()
    {
        var passwordStore = new ConnectionPasswordStore(new InMemoryCredentialStore());
        var targetFactory = new ConnectionTargetFactory(passwordStore);
        return new TerminalLauncher(_repository, targetFactory, _manager);
    }

    private async Task<SavedConnection> AddAsync(SavedConnectionDraft draft, bool hasPassword)
    {
        var connection = new SavedConnection { Id = Guid.NewGuid(), Details = draft, HasPassword = hasPassword };
        await _repository.AddAsync(connection, TestContext.CancellationToken);
        return connection;
    }

    private static SavedConnectionDraft AgentDraft(string name)
    {
        return new SavedConnectionDraft
        {
            Name = name,
            Host = $"{name}.example.test",
            UserName = "deploy",
            AuthMethod = ConnectionAuthMethod.Agent,
        };
    }
}
