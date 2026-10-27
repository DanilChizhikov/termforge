using DTech.Core.SshConfig;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;

namespace DTech.Core.Tests.SshConfig;

[TestClass]
public sealed class SshConfigParserTests
{
    private const string LocalUser = "localuser";

    private TemporaryDirectory _home = null!;
    private SshConfigParser _parser = null!;

    [TestInitialize]
    public void Initialize()
    {
        _home = new TemporaryDirectory();
        var environment = new SshConfigEnvironment(_home.Path, LocalUser);
        _parser = new SshConfigParser(environment);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _home.Dispose();
    }

    [TestMethod]
    public void Parse_WhenFileIsMissing_ReportsFileNotFound()
    {
        var result = _parser.Parse(_home.Combine("missing"));

        result.FileFound.Should().BeFalse();
        result.Hosts.Should().BeEmpty();
    }

    [TestMethod]
    public void Parse_HostBlock_ReturnsAliasHostNamePortUserAndIdentityFile()
    {
        var path = _home.Write(".ssh/config", """
            Host web
                HostName 203.0.113.10
                Port 2222
                User deploy
                IdentityFile ~/.ssh/web_key
            """);

        var result = _parser.Parse(path);

        var host = result.Hosts.Should().ContainSingle().Subject;
        host.Alias.Should().Be("web");
        host.HostName.Should().Be("203.0.113.10");
        host.Port.Should().Be(2222);
        host.UserName.Should().Be("deploy");
        host.IdentityFile.Should().Be(Path.Combine(_home.Path, ".ssh/web_key"));
        result.Issues.Should().BeEmpty();
    }

    [TestMethod]
    public void Parse_HostWithoutOptions_UsesAliasAsHostNameDefaultPortAndLocalUser()
    {
        var path = _home.Write("config", "Host bare\n");

        var result = _parser.Parse(path);

        var host = result.Hosts.Should().ContainSingle().Subject;
        host.HostName.Should().Be("bare");
        host.Port.Should().Be(22);
        host.UserName.Should().Be(LocalUser);
        host.IdentityFile.Should().BeNull();
        host.ProxyJump.Should().BeNull();
    }

    [TestMethod]
    public void Parse_AcceptsEqualsSeparatorQuotesAndCaseInsensitiveKeywords()
    {
        var path = _home.Write("config", """
            host "quoted"
              HOSTNAME=example.org
              port = 2200
              IdentityFile "/keys/with space/id"
            """);

        var result = _parser.Parse(path);

        var host = result.Hosts.Should().ContainSingle().Subject;
        host.HostName.Should().Be("example.org");
        host.Port.Should().Be(2200);
        host.IdentityFile.Should().Be("/keys/with space/id");
    }

    [TestMethod]
    public void Parse_WildcardBlock_ProvidesDefaultsAndFirstValueWins()
    {
        var path = _home.Write("config", """
            Host web
                User specific
            Host *
                User fallback
                Port 2022
            Host db
                Port 3022
            """);

        var result = _parser.Parse(path);

        result.Hosts.Should().HaveCount(2);
        var web = result.Hosts.Single(host => host.Alias == "web");
        var db = result.Hosts.Single(host => host.Alias == "db");
        web.UserName.Should().Be("specific");
        web.Port.Should().Be(2022);
        db.UserName.Should().Be("fallback");
        db.Port.Should().Be(2022);
    }

    [TestMethod]
    public void Parse_GlobalDirectivesBeforeFirstHost_ApplyToEveryHost()
    {
        var path = _home.Write("config", """
            User everyone
            Host one
            Host two
                User override
            """);

        var result = _parser.Parse(path);

        result.Hosts.Single(host => host.Alias == "one").UserName.Should().Be("everyone");
        result.Hosts.Single(host => host.Alias == "two").UserName.Should().Be("everyone");
    }

    [TestMethod]
    public void Parse_NegatedAndWildcardPatterns_DoNotCreateAliasesAndHonourNegation()
    {
        var path = _home.Write("config", """
            Host prod-1 prod-2 !prod-2
                User first
            Host prod-*
                User prodwide
            Host *.internal
                User internal
            """);

        var result = _parser.Parse(path);

        result.Hosts.Select(host => host.Alias).Should().BeEquivalentTo("prod-1", "prod-2");
        result.Hosts.Single(host => host.Alias == "prod-1").UserName.Should().Be("first");
        result.Hosts.Single(host => host.Alias == "prod-2").UserName.Should().Be("prodwide");
    }

    [TestMethod]
    public void Parse_MultipleAliasesInOneHostLine_CreatesOneEntryPerAlias()
    {
        var path = _home.Write("config", """
            Host alpha beta
                HostName shared.example.org
            """);

        var result = _parser.Parse(path);

        result.Hosts.Select(host => host.Alias).Should().BeEquivalentTo("alpha", "beta");
        result.Hosts.Should().OnlyContain(host => host.HostName == "shared.example.org");
    }

    [TestMethod]
    public void Parse_ProxyJump_ReturnsRawValueAndNoneClearsIt()
    {
        var path = _home.Write("config", """
            Host bastion
            Host inner
                ProxyJump bastion
            Host direct
                ProxyJump none
            """);

        var result = _parser.Parse(path);

        result.Hosts.Single(host => host.Alias == "inner").ProxyJump.Should().Be("bastion");
        result.Hosts.Single(host => host.Alias == "direct").ProxyJump.Should().BeNull();
    }

