using DTech.Core.SavedConnections;

namespace DTech.Core.SettingsTransfer.Import;

public static class ConnectionChangeDetector
{
    public static IReadOnlyList<string> Detect(SavedConnectionDraft current, SavedConnectionDraft imported)
    {
        var changes = new List<string>();
        var nameChanged = !string.Equals(current.Name, imported.Name, StringComparison.Ordinal);
        AddIf(changes, nameChanged, "name");
        AddIf(changes, current.GroupName != imported.GroupName, "group");
        AddIf(changes, current.Host != imported.Host, "host");
        AddIf(changes, current.Port != imported.Port, "port");
        AddIf(changes, current.UserName != imported.UserName, "user");
        AddIf(changes, current.AuthMethod != imported.AuthMethod, "authentication");
        AddIf(changes, current.PrivateKeyPath != imported.PrivateKeyPath, "key file");
        AddIf(changes, current.JumpHostId != imported.JumpHostId, "jump host");
        AddIf(changes, current.SshAlias != imported.SshAlias, "SSH alias");
        AddIf(changes, current.ReconnectPolicy != imported.ReconnectPolicy, "reconnect");
        AddIf(changes, current.ReadOnly != imported.ReadOnly, "read-only");
        AddIf(changes, current.AiAccess != imported.AiAccess, "AI access");
        return changes;
    }

    private static void AddIf(List<string> changes, bool changed, string field)
    {
        if (changed)
        {
            changes.Add(field);
        }
    }
}
