using DTech.Core.SettingsTransfer.Import;
using DTech.Persistence.Database;
using Microsoft.EntityFrameworkCore;

namespace DTech.Persistence.SettingsTransfer;

public sealed class SettingsImportTransaction(TermForgeDbContext context) : ISettingsImportTransaction
{
    public async Task<SettingsImportResult> RunAsync(Func<CancellationToken, Task<SettingsImportResult>> apply, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await apply(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }
    }
}
