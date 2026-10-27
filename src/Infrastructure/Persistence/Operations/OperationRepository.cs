using DTech.Core.Operations;
using DTech.Persistence.Database;
using Microsoft.EntityFrameworkCore;

namespace DTech.Persistence.Operations;

public sealed class OperationRepository : IOperationRepository
{
    private readonly TermForgeDbContext _context;

    public OperationRepository(TermForgeDbContext context)
    {
        _context = context;
    }

    public async Task Add(Operation operation, CancellationToken cancellationToken)
    {
        var entry = _context.Operations.Add(operation);
        await _context.SaveChangesAsync(cancellationToken);
        entry.State = EntityState.Detached;
    }

    public async Task Update(Operation operation, CancellationToken cancellationToken)
    {
        var entry = _context.Operations.Update(operation);
        await _context.SaveChangesAsync(cancellationToken);
        entry.State = EntityState.Detached;
    }

    public Task<Operation?> Find(Guid id, CancellationToken cancellationToken)
    {
        return _context.Operations.AsNoTracking().FirstOrDefaultAsync(operation => operation.Id == id, cancellationToken);
    }

    public async Task<OperationPage> Query(OperationQuery query, CancellationToken cancellationToken)
    {
        var filtered = ApplyFilters(_context.Operations.AsNoTracking(), query);
        var total = await filtered.CountAsync(cancellationToken);
        var items = await filtered
            .OrderByDescending(operation => operation.CreatedAt)
            .ThenBy(operation => operation.Id)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
        return new OperationPage(items, total, query.Offset, query.Limit);
    }

    private static IQueryable<Operation> ApplyFilters(IQueryable<Operation> source, OperationQuery query)
    {
        var filtered = source;
        if (!string.IsNullOrWhiteSpace(query.Kind))
        {
            var kind = query.Kind;
            var kindFamilyPrefix = kind + ".";
            filtered = filtered.Where(operation => operation.Kind == kind || operation.Kind.StartsWith(kindFamilyPrefix));
        }

        if (query.Status is not null)
        {
            var status = query.Status.Value;
            filtered = filtered.Where(operation => operation.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Server))
        {
            var server = query.Server;
            filtered = filtered.Where(operation => operation.Server == server);
        }

        if (query.From is not null)
        {
            var from = query.From.Value;
            filtered = filtered.Where(operation => operation.CreatedAt >= from);
        }

        if (query.To is not null)
        {
            var to = query.To.Value;
            filtered = filtered.Where(operation => operation.CreatedAt <= to);
        }

        return filtered;
    }
}
