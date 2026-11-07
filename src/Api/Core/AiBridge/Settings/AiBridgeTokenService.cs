namespace DTech.Core.AiBridge.Settings;

public sealed class AiBridgeTokenService(
    IAiBridgeTokenRepository repository,
    IAiBridgeTokenGenerator generator,
    TimeProvider timeProvider)
{
    public const int MaxNameLength = 100;

    public Task<IReadOnlyList<AiBridgeTokenInfo>> ListAsync(CancellationToken cancellationToken)
    {
        return repository.ListAsync(cancellationToken);
    }

    public Task<bool> HasAnyAsync(CancellationToken cancellationToken)
    {
        return repository.HasAnyAsync(cancellationToken);
    }

    public Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        return repository.DeleteAsync(id, cancellationToken);
    }

    public async Task<AiBridgeTokenCreation> CreateAsync(string? name, CancellationToken cancellationToken)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName) || trimmedName.Length > MaxNameLength)
        {
            return new AiBridgeTokenCreation { Error = AiBridgeTokenCreationError.InvalidName };
        }

        var token = generator.Create();
        var hash = AiBridgeTokenHasher.Hash(token);
        var record = new AiBridgeTokenRecord
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            NormalizedName = trimmedName.ToUpperInvariant(),
            TokenHash = hash,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        if (!await repository.TryAddAsync(record, cancellationToken))
        {
            return new AiBridgeTokenCreation { Error = AiBridgeTokenCreationError.DuplicateName };
        }

        var info = new AiBridgeTokenInfo(record.Id, record.Name, record.CreatedAt);
        return new AiBridgeTokenCreation { Info = info, Token = token };
    }

    public async Task<bool> AcceptsAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var hash = AiBridgeTokenHasher.Hash(token);
        var hashes = await repository.ListHashesAsync(cancellationToken);
        var accepted = false;
        for (var i = 0; i < hashes.Count; i++)
        {
            accepted |= AiBridgeTokenHasher.HashesMatch(hashes[i], hash);
        }

        return accepted;
    }
}
