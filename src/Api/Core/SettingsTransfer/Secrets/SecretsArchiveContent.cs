using DTech.Core.Credentials;

namespace DTech.Core.SettingsTransfer.Secrets;

public sealed class SecretsArchiveContent : IDisposable
{
    public SecretsArchiveContent(Guid exportId, IReadOnlyList<ArchivedSecret> secrets)
    {
        ExportId = exportId;
        Secrets = secrets;
    }

    public Guid ExportId { get; }

    public IReadOnlyList<ArchivedSecret> Secrets { get; }

    public ArchivedSecret? Find(Guid connectionId, ArchivedSecretKind kind)
    {
        for (var i = 0; i < Secrets.Count; i++)
        {
            var secret = Secrets[i];
            if (secret.ConnectionId == connectionId && secret.Kind == kind)
            {
                return secret;
            }
        }

        return null;
    }

    public int Count(ArchivedSecretKind kind)
    {
        var count = 0;
        for (var i = 0; i < Secrets.Count; i++)
        {
            if (Secrets[i].Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    public void Dispose()
    {
        for (var i = 0; i < Secrets.Count; i++)
        {
            Secrets[i].Value.Dispose();
        }
    }

    public override string ToString()
    {
        return $"{nameof(SecretsArchiveContent)} {{ {nameof(ExportId)} = {ExportId}, {nameof(Secrets)} = {Secret.Redacted} }}";
    }
}
