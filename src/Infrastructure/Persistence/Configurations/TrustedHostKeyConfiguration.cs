using DTech.Persistence.HostKeys;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DTech.Persistence.Configurations;

internal sealed class TrustedHostKeyConfiguration : IEntityTypeConfiguration<TrustedHostKeyRecord>
{
    public const int MaxHostLength = 255;
    public const int MaxAlgorithmLength = 100;
    public const int MaxFingerprintLength = 100;

    public void Configure(EntityTypeBuilder<TrustedHostKeyRecord> builder)
    {
        builder.ToTable("trusted_host_keys");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Host).HasMaxLength(MaxHostLength).IsRequired();
        builder.Property(record => record.Port).IsRequired();
        builder.Property(record => record.Algorithm).HasMaxLength(MaxAlgorithmLength).IsRequired();
        builder.Property(record => record.Fingerprint).HasMaxLength(MaxFingerprintLength).IsRequired();
        builder.Property(record => record.TrustedAt).IsRequired();
        builder.HasIndex(record => new { record.Host, record.Port, record.Algorithm, record.Fingerprint }).IsUnique();
    }
}