    [TestMethod]
    public void Parse_IncludeWithGlobAndRelativePath_LoadsIncludedHostsInOrder()
    {
        _home.Write(".ssh/conf.d/a.conf", "Host from-a\n    User alice\n");
        _home.Write(".ssh/conf.d/b.conf", "Host from-b\n    User bob\n");
        _home.Write(".ssh/single.conf", "Host single\n");
        var path = _home.Write(".ssh/config", """
            Include conf.d/*.conf
            Include ~/.ssh/single.conf
            Host main
            """);

        var result = _parser.Parse(path);

        result.Hosts.Select(host => host.Alias).Should().Equal("from-a", "from-b", "single", "main");
        result.Hosts.Single(host => host.Alias == "from-b").UserName.Should().Be("bob");
        result.Issues.Should().BeEmpty();
    }

    [TestMethod]
    public void Parse_IncludeLoop_IsReportedAndTerminates()
    {
        _home.Write(".ssh/loop.conf", "Include loop.conf\nHost looped\n");
        var path = _home.Write(".ssh/config", "Include loop.conf\n");

        var result = _parser.Parse(path);

        result.Hosts.Should().ContainSingle(host => host.Alias == "looped");
        result.Issues.Should().ContainSingle(issue => issue.Directive == "Include" && issue.Reason.Contains("loop"));
    }

    [TestMethod]
    public void Parse_MissingLiteralInclude_IsReported()
    {
        var path = _home.Write(".ssh/config", "Include nowhere.conf\nHost main\n");

        var result = _parser.Parse(path);

        result.Hosts.Should().ContainSingle();
        result.Issues.Should().ContainSingle(issue => issue.Directive == "Include" && issue.Line == 1);
    }

    [TestMethod]
    public void Parse_UnsupportedDirective_IsReportedWithLocationAndAffectedAliases()
    {
        var path = _home.Write("config", """
            Host web
                HostName 10.0.0.1
                ForwardAgent yes
                ServerAliveInterval 30
            """);

        var result = _parser.Parse(path);

        result.Hosts.Should().ContainSingle();
        result.Issues.Should().HaveCount(2);
        var forwardAgent = result.Issues.Single(issue => issue.Directive == "ForwardAgent");
        forwardAgent.Line.Should().Be(3);
        forwardAgent.FilePath.Should().Be(path);
        forwardAgent.AffectedAliases.Should().Equal("web");
        result.Issues.Should().Contain(issue => issue.Directive == "ServerAliveInterval" && issue.Line == 4);
    }

    [TestMethod]
    public void Parse_UnsupportedDirectiveInWildcardBlock_ListsEveryAffectedAliasOnce()
    {
        var path = _home.Write("config", """
            Host one two
            Host *
                AddKeysToAgent yes
            """);

        var result = _parser.Parse(path);

        var issue = result.Issues.Should().ContainSingle().Subject;
        issue.Directive.Should().Be("AddKeysToAgent");
        issue.AffectedAliases.Should().BeEquivalentTo("one", "two");
    }

    [TestMethod]
    public void Parse_UnsupportedDirectiveThatAppliesToNoAlias_IsNotReported()
    {
        var path = _home.Write("config", """
            Host web
            Host *.internal
                ForwardAgent yes
            """);

        var result = _parser.Parse(path);

        result.Issues.Should().BeEmpty();
    }

    [TestMethod]
    public void Parse_MatchBlock_IsReportedAndItsSettingsAreIgnored()
    {
        var path = _home.Write("config", """
            Host web
                User normal
            Match host web
                User ignored
            """);

        var result = _parser.Parse(path);

        result.Hosts.Single().UserName.Should().Be("normal");
        result.Issues.Should().ContainSingle(issue => issue.Directive == "Match" && issue.Line == 3);
    }

    [TestMethod]
    public void Parse_InvalidPortAndPercentTokens_AreReportedAndDefaultsUsed()
    {
        var path = _home.Write("config", """
            Host web
                Port 99999
                HostName %h.example.org
            """);

        var result = _parser.Parse(path);

        var host = result.Hosts.Single();
        host.Port.Should().Be(22);
        host.HostName.Should().Be("web");
        result.Issues.Should().Contain(issue => issue.Directive == "Port" && issue.Reason.Contains("99999"));
        result.Issues.Should().Contain(issue => issue.Directive == "HostName" && issue.Reason.Contains("Percent"));
    }

    [TestMethod]
    public void Parse_SecondIdentityFile_IsReportedAndFirstIsUsed()
    {
        var path = _home.Write("config", """
            Host web
                IdentityFile /keys/first
                IdentityFile /keys/second
            """);

        var result = _parser.Parse(path);

        result.Hosts.Single().IdentityFile.Should().Be("/keys/first");
        result.Issues.Should().ContainSingle(issue => issue.Directive == "IdentityFile" && issue.Line == 3);
    }

    [TestMethod]
    public void Parse_CommentsAndBlankLines_AreIgnored()
    {
        var path = _home.Write("config", """
            # leading comment

            Host web   # trailing comment
                # inner comment
                User deploy
            """);

        var result = _parser.Parse(path);

        result.Hosts.Single().UserName.Should().Be("deploy");
        result.Issues.Should().BeEmpty();
    }
}
