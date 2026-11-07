namespace DTech.Core.AiBridge.Settings;

public interface IAiBridgeTokenRepository
{
    Task<IReadOnlyList<AiBridgeTokenInfo>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListHashesAsync(CancellationToken cancellationToken);

    Task<bool> HasAnyAsync(CancellationToken cancellationToken);

    Task<bool> TryAddAsync(AiBridgeTokenRecord token, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
