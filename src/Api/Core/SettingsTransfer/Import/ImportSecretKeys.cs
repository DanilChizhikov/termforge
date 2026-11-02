using DTech.Core.Credentials;
using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer.Secrets;

namespace DTech.Core.SettingsTransfer.Import;

public static class ImportSecretKeys
{
    public static IReadOnlyList<CredentialKey> Collect(PreparedSettingsImport prepared)
    {
        var keys = new List<CredentialKey>();
        if (!prepared.Sections.Connections)
        {
            return keys;
        }

        var secrets = prepared.Loaded.Secrets;
        var steps = prepared.Plan.Steps;
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (!step.AppliesSecrets)
            {
                continue;
            }

            var existing = step.IsNewConnection ? null : step.Existing;
            var removesPassword = step.WritesConnection && step.Draft.AuthMethod != ConnectionAuthMethod.Password && existing?.HasPassword == true;
            var writesPassword = step.Draft.AuthMethod == ConnectionAuthMethod.Password && secrets?.Find(step.Imported.Id, ArchivedSecretKind.Password) is not null;
            if (removesPassword || writesPassword)
            {
                keys.Add(PasswordCredentialKey.For(step.LocalId));
            }

            if (secrets?.Find(step.Imported.Id, ArchivedSecretKind.SudoPassword) is not null)
            {
                keys.Add(ConnectionSecretKeys.For(step.LocalId, ArchivedSecretKind.SudoPassword));
            }
        }

        var removals = prepared.Plan.Removals;
        for (var i = 0; i < removals.Count; i++)
        {
            keys.Add(PasswordCredentialKey.For(removals[i].Id));
            keys.Add(ConnectionSecretKeys.For(removals[i].Id, ArchivedSecretKind.SudoPassword));
        }

        return keys;
    }
}
