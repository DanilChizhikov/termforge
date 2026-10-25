using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DTech.Persistence.Time;

public sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(value => ToUtc(value), value => ToUtcKind(value))
    {
    }

    private static DateTime? ToUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
        }

        return value.Value.ToUniversalTime();
    }

    private static DateTime? ToUtcKind(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
    }
}
