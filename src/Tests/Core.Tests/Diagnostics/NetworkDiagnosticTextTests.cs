using DTech.Core.Diagnostics;
using DTech.Core.NetworkDiagnostics;
using FluentAssertions;

namespace DTech.Core.Tests.Diagnostics;

[TestClass]
public sealed class NetworkDiagnosticTextTests
{
    [TestMethod]
    public void FailedTlsCheck_PreservesCertificateLinesAndRemovesControls()
    {
        var probe = new NetworkProbe { Host = "example.com", Domain = "example.com", Protocol = "tls", Port = 443 };
        var result = new RemoteCommandResult(1, "CONNECTED\r\nCertificate chain\r0 s:CN = example.com\n", "\0\aCertificate validation failed\t");

        var outcome = RemoteNetworkCommands.Parse(probe, result);

        outcome.Status.Should().Be(NetworkProbeOutcome.Error);
        outcome.Detail.Should().Be("CONNECTED\nCertificate chain\n0 s:CN = example.com\n\nCertificate validation failed");
    }

    [TestMethod]
    [DataRow("tls", 1, 1000)]
    [DataRow("dns", 0, 2000)]
    public void LongOutput_StaysWithinExistingLimitAndMarksTruncation(string protocol, int exitCode, int limit)
    {
        var probe = new NetworkProbe { Host = "example.com", Domain = "example.com", Protocol = protocol, Port = 443 };
        var output = new string('x', limit + 100);
        var result = new RemoteCommandResult(exitCode, output, "");

        var outcome = RemoteNetworkCommands.Parse(probe, result);

        outcome.Detail.Length.Should().Be(limit);
        outcome.Detail.Should().EndWith("\n[Output truncated]");
    }

    [TestMethod]
    public void RouteOutput_KeepsLineBreaksWhenHopsDoNotRespond()
    {
        var probe = new NetworkProbe { Host = "example.com", Domain = "example.com", Protocol = "route" };
        var result = new RemoteCommandResult(0, "1 gateway\n2 * * *\n3 destination", "");

        var outcome = RemoteNetworkCommands.Parse(probe, result);

        outcome.Status.Should().Be(NetworkProbeOutcome.NoResponse);
        outcome.Detail.Should().Be("1 gateway\n2 * * *\n3 destination");
    }

    [TestMethod]
    public void EmptyErrorOutput_IsHandledWithoutAddingText()
    {
        var probe = new NetworkProbe { Host = "example.com", Domain = "example.com", Protocol = "tls", Port = 443 };
        var result = new RemoteCommandResult(1, "", "");

        var outcome = RemoteNetworkCommands.Parse(probe, result);

        outcome.Status.Should().Be(NetworkProbeOutcome.Error);
        outcome.Detail.Should().BeEmpty();
    }
}
