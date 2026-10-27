using System.Text.Json;
using DTech.Core.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DTech.Persistence.Configurations;

internal sealed class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    public const int MaxKindLength = 100;
    public const int MaxServerLength = 200;

    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.ToTable("operations");
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Kind).IsRequired().HasMaxLength(MaxKindLength);
        builder.Property(operation => operation.Server).HasMaxLength(MaxServerLength);
        builder.Property(operation => operation.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(operation => operation.CreatedAt).IsRequired();
        builder.Property(operation => operation.UpdatedAt).IsRequired();
        builder.Property(operation => operation.Error);
        builder.Property(operation => operation.Retryable).IsRequired();
        builder.Property(operation => operation.Details)
            .IsRequired()
            .HasConversion(DetailsJson.Converter, DetailsJson.Comparer);
        builder.HasIndex(operation => operation.CreatedAt);
        builder.HasIndex(operation => operation.Status);
        builder.HasIndex(operation => operation.Kind);
        builder.HasIndex(operation => operation.Server);
    }

    private static class DetailsJson
    {
        public static readonly Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<IReadOnlyDictionary<string, string>, string> Converter =
            new(details => Serialize(details), json => Deserialize(json));

        public static readonly ValueComparer<IReadOnlyDictionary<string, string>> Comparer =
            new((left, right) => Serialize(left) == Serialize(right), details => Serialize(details).GetHashCode(), details => Deserialize(Serialize(details)));

        private static string Serialize(IReadOnlyDictionary<string, string>? details)
        {
            return JsonSerializer.Serialize(details ?? new Dictionary<string, string>());
        }

        private static IReadOnlyDictionary<string, string> Deserialize(string json)
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return parsed ?? new Dictionary<string, string>();
        }
    }
}
