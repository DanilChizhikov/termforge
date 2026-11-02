using System.Collections.Concurrent;
using System.Text.Json;

namespace DTech.Entrypoint.Realtime;

public sealed class RealtimeChannel
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<Guid, RealtimeConnection> _connections = new();
    private readonly Lock _publishing = new();
    private readonly ILogger<RealtimeChannel> _logger;
    private readonly TimeProvider _timeProvider;

    public RealtimeChannel(ILogger<RealtimeChannel> logger, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public int ConnectionCount => _connections.Count;

    public Task Publish<TPayload>(string type, TPayload payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var message = new RealtimeMessage<TPayload>(type, payload);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        lock (_publishing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var connection in _connections)
            {
                connection.Value.TryEnqueue(bytes);
            }
        }

        return Task.CompletedTask;
    }

    internal RealtimeConnection Connect(System.Net.WebSockets.WebSocket webSocket, CancellationToken cancellationToken)
    {
        var connection = new RealtimeConnection(webSocket, Remove, _timeProvider);
        _connections[connection.Id] = connection;
        connection.Start(cancellationToken);
        return connection;
    }

    private void Remove(RealtimeConnection connection)
    {
        if (_connections.TryRemove(connection.Id, out _) && connection.Failure is not null)
        {
            _logger.LogDebug($"Dropping realtime connection: {connection.Failure}");
        }
    }
}
