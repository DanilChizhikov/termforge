using System.Net;
using System.Net.Sockets;
using DTech.Core.Connections;

namespace DTech.Ssh.IntegrationTests.Harness;

internal sealed class BlackholeProxy : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly SshEndpoint _target;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _relay;
    private int _dropTraffic;
    private int _disposed;

    public BlackholeProxy(SshEndpoint target)
    {
        _target = target;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var bound = (IPEndPoint)_listener.LocalEndpoint;
        Endpoint = new SshEndpoint(bound.Address.ToString(), bound.Port);
        _relay = RelayAsync();
    }

    public SshEndpoint Endpoint { get; }

    public void DropTraffic()
    {
        Interlocked.Exchange(ref _dropTraffic, 1);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _stopping.CancelAsync();
        _listener.Stop();
        try
        {
            await _relay;
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
        {
        }

        _stopping.Dispose();
    }

    private async Task RelayAsync()
    {
        using var incoming = await _listener.AcceptTcpClientAsync(_stopping.Token);
        using var outgoing = new TcpClient();
        await outgoing.ConnectAsync(_target.Host, _target.Port, _stopping.Token);
        var inbound = incoming.GetStream();
        var outbound = outgoing.GetStream();
        var upload = CopyAsync(inbound, outbound);
        var download = CopyAsync(outbound, inbound);
        try
        {
            await Task.WhenAny(upload, download);
        }
        finally
        {
            await _stopping.CancelAsync();
            await Task.WhenAll(upload, download);
        }
    }

    private async Task CopyAsync(Stream source, Stream destination)
    {
        var buffer = new byte[16 * 1024];
        var length = await source.ReadAsync(buffer, _stopping.Token);
        while (length > 0)
        {
            if (Volatile.Read(ref _dropTraffic) == 0)
            {
                var data = buffer.AsMemory(0, length);
                await destination.WriteAsync(data, _stopping.Token);
            }

            length = await source.ReadAsync(buffer, _stopping.Token);
        }
    }
}
