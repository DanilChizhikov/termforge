using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DTech.Persistence.Time;

public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(value => ToUtc(value), value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }

    private static DateTime ToUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.ToUniversalTime();
    }
}
