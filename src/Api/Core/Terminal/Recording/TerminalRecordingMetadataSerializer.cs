using System.Text.Json;

namespace DTech.Core.Terminal.Recording;

internal static class TerminalRecordingMetadataSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static byte[] Serialize(TerminalRecordingMetadata metadata)
    {
        var document = new TerminalRecordingMetadataDocument
        {
            Id = metadata.Id.Value,
            ConnectionId = metadata.ConnectionId,
            ConnectionName = metadata.ConnectionName,
            SessionNumber = metadata.SessionNumber,
            Columns = metadata.Size.Columns,
            Rows = metadata.Size.Rows,
            StartedAt = metadata.StartedAt,
            EndedAt = metadata.EndedAt,
            SizeBytes = metadata.SizeBytes,
            EndReason = metadata.EndReason?.ToString(),
        };
        return JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions);
    }

    public static TerminalRecordingMetadata? Deserialize(ReadOnlySpan<byte> json, TerminalRecordingId expectedId)
    {
        TerminalRecordingMetadataDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<TerminalRecordingMetadataDocument>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (document is null || document.Id != expectedId.Value)
        {
            return null;
        }

        var size = new TerminalSize(document.Columns, document.Rows);
        return new TerminalRecordingMetadata
        {
            Id = expectedId,
            ConnectionId = document.ConnectionId,
            ConnectionName = document.ConnectionName ?? string.Empty,
            SessionNumber = document.SessionNumber,
            Size = size,
            StartedAt = DateTime.SpecifyKind(document.StartedAt, DateTimeKind.Utc),
            EndedAt = ToUtc(document.EndedAt),
            SizeBytes = document.SizeBytes,
            EndReason = ParseEndReason(document.EndReason),
        };
    }

    private static DateTime? ToUtc(DateTime? value)
    {
        return value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
    }

    private static TerminalRecordingEndReason? ParseEndReason(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var isKnown = Enum.TryParse<TerminalRecordingEndReason>(text, false, out var reason) && Enum.IsDefined(reason);
        return isKnown ? reason : null;
    }
}
