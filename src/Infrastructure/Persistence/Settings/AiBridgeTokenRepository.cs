using DTech.Core.AiBridge.Settings;
using DTech.Persistence.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DTech.Persistence.Settings;

public sealed class AiBridgeTokenRepository(TermForgeDbContext context) : IAiBridgeTokenRepository
{
    public async Task<IReadOnlyList<AiBridgeTokenInfo>> ListAsync(CancellationToken cancellationToken)
    {
        return await context.Set<AiBridgeTokenRecord>().AsNoTracking()
            .OrderBy(token => token.CreatedAt).ThenBy(token => token.Id)
            .Select(token => new AiBridgeTokenInfo(token.Id, token.Name, token.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListHashesAsync(CancellationToken cancellationToken)
    {
        return await context.Set<AiBridgeTokenRecord>().AsNoTracking()
            .Select(token => token.TokenHash).ToListAsync(cancellationToken);
    }

    public Task<bool> HasAnyAsync(CancellationToken cancellationToken)
    {
        return context.Set<AiBridgeTokenRecord>().AnyAsync(cancellationToken);
    }

    public async Task<bool> TryAddAsync(AiBridgeTokenRecord token, CancellationToken cancellationToken)
    {
        context.Set<AiBridgeTokenRecord>().Add(token);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            return false;
        }
        finally
        {
            context.Entry(token).State = EntityState.Detached;
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await context.Set<AiBridgeTokenRecord>()
            .Where(token => token.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted != 0;
    }
}
