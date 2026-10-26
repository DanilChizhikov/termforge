using System.Text.Json;

namespace DTech.Entrypoint.Terminals;

internal sealed record TerminalControlMessage(string? Type, int Columns, int Rows)
{
    public const string ResizeType = "resize";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public bool IsResize => string.Equals(Type, ResizeType, StringComparison.Ordinal);

    public static TerminalControlMessage? Parse(ReadOnlySpan<byte> json)
    {
        try
        {
            return JsonSerializer.Deserialize<TerminalControlMessage>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
