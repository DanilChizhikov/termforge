using DTech.Core.SavedConnections;
using DTech.Core.Safety;

namespace DTech.Entrypoint.Safety;

internal sealed class ScopedServerAccessModeReader(IServiceScopeFactory scopeFactory) : IServerAccessModeReader
{
    public async Task<bool> IsReadOnlyAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISavedConnectionRepository>();
        var connection = await repository.FindAsync(connectionId, cancellationToken);
        return connection?.Details.ReadOnly ?? false;
    }
}
