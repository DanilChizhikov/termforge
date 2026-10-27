using DTech.Core.SavedConnections;

namespace DTech.Core.SettingsTransfer.Import;

public static class ConnectionWriteOrder
{
    public static List<ConnectionImportStep> JumpHostsFirst(IReadOnlyList<ConnectionImportStep> steps)
    {
        var pending = new List<ConnectionImportStep>();
        var pendingIds = new HashSet<Guid>();
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].WritesConnection)
            {
                pending.Add(steps[i]);
                pendingIds.Add(steps[i].LocalId);
            }
        }

        var ordered = new List<ConnectionImportStep>(pending.Count);
        while (pending.Count > 0)
        {
            var placed = 0;
            for (var i = pending.Count - 1; i >= 0; i--)
            {
                var step = pending[i];
                var waitsForJumpHost = step.Draft.JumpHostId is { } jumpHostId && jumpHostId != step.LocalId && pendingIds.Contains(jumpHostId);
                if (waitsForJumpHost)
                {
                    continue;
                }

                ordered.Add(step);
                pendingIds.Remove(step.LocalId);
                pending.RemoveAt(i);
                placed++;
            }

            if (placed == 0)
            {
                throw new InvalidOperationException("The jump host chain of the imported connections loops.");
            }
        }

        return ordered;
    }

    public static List<SavedConnection> DependentsFirst(IReadOnlyList<SavedConnection> removals)
    {
        var pending = removals.ToList();
        var ordered = new List<SavedConnection>(pending.Count);
        while (pending.Count > 0)
        {
            var referenced = new HashSet<Guid>();
            for (var i = 0; i < pending.Count; i++)
            {
                if (pending[i].Details.JumpHostId is { } jumpHostId && jumpHostId != pending[i].Id)
                {
                    referenced.Add(jumpHostId);
                }
            }

            var placed = 0;
            for (var i = pending.Count - 1; i >= 0; i--)
            {
                if (!referenced.Contains(pending[i].Id))
                {
                    ordered.Add(pending[i]);
                    pending.RemoveAt(i);
                    placed++;
                }
            }

            if (placed == 0)
            {
                ordered.AddRange(pending);
                pending.Clear();
            }
        }

        return ordered;
    }
}
