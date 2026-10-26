using DTech.Core.Connections;
using DTech.Core.Credentials;
using DTech.Core.SavedConnections;
using DTech.Core.Terminal;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
[DoNotParallelize]
public sealed class ConnectionTesterTests
{
    private const string WrongPassword = "wrong-secret-value";

    private readonly List<SavedConnection> _connections = [];
    private readonly Dictionary<string, string> _secrets = [];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Test_WithAuthorizedKeyAndTrustedHostKey_ReturnsConnectedWithLatency()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var connection = AddConnection(KeyDraft("target", cluster.Target, cluster.Target.Identity.PrivateKeyPath));

        var result = await TestAsync(connection, SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey));

        result.State.Should().Be(ConnectionTestState.Connected, result.Detail);
        result.LatencyMilliseconds.Should().BeGreaterThanOrEqualTo(0);
        _connections.Single().LastConnectedAt.Should().NotBeNull();
    }

    [TestMethod]
    public async Task Test_WithStoredPassword_ReturnsConnected()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var connection = AddPasswordConnection(cluster, SshServerContainer.Password);

        var result = await TestAsync(connection, SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey));

        result.State.Should().Be(ConnectionTestState.Connected, result.Detail);
    }

    [TestMethod]
    public async Task Test_WithWrongStoredPassword_ReturnsAuthenticationFailedWithoutLeakingPassword()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var connection = AddPasswordConnection(cluster, WrongPassword);

        var result = await TestAsync(connection, SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey));

        result.State.Should().Be(ConnectionTestState.AuthenticationFailed);
        result.Message.Should().Contain("Authentication failed");
        $"{result.Message}{result.Detail}".Should().NotContain(WrongPassword);
        _connections.Single().LastConnectedAt.Should().BeNull();
    }

    [TestMethod]
    public async Task Test_WithUnauthorizedKey_ReturnsAuthenticationFailed()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var connection = AddConnection(KeyDraft("target", cluster.Target, cluster.Jump.Identity.PrivateKeyPath));

        var result = await TestAsync(connection, SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey));

        result.State.Should().Be(ConnectionTestState.AuthenticationFailed);
    }

    [TestMethod]
    public async Task Test_WithUntrustedHostKey_FailsClosedWithHostKeyRejected()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var connection = AddConnection(KeyDraft("target", cluster.Target, cluster.Target.Identity.PrivateKeyPath));

        var result = await TestAsync(connection, SshTransportTestServices.CreateRejectingVerifier());

        result.State.Should().Be(ConnectionTestState.HostKeyRejected);
        result.Message.Should().Contain("not trusted");
    }

    [TestMethod]
    public async Task Test_ThroughSavedJumpHost_ReturnsConnected()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var bastion = AddConnection(KeyDraft("bastion", cluster.Jump, cluster.Jump.Identity.PrivateKeyPath));
        var behind = KeyDraft("behind", cluster.Target, cluster.Target.Identity.PrivateKeyPath) with
        {
            Host = cluster.Target.NetworkEndpoint.Host,
            Port = cluster.Target.NetworkEndpoint.Port,
            JumpHostId = bastion.Id,
        };
        var connection = AddConnection(behind);
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey, cluster.Jump.Identity.HostKey);

        var result = await TestAsync(connection, verifier);

        result.State.Should().Be(ConnectionTestState.Connected, result.Detail);
    }

    [TestMethod]
    public async Task Test_WhenNothingListens_ReturnsUnreachable()
    {
        var draft = new SavedConnectionDraft
        {
            Name = "closed",
            Host = "127.0.0.1",
            Port = 1,
            UserName = "nobody",
            AuthMethod = ConnectionAuthMethod.Password,
        };
        var connection = AddConnection(draft, true);
        _secrets[PasswordCredentialKey.For(connection.Id).Value] = "irrelevant";

        var result = await TestAsync(connection, SshTransportTestServices.CreateRejectingVerifier());

        result.State.Should().Be(ConnectionTestState.Unreachable);
        result.Message.Should().Contain("127.0.0.1:1");
    }

    private static SavedConnectionDraft KeyDraft(string name, SshServerContainer server, string keyPath)
    {
        return new SavedConnectionDraft
        {
            Name = name,
            Host = server.Endpoint.Host,
            Port = server.Endpoint.Port,
            UserName = SshServerContainer.UserName,
            AuthMethod = ConnectionAuthMethod.KeyFile,
            PrivateKeyPath = keyPath,
        };
    }

    private SavedConnection AddPasswordConnection(SshServerCluster cluster, string password)
    {
        var draft = KeyDraft("password", cluster.Target, cluster.Target.Identity.PrivateKeyPath) with
        {
            AuthMethod = ConnectionAuthMethod.Password,
            PrivateKeyPath = null,
        };
        var connection = AddConnection(draft, true);
        _secrets[PasswordCredentialKey.For(connection.Id).Value] = password;
        return connection;
    }

    private SavedConnection AddConnection(SavedConnectionDraft draft, bool hasPassword = false)
    {
        var connection = new SavedConnection { Id = Guid.NewGuid(), Details = draft, HasPassword = hasPassword };
        _connections.Add(connection);
        return connection;
    }

    private async Task<ConnectionTestResult> TestAsync(SavedConnection connection, IHostKeyVerifier verifier)
    {
        await using var services = SshTransportTestServices.Create(verifier);
        var sessionFactory = services.GetRequiredService<ITerminalSessionFactory>();
        var passwordStore = new ConnectionPasswordStore(CreateCredentialStore());
        var targetFactory = new ConnectionTargetFactory(passwordStore);
        var tester = new ConnectionTester(CreateRepository(), targetFactory, sessionFactory, TimeProvider.System, ConnectionTestOptions.Default);

        var result = await tester.TestAsync(connection.Id, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value;
    }

    private ISavedConnectionRepository CreateRepository()
    {
        var repository = Substitute.For<ISavedConnectionRepository>();
        repository.ListAsync(default).ReturnsForAnyArgs(call => Task.FromResult<IReadOnlyList<SavedConnection>>(_connections.ToList()));
        repository.RecordConnectedAsync(default, default, default).ReturnsForAnyArgs(call =>
        {
            var index = _connections.FindIndex(item => item.Id == call.Arg<Guid>());
            _connections[index] = _connections[index] with { LastConnectedAt = call.ArgAt<DateTime>(1) };
            return Task.FromResult(true);
        });
        return repository;
    }

    private ICredentialStore CreateCredentialStore()
    {
        var store = Substitute.For<ICredentialStore>();
        store.Read(default, default).ReturnsForAnyArgs(call =>
        {
            var key = call.Arg<CredentialKey>();
            var secret = Secret.FromString(_secrets[key.Value]);
            return Task.FromResult(CredentialResult<Secret>.Success(secret));
        });
        return store;
    }
}
