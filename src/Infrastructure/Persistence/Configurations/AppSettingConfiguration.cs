using DTech.Persistence.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DTech.Persistence.Configurations;

internal sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public const int MaxKeyLength = 200;

    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("app_settings");
        builder.HasKey(setting => setting.Key);
        builder.Property(setting => setting.Key).HasMaxLength(MaxKeyLength);
        builder.Property(setting => setting.Value).IsRequired();
        builder.Property(setting => setting.UpdatedAt).IsRequired();
    }
}
