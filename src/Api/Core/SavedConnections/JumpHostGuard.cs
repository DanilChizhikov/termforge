namespace DTech.Core.SavedConnections;

public static class JumpHostGuard
{
    public static SavedConnectionError? Check(Guid connectionId, Guid? jumpHostId, IReadOnlyList<SavedConnection> all)
    {
        if (jumpHostId is null)
        {
            return null;
        }

        var visited = new HashSet<Guid> { connectionId };
        var currentId = jumpHostId;
        while (currentId is not null)
        {
            if (!visited.Add(currentId.Value))
            {
                return SavedConnectionError.Invalid("The jump host chain must not loop back to this connection.");
            }

            var current = Find(all, currentId.Value);
            if (current is null)
            {
                return SavedConnectionError.Invalid("The jump host does not exist.");
            }

            currentId = current.Details.JumpHostId;
        }

        return null;
    }

    public static SavedConnection? Find(IReadOnlyList<SavedConnection> all, Guid id)
    {
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Id == id)
            {
                return all[i];
            }
        }

        return null;
    }
}
