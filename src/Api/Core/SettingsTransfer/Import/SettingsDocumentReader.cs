using System.Text.Json;
using DTech.Core.SettingsTransfer.Document;

namespace DTech.Core.SettingsTransfer.Import;

public static class SettingsDocumentReader
{
    private const string FormatProperty = "format";
    private const string SchemaVersionProperty = "schemaVersion";

    public static SettingsTransferResult<SettingsDocument> Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Invalid("The settings file is empty.");
        }

        if (text.Length > SettingsTransferLimits.MaxSettingsFileCharacters)
        {
            var tooLarge = SettingsTransferError.TooLarge("settings file", SettingsTransferLimits.MaxSettingsFileCharacters);
            return SettingsTransferResult<SettingsDocument>.Failure(tooLarge);
        }

        var documentOptions = new JsonDocumentOptions { MaxDepth = SettingsDocumentSchema.MaxDepth };
        try
        {
            using var json = JsonDocument.Parse(text, documentOptions);
            return ReadRoot(json.RootElement);
        }
        catch (JsonException)
        {
            return Invalid("The settings file is not valid JSON or does not match the TermForge settings format.");
        }
    }

    private static SettingsTransferResult<SettingsDocument> ReadRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Invalid("The settings file is not a TermForge settings export.");
        }

        var hasFormat = root.TryGetProperty(FormatProperty, out var format);
        if (!hasFormat || format.ValueKind != JsonValueKind.String || format.GetString() != SettingsDocumentSchema.Format)
        {
            return Invalid("The settings file is not a TermForge settings export.");
        }

        var hasVersion = root.TryGetProperty(SchemaVersionProperty, out var versionElement);
        if (!hasVersion || versionElement.ValueKind != JsonValueKind.Number || !versionElement.TryGetInt32(out var version) || version < 1)
        {
            return Invalid("The settings file has no valid schema version.");
        }

        if (version > SettingsDocumentSchema.CurrentVersion)
        {
            var unsupported = new SettingsTransferError(
                SettingsTransferErrorCode.UnsupportedVersion,
                $"The settings file uses schema version {version}, which was created by a newer TermForge. This version reads schema version {SettingsDocumentSchema.CurrentVersion}. Update TermForge to import it.");
            return SettingsTransferResult<SettingsDocument>.Failure(unsupported);
        }

        var document = root.Deserialize<SettingsDocument>(SettingsDocumentSchema.JsonOptions);
        if (document is null)
        {
            return Invalid("The settings file is empty.");
        }

        return SettingsTransferResult<SettingsDocument>.Success(document);
    }

    private static SettingsTransferResult<SettingsDocument> Invalid(string message)
    {
        var error = SettingsTransferError.Invalid(message);
        return SettingsTransferResult<SettingsDocument>.Failure(error);
    }
}
