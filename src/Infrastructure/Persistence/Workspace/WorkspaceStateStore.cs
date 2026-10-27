using DTech.Core.Workspace;
using DTech.Persistence.Database;
using Microsoft.EntityFrameworkCore;

namespace DTech.Persistence.Workspace;

public sealed class WorkspaceStateStore(TermForgeDbContext context, TimeProvider timeProvider) : IWorkspaceStateStore
{
    public const string DefaultStateId = "default";

    public async Task<StoredWorkspaceDocument?> LoadDocumentAsync(CancellationToken cancellationToken)
    {
        var entity = await context.Set<WorkspaceStateEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(state => state.Id == DefaultStateId, cancellationToken);
        return entity is null ? null : new StoredWorkspaceDocument(entity.SchemaVersion, entity.Document);
    }

    public async Task<IReadOnlyList<WorkspaceEditorDraft>> LoadDraftsAsync(CancellationToken cancellationToken)
    {
        var entities = await context.Set<WorkspaceEditorDraftEntity>()
            .AsNoTracking()
            .OrderBy(draft => draft.UpdatedAt)
            .ToListAsync(cancellationToken);
        var drafts = new List<WorkspaceEditorDraft>(entities.Count);
        for (var i = 0; i < entities.Count; i++)
        {
            var draft = WorkspaceEditorDraftMapper.ToModel(entities[i]);
            drafts.Add(draft);
        }

        return drafts;
    }

    public async Task SaveAsync(StoredWorkspaceDocument document, IReadOnlyList<WorkspaceEditorDraft> drafts, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Set<WorkspaceEditorDraftEntity>().ExecuteDeleteAsync(cancellationToken);
        await context.Set<WorkspaceStateEntity>()
            .Where(state => state.Id == DefaultStateId)
            .ExecuteDeleteAsync(cancellationToken);
        var stateEntity = new WorkspaceStateEntity
        {
            Id = DefaultStateId,
            SchemaVersion = document.SchemaVersion,
            Document = document.Json,
            UpdatedAt = now,
        };
        context.Set<WorkspaceStateEntity>().Add(stateEntity);
        var draftEntities = new List<WorkspaceEditorDraftEntity>(drafts.Count);
        for (var i = 0; i < drafts.Count; i++)
        {
            var draftEntity = WorkspaceEditorDraftMapper.ToEntity(drafts[i], now);
            draftEntities.Add(draftEntity);
        }

        context.Set<WorkspaceEditorDraftEntity>().AddRange(draftEntities);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        context.ChangeTracker.Clear();
    }

    public async Task DeleteDocumentAsync(CancellationToken cancellationToken)
    {
        await context.Set<WorkspaceStateEntity>()
            .Where(state => state.Id == DefaultStateId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
