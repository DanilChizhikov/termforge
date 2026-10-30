using DTech.Core.AiBridge.Settings;
using DTech.Core.SavedConnections;

namespace DTech.Core.AiBridge;

public sealed class AiServerAccessPolicy(AiBridgeSettingsService settings, ISavedConnectionRepository repository)
{
    public async Task<AiBridgeResult<IReadOnlyList<SavedConnection>>> ListAccessibleAsync(CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        if (!current.Enabled)
        {
            var disabled = AiBridgeError.BridgeDisabled();
            return AiBridgeResult<IReadOnlyList<SavedConnection>>.Failure(disabled);
        }

        var all = await repository.ListAsync(cancellationToken);
        var accessible = new List<SavedConnection>();
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Details.AiAccess)
            {
                accessible.Add(all[i]);
            }
        }

        return AiBridgeResult<IReadOnlyList<SavedConnection>>.Success(accessible);
    }

    public async Task<AiBridgeResult<SavedConnection>> CheckAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        if (!current.Enabled)
        {
            var disabled = AiBridgeError.BridgeDisabled();
            return AiBridgeResult<SavedConnection>.Failure(disabled);
        }

        var connection = await repository.FindAsync(connectionId, cancellationToken);
        if (connection is null)
        {
            var missing = AiBridgeError.ServerNotFound(connectionId);
            return AiBridgeResult<SavedConnection>.Failure(missing);
        }

        if (!connection.Details.AiAccess)
        {
            var denied = AiBridgeError.AiAccessDisabled(connection.Details.Name);
            return AiBridgeResult<SavedConnection>.Failure(denied);
        }

        return AiBridgeResult<SavedConnection>.Success(connection);
    }

    public async Task<AiBridgeResult<SavedConnection>> CheckWritableAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var checkedConnection = await CheckAsync(connectionId, cancellationToken);
        if (!checkedConnection.IsSuccess || !checkedConnection.Value.Details.ReadOnly)
        {
            return checkedConnection;
        }

        var readOnly = AiBridgeError.ReadOnlyServer(checkedConnection.Value.Details.Name);
        return AiBridgeResult<SavedConnection>.Failure(readOnly);
    }
}
