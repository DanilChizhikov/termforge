using DTech.Core.SavedConnections;

namespace DTech.Core.SettingsTransfer.Import;

internal sealed class ImportPlanningState
{
    public ImportPlanningState(IReadOnlyList<SavedConnection> existing, bool applyConnections)
    {
        ApplyConnections = applyConnections;
        for (var i = 0; i < existing.Count; i++)
        {
            var connection = existing[i];
            ExistingByName[connection.Details.Name] = connection;
            UsedIds.Add(connection.Id);
            TakenNames.Add(connection.Details.Name);
        }
    }

    public bool ApplyConnections { get; }

    public Dictionary<string, SavedConnection> ExistingByName { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<Guid> UsedIds { get; } = [];

    public HashSet<string> TakenNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<Guid, Guid> IdMap { get; } = [];

    public Dictionary<Guid, SavedConnection> Matches { get; } = [];

    public Guid AllocateId()
    {
        while (true)
        {
            var id = Guid.NewGuid();
            if (UsedIds.Add(id))
            {
                return id;
            }
        }
    }
}
