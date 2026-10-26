using DTech.Core.Files;
using DTech.Core.Files.Remote;
using DTech.Core.Transfers;

namespace DTech.Entrypoint.Transfers;

internal sealed class ScopedTransferConnectionOpener(IServiceScopeFactory scopeFactory) : ITransferConnectionOpener
{
    public async Task<FileResult<IRemoteFileClient>> OpenAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var opener = scope.ServiceProvider.GetRequiredService<IRemoteConnectionOpener>();
        return await opener.OpenAsync(connectionId, cancellationToken);
    }
}
