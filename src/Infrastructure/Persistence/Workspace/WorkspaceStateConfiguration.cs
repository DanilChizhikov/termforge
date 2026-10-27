using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DTech.Persistence.Workspace;

internal sealed class WorkspaceStateConfiguration : IEntityTypeConfiguration<WorkspaceStateEntity>
{
    private const int MaxIdLength = 64;

    public void Configure(EntityTypeBuilder<WorkspaceStateEntity> builder)
    {
        builder.ToTable("workspace_states");
        builder.HasKey(state => state.Id);
        builder.Property(state => state.Id).HasMaxLength(MaxIdLength);
        builder.Property(state => state.SchemaVersion).IsRequired();
        builder.Property(state => state.Document).IsRequired();
        builder.Property(state => state.UpdatedAt).IsRequired();
    }
}
