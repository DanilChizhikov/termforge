namespace DTech.Core.Safety;

public interface IServerAccessModeReader
{
    Task<bool> IsReadOnlyAsync(Guid connectionId, CancellationToken cancellationToken);
}
