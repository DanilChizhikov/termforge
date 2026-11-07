using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

namespace DTech.Core.NetworkDiagnostics;

public sealed class LocalNetworkProbeSession : INetworkProbeSession
{
    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, MaxResponseHeadersLength = 32 });

    public async Task<NetworkProbeOutcome> ProbeAsync(NetworkProbe probe, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(probe.Protocol is "http" or "route" ? 10 : probe.Protocol == "ssh" ? 5 : 3));
        try
        {
            return probe.Protocol switch
            {
                "dns" => await DnsAsync(probe, timeout.Token),
                "http" => await HttpAsync(probe, timeout.Token),
                "route" or "ssh" => await LocalToolProbe.RunAsync(probe, timeout.Token),
                "udp" => await UdpAsync(probe, timeout.Token),
                "tcp" or "tls" => await StreamAsync(probe, timeout.Token),
                _ => new NetworkProbeOutcome(NetworkProbeOutcome.Unavailable, "Unsupported protocol."),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new NetworkProbeOutcome(NetworkProbeOutcome.NoResponse, "No response before the timeout. This does not establish network blocking.");
        }
        catch (SocketException exception)
        {
            var status = exception.SocketErrorCode == SocketError.ConnectionRefused ? NetworkProbeOutcome.Refused
                : exception.SocketErrorCode == SocketError.TimedOut ? NetworkProbeOutcome.NoResponse : NetworkProbeOutcome.Error;
            return new NetworkProbeOutcome(status, $"Socket error: {exception.SocketErrorCode}.");
        }
        catch (AuthenticationException)
        {
            return new NetworkProbeOutcome(NetworkProbeOutcome.Error, "TLS handshake or certificate validation failed.");
        }
        catch (HttpRequestException exception)
        {
            var refused = exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused };
            return new NetworkProbeOutcome(refused ? NetworkProbeOutcome.Refused : NetworkProbeOutcome.Error, $"HTTP request failed: {exception.HttpRequestError}.");
        }
        catch (IOException)
        {
            return new NetworkProbeOutcome(NetworkProbeOutcome.Error, "The peer closed the connection during the check.");
        }
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async Task<NetworkProbeOutcome> DnsAsync(NetworkProbe probe, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(probe.Domain, cancellationToken);
        var text = string.Join(", ", addresses.Select(address => address.ToString()));
        return new NetworkProbeOutcome(NetworkProbeOutcome.Responded, text);
    }

    private async Task<NetworkProbeOutcome> HttpAsync(NetworkProbe probe, CancellationToken cancellationToken)
    {
        var address = probe.Url ?? new UriBuilder(probe.Port == 443 ? "https" : "http", probe.Host, probe.Port!.Value).Uri;
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return new NetworkProbeOutcome(NetworkProbeOutcome.Responded, $"HTTP {(int)response.StatusCode} ({response.StatusCode}); redirects were not followed.");
    }

    private static async Task<NetworkProbeOutcome> UdpAsync(NetworkProbe probe, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(probe.Host, cancellationToken);
        var address = addresses[0];
        using var socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        var endpoint = new IPEndPoint(address, probe.Port!.Value);
        await socket.ConnectAsync(endpoint, cancellationToken);
        ReadOnlyMemory<byte> payload = new byte[] { 0 };
        await socket.SendAsync(payload, SocketFlags.None, cancellationToken);
        var response = new byte[2048];
        await socket.ReceiveAsync(response, SocketFlags.None, cancellationToken);
        return new NetworkProbeOutcome(NetworkProbeOutcome.Responded, "Received a UDP datagram.");
    }

    private static async Task<NetworkProbeOutcome> StreamAsync(NetworkProbe probe, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(probe.Host, probe.Port!.Value, cancellationToken);
        if (probe.Protocol == "tcp") return new NetworkProbeOutcome(NetworkProbeOutcome.Responded, "TCP connection established.");
        await using var stream = client.GetStream();
        if (probe.Protocol == "tls")
        {
            await using var tls = new SslStream(stream, true);
            var options = new SslClientAuthenticationOptions { TargetHost = probe.Domain };
            await tls.AuthenticateAsClientAsync(options, cancellationToken);
            return new NetworkProbeOutcome(NetworkProbeOutcome.Responded, $"TLS negotiated {tls.SslProtocol}; certificate validated.");
        }
        return new NetworkProbeOutcome(NetworkProbeOutcome.Unavailable, "Unsupported stream protocol.");
    }
}
