using DTech.Core.Diagnostics;
using DTech.Core.NetworkDiagnostics;
using DTech.Core.SavedConnections;

namespace DTech.Entrypoint.NetworkDiagnostics;

public sealed class NetworkProbeSessionFactory(ISavedConnectionRepository repository, ConnectionTargetFactory targets, IRemoteCommandRunnerFactory runners)
{
    public async Task<NetworkProbeConnection> OpenAsync(NetworkDiagnosticPoint point, CancellationToken cancellationToken)
    {
        if (point.ConnectionId is not { } id) return new NetworkProbeConnection("Local machine", new LocalNetworkProbeSession());
        var connections = await repository.ListAsync(cancellationToken);
        var connection = connections.FirstOrDefault(item => item.Id == id) ?? throw new IOException("The saved point no longer exists.");
        var target = await targets.CreateAsync(connection, connections, cancellationToken);
        if (!target.IsSuccess) throw new IOException(target.Error.Message);
        var opened = await runners.ConnectAsync(target.Value, cancellationToken);
        if (!opened.IsSuccess) throw new IOException(opened.Error!.Message);
        var session = new RemoteNetworkProbeSession(opened.Value);
        try
        {
            await session.InitializeAsync(cancellationToken);
            return new NetworkProbeConnection(connection.Details.Name, session);
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }
}

public sealed record NetworkProbeConnection(string Name, INetworkProbeSession Session);
