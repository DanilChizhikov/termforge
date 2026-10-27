using System.Text.Json;
using DTech.Core.Reconnect;
using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Document;
using DTech.Core.SettingsTransfer.Import;

namespace DTech.Core.Tests.SettingsTransfer;

internal static class SettingsSamples
{
    public static readonly SettingsSections AllSections = new(true, true, true, true);

    public static ExportedConnection Connection(string name, Guid? id = null, Guid? jumpHostId = null)
    {
        var reconnect = new ExportedReconnectPolicy { Mode = "Manual", MaxAttempts = 5, InitialDelaySeconds = 2, MaxDelaySeconds = 60 };
        return new ExportedConnection
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Host = $"{name}.example.test",
            Port = 22,
            UserName = "deploy",
            AuthMethod = "Agent",
            JumpHostId = jumpHostId,
            Reconnect = reconnect,
        };
    }

    public static SavedConnection Saved(string name, Guid? id = null)
    {
        var draft = new SavedConnectionDraft
        {
            Name = name,
            Host = $"{name}.example.test",
            Port = 22,
            UserName = "deploy",
            AuthMethod = ConnectionAuthMethod.Agent,
            ReconnectPolicy = ReconnectPolicy.Default,
        };
        return new SavedConnection { Id = id ?? Guid.NewGuid(), Details = draft };
    }

    public static SettingsDocument Document(params ExportedConnection[] connections)
    {
        return new SettingsDocument
        {
            Format = SettingsDocumentSchema.Format,
            SchemaVersion = SettingsDocumentSchema.CurrentVersion,
            ExportId = Guid.NewGuid(),
            ExportedAt = new DateTime(2026, 10, 27, 10, 0, 0, DateTimeKind.Utc),
            Connections = connections,
        };
    }

    public static SettingsImportOptions Options(ImportMode mode, Dictionary<Guid, ConnectionConflictResolution>? resolutions = null)
    {
        return new SettingsImportOptions(mode, AllSections, resolutions ?? []);
    }

    public static string Serialize(SettingsDocument document)
    {
        return JsonSerializer.Serialize(document, SettingsDocumentSchema.JsonOptions);
    }
}
