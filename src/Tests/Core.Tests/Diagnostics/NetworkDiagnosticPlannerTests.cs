using DTech.Core.NetworkDiagnostics;
using FluentAssertions;

namespace DTech.Core.Tests.Diagnostics;

[TestClass]
public sealed class NetworkDiagnosticPlannerTests
{
    [TestMethod]
    public void Plan_MergesAndSortsPortsAndRanges()
    {
        var request = Request() with { Ports = "443,22,80-82,81" };

        var plan = NetworkDiagnosticPlanner.Create(request);

        plan.Ports.Should().Equal(22, 80, 81, 82, 443);
        plan.FullScan.Should().BeFalse();
    }

    [TestMethod]
    public void Plan_ExplicitFullRange_StillRequiresConfirmation()
    {
        var request = Request() with { Ports = "1-65535" };

        NetworkDiagnosticPlanner.Create(request).FullScan.Should().BeTrue();
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("65536")]
    [DataRow("90-80")]
    [DataRow("22; touch /tmp/injection")]
    public void Plan_InvalidPorts_AreRejected(string ports)
    {
        var action = () => NetworkDiagnosticPlanner.Create(Request() with { Ports = ports });

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Plan_UnknownPointCategory_IsRejected()
    {
        var request = Request() with { Points = [new NetworkDiagnosticPoint(null, "auto")] };
        var action = () => NetworkDiagnosticPlanner.Create(request);

        action.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Plan_UrlCredentials_AreRejected()
    {
        var request = Request() with { Url = "https://user:password@example.com/" };
        var action = () => NetworkDiagnosticPlanner.Create(request);

        action.Should().Throw<ArgumentException>();
    }

    private static NetworkDiagnosticRequest Request()
    {
        return new NetworkDiagnosticRequest { Host = "example.com", Protocols = ["tcp"], Points = [new NetworkDiagnosticPoint(null, "rf")] };
    }
}
