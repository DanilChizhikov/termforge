using DTech.Core.Connections;
using DTech.HostKeys.KnownHosts;
using DTech.HostKeys.Tests.Support;
using FluentAssertions;

namespace DTech.HostKeys.Tests.KnownHosts;

[TestClass]
public sealed class KnownHostsHostMatcherTests
{
    private static readonly SshEndpoint DefaultPortEndpoint = new("example.com", 22);
    private static readonly SshEndpoint CustomPortEndpoint = new("example.com", 2222);

    [TestMethod]
    [DataRow("example.com", true)]
    [DataRow("EXAMPLE.com", true)]
    [DataRow("other.com", false)]
    [DataRow("other.com,example.com", true)]
    [DataRow("*.com", true)]
    [DataRow("exampl?.com", true)]
    [DataRow("*", true)]
    [DataRow("*.org", false)]
    [DataRow("*.com,!example.com", false)]
    [DataRow("!example.com,*", false)]
    [DataRow("[example.com]:22", false)]
    public void Matches_DefaultPort_UsesPlainHostName(string patterns, bool expected)
    {
        KnownHostsHostMatcher.Matches(patterns, DefaultPortEndpoint).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("[example.com]:2222", true)]
    [DataRow("example.com", false)]
    [DataRow("[example.com]:22", false)]
    [DataRow("[*.com]:2222", true)]
    [DataRow("[example.com]:*", true)]
    public void Matches_CustomPort_RequiresBracketedForm(string patterns, bool expected)
    {
        KnownHostsHostMatcher.Matches(patterns, CustomPortEndpoint).Should().Be(expected);
    }

    [TestMethod]
    public void Matches_HashedHostForDefaultPort_MatchesOnlyThatHost()
    {
        var hashed = HashedHostName.Create("example.com");

        KnownHostsHostMatcher.Matches(hashed, DefaultPortEndpoint).Should().BeTrue();
        KnownHostsHostMatcher.Matches(hashed, CustomPortEndpoint).Should().BeFalse();
        KnownHostsHostMatcher.Matches(hashed, new SshEndpoint("other.com", 22)).Should().BeFalse();
    }

    [TestMethod]
    public void Matches_HashedHostWithPort_MatchesBracketedLookupName()
    {
        var hashed = HashedHostName.Create("[example.com]:2222");

        KnownHostsHostMatcher.Matches(hashed, CustomPortEndpoint).Should().BeTrue();
        KnownHostsHostMatcher.Matches(hashed, DefaultPortEndpoint).Should().BeFalse();
    }

    [TestMethod]
    public void Matches_HashedHost_IsCaseInsensitiveForRequestedHost()
    {
        var hashed = HashedHostName.Create("example.com");
        var upperCaseEndpoint = new SshEndpoint("Example.COM", 22);

        KnownHostsHostMatcher.Matches(hashed, upperCaseEndpoint).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("|1|bad|data")]
    [DataRow("|1|not*base64|not*base64")]
    [DataRow("|1|")]
    public void Matches_MalformedHashedHost_ReturnsFalse(string hashed)
    {
        KnownHostsHostMatcher.Matches(hashed, DefaultPortEndpoint).Should().BeFalse();
    }
}
