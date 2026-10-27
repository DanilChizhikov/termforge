using DTech.Core.Workspace;
using DTech.Persistence.SavedConnections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DTech.Persistence.Workspace;

internal sealed class WorkspaceEditorDraftConfiguration : IEntityTypeConfiguration<WorkspaceEditorDraftEntity>
{
    private const int MaxSha256Length = 128;

    public void Configure(EntityTypeBuilder<WorkspaceEditorDraftEntity> builder)
    {
        builder.ToTable("workspace_editor_drafts");
        builder.HasKey(draft => new { draft.ConnectionId, draft.Path });
        builder.Property(draft => draft.Path).HasMaxLength(WorkspaceStateLimits.MaxPathLength);
        builder.Property(draft => draft.Content).IsRequired();
        builder.Property(draft => draft.BaseSha256).HasMaxLength(MaxSha256Length);
        builder.Property(draft => draft.UpdatedAt).IsRequired();
        builder.HasOne<SavedConnectionEntity>()
            .WithMany()
            .HasForeignKey(draft => draft.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
