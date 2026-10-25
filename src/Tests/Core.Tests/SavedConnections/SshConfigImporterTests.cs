using DTech.Core.SavedConnections;
using DTech.Core.SshConfig;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;

namespace DTech.Core.Tests.SavedConnections;

[TestClass]
public sealed class SshConfigImporterTests
{
    private TemporaryDirectory _home = null!;
    private InMemorySavedConnectionRepository _repository = null!;
    private string _configPath = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _home = new TemporaryDirectory();
        _repository = new InMemorySavedConnectionRepository();
        _configPath = _home.Combine(".ssh/config");
    }

    [TestCleanup]
    public void Cleanup()
    {
        _home.Dispose();
    }

    [TestMethod]
    public async Task Import_WhenConfigIsMissing_ReturnsSshConfigNotFound()
    {
        var importer = CreateImporter();

        var result = await importer.ImportAsync(TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.SshConfigNotFound);
        result.Error.Message.Should().Contain(_configPath);
    }

    [TestMethod]
    public async Task Import_CreatesConnectionsWithAliasPortUserAndKey()
    {
        WriteConfig("""
            Host web
                HostName 203.0.113.10
                Port 2222
                User deploy
                IdentityFile /keys/web
            Host plain
                HostName plain.example.org
            """);

        var result = await CreateImporter().ImportAsync(TestContext.CancellationToken);

        result.Value.Created.Should().Be(2);
        result.Value.Updated.Should().Be(0);
        var web = _repository.Snapshot.Single(connection => connection.Details.Name == "web").Details;
        web.Host.Should().Be("203.0.113.10");
        web.Port.Should().Be(2222);
        web.UserName.Should().Be("deploy");
        web.AuthMethod.Should().Be(ConnectionAuthMethod.KeyFile);
        web.PrivateKeyPath.Should().Be("/keys/web");
        web.SshAlias.Should().Be("web");
        var plain = _repository.Snapshot.Single(connection => connection.Details.Name == "plain").Details;
        plain.AuthMethod.Should().Be(ConnectionAuthMethod.Agent);
    }

    [TestMethod]
    public async Task Import_ResolvesProxyJumpToImportedAlias()
    {
        WriteConfig("""
            Host inner
                ProxyJump bastion
            Host bastion
            """);

        var result = await CreateImporter().ImportAsync(TestContext.CancellationToken);

        var bastion = _repository.Snapshot.Single(connection => connection.Details.Name == "bastion");
        var inner = _repository.Snapshot.Single(connection => connection.Details.Name == "inner");
        inner.Details.JumpHostId.Should().Be(bastion.Id);
        result.Value.Issues.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Import_ReportsProxyJumpThatIsNotAnAliasOrHasSeveralHops()
    {
        WriteConfig("""
            Host literal
                ProxyJump ops@jump.example.org:2200
            Host chain
                ProxyJump one,two
            Host one
            Host two
            """);

        var result = await CreateImporter().ImportAsync(TestContext.CancellationToken);

        var issues = result.Value.Issues.Where(issue => issue.Directive == "ProxyJump").ToList();
        issues.Should().HaveCount(2);
        issues.Should().Contain(issue => issue.AffectedAliases.Contains("literal") && issue.Reason.Contains("not a Host alias"));
        issues.Should().Contain(issue => issue.AffectedAliases.Contains("chain") && issue.Reason.Contains("several hops"));
        _repository.Snapshot.Should().OnlyContain(connection => connection.Details.JumpHostId == null);
    }

    [TestMethod]
    public async Task Import_ReportsProxyJumpLoopAndSkipsIt()
    {
        WriteConfig("""
            Host a
                ProxyJump b
            Host b
                ProxyJump a
            """);

        var result = await CreateImporter().ImportAsync(TestContext.CancellationToken);

        result.Value.Issues.Should().ContainSingle(issue => issue.Directive == "ProxyJump");
        _repository.Snapshot.Count(connection => connection.Details.JumpHostId != null).Should().Be(1);
    }

    [TestMethod]
    public async Task Import_PassesParserIssuesThrough()
    {
        WriteConfig("""
            Host web
                ForwardAgent yes
            """);

        var result = await CreateImporter().ImportAsync(TestContext.CancellationToken);

        result.Value.Issues.Should().ContainSingle(issue => issue.Directive == "ForwardAgent");
        _repository.Snapshot.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Import_Reload_UpdatesExistingByAliasAndKeepsNameGroupAndCreatedConnections()
    {
        WriteConfig("Host web\n    HostName 10.0.0.1\n");
        await CreateImporter().ImportAsync(TestContext.CancellationToken);
        var original = _repository.Snapshot.Single();
        await _repository.UpdateAsync(original with { Details = original.Details with { Name = "Renamed", GroupName = "prod" } }, TestContext.CancellationToken);
        WriteConfig("Host web\n    HostName 10.0.0.2\n    Port 2200\nHost extra\n");

        var result = await CreateImporter().ImportAsync(TestContext.CancellationToken);

        result.Value.Created.Should().Be(1);
        result.Value.Updated.Should().Be(1);
        var web = _repository.Snapshot.Single(connection => connection.Id == original.Id);
        web.Details.Name.Should().Be("Renamed");
        web.Details.GroupName.Should().Be("prod");
        web.Details.Host.Should().Be("10.0.0.2");
        web.Details.Port.Should().Be(2200);
    }

    [TestMethod]
    public async Task Import_Reload_WithoutChanges_ReportsNothingUpdated()
    {
        WriteConfig("Host web\n");
        await CreateImporter().ImportAsync(TestContext.CancellationToken);

        var result = await CreateImporter().ImportAsync(TestContext.CancellationToken);

        result.Value.Created.Should().Be(0);
        result.Value.Updated.Should().Be(0);
    }

    [TestMethod]
    public async Task Import_Reload_KeepsPasswordAuthenticationChosenByUser()
    {
        WriteConfig("Host web\n    IdentityFile /keys/web\n");
        await CreateImporter().ImportAsync(TestContext.CancellationToken);
        var original = _repository.Snapshot.Single();
        var passwordDetails = original.Details with { AuthMethod = ConnectionAuthMethod.Password, PrivateKeyPath = null };
        await _repository.UpdateAsync(original with { Details = passwordDetails, HasPassword = true }, TestContext.CancellationToken);

        await CreateImporter().ImportAsync(TestContext.CancellationToken);

        var after = _repository.Snapshot.Single();
        after.Details.AuthMethod.Should().Be(ConnectionAuthMethod.Password);
        after.HasPassword.Should().BeTrue();
    }

    [TestMethod]
    public async Task Import_WhenNameIsTakenByAnotherConnection_UsesDisambiguatedName()
    {
        var manual = new SavedConnection
        {
            Id = Guid.NewGuid(),
            Details = new SavedConnectionDraft { Name = "web", Host = "manual.example.org", UserName = "me" },
        };
        await _repository.AddAsync(manual, TestContext.CancellationToken);
        WriteConfig("Host web\n");

        await CreateImporter().ImportAsync(TestContext.CancellationToken);

        _repository.Snapshot.Select(connection => connection.Details.Name).Should().BeEquivalentTo("web", "web (ssh config)");
    }

    private void WriteConfig(string content)
    {
        _home.Write(".ssh/config", content);
    }

    private SshConfigImporter CreateImporter()
    {
        var environment = new SshConfigEnvironment(_home.Path, "localuser");
        var parser = new SshConfigParser(environment);
        var options = new SshConfigImportOptions(_configPath);
        return new SshConfigImporter(parser, _repository, options, TimeProvider.System);
    }
}
