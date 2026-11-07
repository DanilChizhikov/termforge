using DTech.Core.Files;

namespace DTech.Core.Transfers.Execution;

public sealed class TransferConnections : IAsyncDisposable
{
    private readonly IRemoteFileClient _source;
    private readonly IRemoteFileClient? _target;

    private TransferConnections(IRemoteFileClient source, IRemoteFileClient? target)
    {
        _source = source;
        _target = target;
    }

    public TransferContext Context(TransferRequest request)
    {
        return new TransferContext(_source, request) { TargetClient = _target };
    }

    public static async Task<FileResult<TransferConnections>> OpenAsync(ITransferConnectionOpener opener, TransferRequest request, CancellationToken cancellationToken)
    {
        var source = await opener.OpenAsync(request.ConnectionId, cancellationToken);
        if (!source.IsSuccess)
        {
            return FileResult<TransferConnections>.Failure(source.Error);
        }

        var transferred = false;
        try
        {
            IRemoteFileClient? target = null;
            if (request.Direction == TransferDirection.RemoteToRemote)
            {
                var opened = await opener.OpenAsync(request.DestinationConnectionId, cancellationToken);
                if (!opened.IsSuccess)
                {
                    return FileResult<TransferConnections>.Failure(opened.Error);
                }

                target = opened.Value;
            }

            var connections = new TransferConnections(source.Value, target);
            transferred = true;
            return FileResult<TransferConnections>.Success(connections);
        }
        finally
        {
            if (!transferred)
            {
                await source.Value.DisposeAsync();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_target is not null) await _target.DisposeAsync();
        }
        finally
        {
            await _source.DisposeAsync();
        }
    }
}
