using System.Collections.Concurrent;
using System.Text.Json;

namespace DTech.Entrypoint.Realtime;

public sealed class RealtimeChannel
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<Guid, RealtimeConnection> _connections = new();
    private readonly ILogger<RealtimeChannel> _logger;

    public RealtimeChannel(ILogger<RealtimeChannel> logger)
    {
        _logger = logger;
    }

    public int ConnectionCount => _connections.Count;

    public async Task Publish<TPayload>(string type, TPayload payload, CancellationToken cancellationToken)
    {
        var message = new RealtimeMessage<TPayload>(type, payload);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        foreach (var connection in _connections.Values)
        {
            await SendSafely(connection, bytes, cancellationToken);
        }
    }

    internal void Add(RealtimeConnection connection)
    {
        _connections[connection.Id] = connection;
    }

    internal void Remove(RealtimeConnection connection)
    {
        _connections.TryRemove(connection.Id, out _);
    }

    private async Task SendSafely(RealtimeConnection connection, byte[] bytes, CancellationToken cancellationToken)
    {
        try
        {
            await connection.Send(bytes, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogDebug($"Dropping realtime connection after send failure: {exception.GetType().Name}");
            Remove(connection);
        }
    }
}
