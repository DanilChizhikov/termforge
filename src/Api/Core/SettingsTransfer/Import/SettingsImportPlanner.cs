using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer.Document;

namespace DTech.Core.SettingsTransfer.Import;

public static class SettingsImportPlanner
{
    public static SettingsTransferResult<SettingsImportPlan> Plan(SettingsDocument document, IReadOnlyList<SavedConnection> existing, SettingsImportOptions options)
    {
        var imported = document.Connections ?? [];
        var applyConnections = options.Sections.Connections && document.Connections is not null;
        var state = new ImportPlanningState(existing, applyConnections);
        for (var i = 0; i < imported.Count; i++)
        {
            state.TakenNames.Add(imported[i].Name!.Trim());
        }

        for (var i = 0; i < imported.Count; i++)
        {
            AssignLocalId(state, imported[i], options);
        }

        var steps = new List<ConnectionImportStep>(imported.Count);
        for (var i = 0; i < imported.Count; i++)
        {
            var step = BuildStep(state, imported[i], options);
            steps.Add(step);
        }

        var removals = applyConnections && options.Mode == ImportMode.Replace ? FindRemovals(existing, state) : [];
        var finalConnections = BuildFinalConnections(existing, steps, removals);
        var invalid = ValidateFinal(steps, finalConnections);
        if (invalid is not null)
        {
            return SettingsTransferResult<SettingsImportPlan>.Failure(invalid);
        }

        var finalNames = new Dictionary<Guid, string>(finalConnections.Count);
        for (var i = 0; i < finalConnections.Count; i++)
        {
            finalNames[finalConnections[i].Id] = finalConnections[i].Details.Name;
        }

        var plan = new SettingsImportPlan(steps, removals, state.IdMap) { FinalConnectionNames = finalNames };
        return SettingsTransferResult<SettingsImportPlan>.Success(plan);
    }

    private static void AssignLocalId(ImportPlanningState state, ExportedConnection connection, SettingsImportOptions options)
    {
        var name = connection.Name!.Trim();
        Guid localId;
        if (state.ExistingByName.TryGetValue(name, out var match))
        {
            state.Matches[connection.Id] = match;
            var keepBoth = state.ApplyConnections && options.Mode == ImportMode.Merge && options.ResolutionFor(connection.Id) == ConnectionConflictResolution.KeepBoth;
            localId = keepBoth ? state.AllocateId() : match.Id;
        }
        else
        {
            localId = state.ApplyConnections && state.UsedIds.Contains(connection.Id) ? state.AllocateId() : connection.Id;
            state.UsedIds.Add(localId);
        }

        state.IdMap[connection.Id] = localId;
    }

    private static ConnectionImportStep BuildStep(ImportPlanningState state, ExportedConnection connection, SettingsImportOptions options)
    {
        var parsed = ExportedConnectionMapper.TryToDraft(connection)!;
        Guid? jumpHostId = parsed.JumpHostId is { } importedJumpHostId ? state.IdMap[importedJumpHostId] : null;
        var draft = parsed with { JumpHostId = jumpHostId };
        var localId = state.IdMap[connection.Id];
        var step = new ConnectionImportStep { Imported = connection, Action = ConnectionImportAction.Skipped, LocalId = localId, Draft = draft };
        if (!state.ApplyConnections)
        {
            return step;
        }

        if (!state.Matches.TryGetValue(connection.Id, out var match))
        {
            return step with { Action = ConnectionImportAction.Add };
        }

        var changes = ConnectionChangeDetector.Detect(match.Details, draft);
        var matched = step with { Existing = match, Changes = changes };
        var resolution = options.ResolutionFor(connection.Id);
        if (options.Mode == ImportMode.Merge && resolution == ConnectionConflictResolution.KeepBoth)
        {
            var copyName = ImportedNameAllocator.Allocate(draft.Name, state.TakenNames);
            var copy = draft with { Name = copyName };
            return matched with { Action = ConnectionImportAction.Conflict, Resolution = ConnectionConflictResolution.KeepBoth, Draft = copy };
        }

        if (changes.Count == 0)
        {
            return matched with { Action = ConnectionImportAction.Unchanged };
        }

        if (options.Mode == ImportMode.Replace)
        {
            return matched with { Action = ConnectionImportAction.Update };
        }

        return matched with { Action = ConnectionImportAction.Conflict, Resolution = resolution ?? ConnectionConflictResolution.KeepExisting };
    }

    private static List<SavedConnection> FindRemovals(IReadOnlyList<SavedConnection> existing, ImportPlanningState state)
    {
        var matchedIds = new HashSet<Guid>();
        foreach (var match in state.Matches.Values)
        {
            matchedIds.Add(match.Id);
        }

        var removals = new List<SavedConnection>();
        for (var i = 0; i < existing.Count; i++)
        {
            if (!matchedIds.Contains(existing[i].Id))
            {
                removals.Add(existing[i]);
            }
        }

        return removals;
    }

    private static List<SavedConnection> BuildFinalConnections(IReadOnlyList<SavedConnection> existing, List<ConnectionImportStep> steps, IReadOnlyList<SavedConnection> removals)
    {
        var removedIds = new HashSet<Guid>();
        for (var i = 0; i < removals.Count; i++)
        {
            removedIds.Add(removals[i].Id);
        }

        var final = new Dictionary<Guid, SavedConnection>();
        for (var i = 0; i < existing.Count; i++)
        {
            if (!removedIds.Contains(existing[i].Id))
            {
                final[existing[i].Id] = existing[i];
            }
        }

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.WritesConnection)
            {
                final[step.LocalId] = new SavedConnection { Id = step.LocalId, Details = step.Draft };
            }
        }

        return final.Values.ToList();
    }

    private static SettingsTransferError? ValidateFinal(List<ConnectionImportStep> steps, List<SavedConnection> finalConnections)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < finalConnections.Count; i++)
        {
            var name = finalConnections[i].Details.Name;
            if (!names.Add(name))
            {
                return SettingsTransferError.Invalid($"The import would create two servers named '{name}'.");
            }
        }

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (!step.WritesConnection)
            {
                continue;
            }

            var invalid = JumpHostGuard.Check(step.LocalId, step.Draft.JumpHostId, finalConnections);
            if (invalid is not null)
            {
                return SettingsTransferError.Invalid($"Server '{step.Draft.Name}': {invalid.Message}");
            }
        }

        return null;
    }
}
