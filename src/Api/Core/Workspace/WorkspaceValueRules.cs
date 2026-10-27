namespace DTech.Core.Workspace;

internal static class WorkspaceValueRules
{
    public static bool IsIdentifier(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > WorkspaceStateLimits.MaxIdentifierLength)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            var allowed = char.IsAsciiLetterOrDigit(character) || character == '-' || character == '_';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsPath(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > WorkspaceStateLimits.MaxPathLength)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsControl(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsEditorTab(WorkspaceEditorTab? tab)
    {
        return tab is not null && tab.ConnectionId != Guid.Empty && IsPath(tab.Path);
    }
}
