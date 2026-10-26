using DTech.Core.Reconnect;
using DTech.Core.SavedConnections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DTech.Persistence.SavedConnections;

internal sealed class SavedConnectionConfiguration : IEntityTypeConfiguration<SavedConnectionEntity>
{
    private const string CaseInsensitiveCollation = "NOCASE";
    private const int MaxAuthMethodLength = 20;

    public void Configure(EntityTypeBuilder<SavedConnectionEntity> builder)
    {
        builder.ToTable("saved_connections");
        builder.HasKey(connection => connection.Id);
        builder.Property(connection => connection.Name)
            .HasMaxLength(SavedConnectionValidator.MaxNameLength)
            .UseCollation(CaseInsensitiveCollation)
            .IsRequired();
        builder.HasIndex(connection => connection.Name).IsUnique();
        builder.Property(connection => connection.GroupName).HasMaxLength(SavedConnectionValidator.MaxNameLength);
        builder.Property(connection => connection.Host).HasMaxLength(SavedConnectionValidator.MaxTextLength).IsRequired();
        builder.Property(connection => connection.UserName).HasMaxLength(SavedConnectionValidator.MaxTextLength).IsRequired();
        builder.Property(connection => connection.AuthMethod).HasConversion<string>().HasMaxLength(MaxAuthMethodLength).IsRequired();
        builder.Property(connection => connection.PrivateKeyPath).HasMaxLength(SavedConnectionValidator.MaxTextLength);
        builder.Property(connection => connection.SshAlias).HasMaxLength(SavedConnectionValidator.MaxTextLength);
        builder.Property(connection => connection.ReconnectMode).HasConversion<string>().HasMaxLength(MaxAuthMethodLength).HasDefaultValue(ReconnectPolicy.Default.Mode).ValueGeneratedNever().IsRequired();
        builder.Property(connection => connection.ReconnectMaxAttempts).HasDefaultValue(ReconnectPolicy.Default.MaxAttempts).IsRequired();
        builder.Property(connection => connection.ReconnectInitialDelaySeconds).HasDefaultValue(ReconnectPolicy.Default.InitialDelaySeconds).IsRequired();
        builder.Property(connection => connection.ReconnectMaxDelaySeconds).HasDefaultValue(ReconnectPolicy.Default.MaxDelaySeconds).IsRequired();
        builder.Property(connection => connection.CreatedAt).IsRequired();
        builder.Property(connection => connection.UpdatedAt).IsRequired();
        builder.HasOne<SavedConnectionEntity>()
            .WithMany()
            .HasForeignKey(connection => connection.JumpHostId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
