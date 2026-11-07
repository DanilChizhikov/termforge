using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DTech.Core.Diagnostics;
using DTech.Core.NetworkDiagnostics;
using FluentAssertions;

namespace DTech.Core.Tests.Diagnostics;

[TestClass]
public sealed class NetworkProbeTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Tcp_OpenAndClosedPorts_AreDistinguished()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await using var session = new LocalNetworkProbeSession();
        var probe = Probe("tcp", port);

        var open = await session.ProbeAsync(probe, TestContext.CancellationToken);
        listener.Stop();
        var closed = await session.ProbeAsync(probe, TestContext.CancellationToken);

        open.Status.Should().Be(NetworkProbeOutcome.Responded);
        closed.Status.Should().Be(NetworkProbeOutcome.Refused);
    }

    [TestMethod]
    public async Task Udp_Reply_IsReportedWithoutAssumingPortStateFromSilence()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var echo = EchoAsync(server, TestContext.CancellationToken);
        await using var session = new LocalNetworkProbeSession();
        var probe = Probe("udp", port);

        var outcome = await session.ProbeAsync(probe, TestContext.CancellationToken);

        await echo;
        outcome.Status.Should().Be(NetworkProbeOutcome.Responded);
    }

    [TestMethod]
    public async Task Udp_SilentPeer_IsNoResponse()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        await using var session = new LocalNetworkProbeSession();
        var probe = Probe("udp", port);

        var outcome = await session.ProbeAsync(probe, TestContext.CancellationToken);

        outcome.Status.Should().Be(NetworkProbeOutcome.NoResponse);
        outcome.Detail.Should().Contain("does not establish");
    }

    [TestMethod]
    public async Task Cancellation_StopsAWaitingProbe()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await using var session = new LocalNetworkProbeSession();
        var probe = Probe("udp", port);
        var action = () => session.ProbeAsync(probe, stop.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task Http_Redirect_IsReturnedWithoutFollowingIt()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serving = RedirectAsync(listener, TestContext.CancellationToken);
        await using var session = new LocalNetworkProbeSession();
        var probe = Probe("http", port);

        var result = await session.ProbeAsync(probe, TestContext.CancellationToken);

        await serving;
        result.Status.Should().Be(NetworkProbeOutcome.Responded);
        result.Detail.Should().Contain("302");
    }

    [TestMethod]
    [DataRow("apt-get", "dnsutils", "netcat-openbsd")]
    [DataRow("dnf", "bind-utils", "nmap-ncat")]
    [DataRow("apk", "bind-tools", "netcat-openbsd")]
    [DataRow("pacman", "bind", "openbsd-netcat")]
    [DataRow("zypper", "bind-utils", "netcat-openbsd")]
    public void Packages_AreAllowlistedAndDoNotUpgradeTheSystem(string manager, string dnsPackage, string netcatPackage)
    {
        var plan = NetworkPackageInstaller.Create(manager, ["dns", "tcp"]);

        plan.Packages.Should().Contain(dnsPackage).And.Contain(netcatPackage);
        plan.Command.Should().NotContain(" dist-upgrade").And.NotContain(" full-upgrade");
        plan.Command.Should().NotContain("-Syu");
    }

    [TestMethod]
    public void RemoteUdp_SilenceIsInconclusiveEvenWhenToolExitsSuccessfully()
    {
        var probe = Probe("udp", 53);
        var result = new RemoteCommandResult(0, "", "connection succeeded");

        var outcome = RemoteNetworkCommands.Parse(probe, result);

        outcome.Status.Should().Be(NetworkProbeOutcome.NoResponse);
    }

    [TestMethod]
    public void RemoteHttp_CertificateFailureIsAnError()
    {
        var probe = Probe("http", 443);
        var result = new RemoteCommandResult(60, "000", "SSL certificate problem");

        RemoteNetworkCommands.Parse(probe, result).Status.Should().Be(NetworkProbeOutcome.Error);
    }

    [TestMethod]
    public void Commands_QuoteDomainsAsData()
    {
        var probe = Probe("dns", 53) with { Domain = "a'$(touch /tmp/should-not-execute)" };

        var command = RemoteNetworkCommands.Create(probe);

        command.Should().Contain("'a'\"'\"'$(touch /tmp/should-not-execute)'");
    }

    private static NetworkProbe Probe(string protocol, int port)
    {
        return new NetworkProbe { Host = "127.0.0.1", Domain = "localhost", Protocol = protocol, Port = port };
    }

    private static async Task EchoAsync(UdpClient server, CancellationToken cancellationToken)
    {
        var received = await server.ReceiveAsync(cancellationToken);
        await server.SendAsync(received.Buffer, received.RemoteEndPoint, cancellationToken);
    }

    private static async Task RedirectAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        var request = new byte[4096];
        var received = await stream.ReadAsync(request, cancellationToken);
        if (received == 0) throw new IOException("The HTTP client sent no request.");
        var response = Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: /final\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, cancellationToken);
    }

    [TestMethod]
    public async Task Tls_UntrustedCertificate_IsRejected()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var key = RSA.Create(2048);
        var certificateRequest = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        var serving = TlsServerAsync(listener, certificate, TestContext.CancellationToken);
        await using var session = new LocalNetworkProbeSession();
        var probe = Probe("tls", port);

        var outcome = await session.ProbeAsync(probe, TestContext.CancellationToken);

        await serving;
        outcome.Status.Should().Be(NetworkProbeOutcome.Error);
        outcome.Detail.Should().Contain("certificate");
    }

    private static async Task TlsServerAsync(TcpListener listener, X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        await using var tls = new SslStream(stream);
        var options = new SslServerAuthenticationOptions { ServerCertificate = certificate };
        try { await tls.AuthenticateAsServerAsync(options, cancellationToken); }
        catch (Exception exception) when (exception is AuthenticationException or IOException) { }
    }
}
