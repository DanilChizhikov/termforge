using DTech.Core.AiBridge.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DTech.Persistence.Settings;

internal sealed class AiBridgeTokenConfiguration : IEntityTypeConfiguration<AiBridgeTokenRecord>
{
    public void Configure(EntityTypeBuilder<AiBridgeTokenRecord> builder)
    {
        builder.ToTable("ai_bridge_tokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Name).HasMaxLength(AiBridgeTokenService.MaxNameLength).IsRequired();
        builder.Property(token => token.NormalizedName).IsRequired();
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(token => token.NormalizedName).IsUnique();
    }
}
