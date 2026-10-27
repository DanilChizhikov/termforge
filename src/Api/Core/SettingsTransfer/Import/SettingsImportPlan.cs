using DTech.Core.SavedConnections;

namespace DTech.Core.SettingsTransfer.Import;

public sealed record SettingsImportPlan(
    IReadOnlyList<ConnectionImportStep> Steps,
    IReadOnlyList<SavedConnection> Removals,
    IReadOnlyDictionary<Guid, Guid> IdMap)
{
    public required IReadOnlyDictionary<Guid, string> FinalConnectionNames { get; init; }

    public Guid? MapConnectionId(Guid importedId)
    {
        var localId = IdMap.TryGetValue(importedId, out var mapped) ? mapped : importedId;
        return FinalConnectionNames.ContainsKey(localId) ? localId : null;
    }
}
