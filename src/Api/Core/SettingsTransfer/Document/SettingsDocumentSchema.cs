using System.Text.Json;
using System.Text.Json.Serialization;

namespace DTech.Core.SettingsTransfer.Document;

public static class SettingsDocumentSchema
{
    public const string Format = "termforge-settings";
    public const int CurrentVersion = 1;
    public const int MaxDepth = 32;

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            MaxDepth = MaxDepth,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.MakeReadOnly(true);
        return options;
    }
}
