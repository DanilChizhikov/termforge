using DTech.Core.SettingsTransfer.Secrets;

namespace DTech.Core.SettingsTransfer.Import;

internal sealed record ConnectionImportContext(SecretsArchiveContent? Secrets, ConnectionImportOutcome Outcome, DateTime Now)
{
    public ArchivedSecret? FindSecret(ConnectionImportStep step, ArchivedSecretKind kind)
    {
        return Secrets?.Find(step.Imported.Id, kind);
    }
}
