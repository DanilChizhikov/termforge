using DTech.Core.CommandHistory;
using DTech.Persistence.SavedConnections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DTech.Persistence.CommandHistory;

internal sealed class CommandHistoryConfiguration : IEntityTypeConfiguration<CommandHistoryEntity>
{
    public void Configure(EntityTypeBuilder<CommandHistoryEntity> builder)
    {
        builder.ToTable("command_history");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Command).HasMaxLength(CommandHistoryLimits.MaxCommandLength).IsRequired();
        builder.Property(entry => entry.ExecutedAt).IsRequired();
        builder.Property(entry => entry.IsPinned).IsRequired();
        builder.HasIndex(entry => new { entry.ConnectionId, entry.ExecutedAt });
        builder.HasOne<SavedConnectionEntity>()
            .WithMany()
            .HasForeignKey(entry => entry.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
