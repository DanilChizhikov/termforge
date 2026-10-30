using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer.Secrets;

namespace DTech.Core.SettingsTransfer.Export;

public sealed class ConnectionSecretsCollector(ICredentialStore credentialStore)
{
    private static readonly ArchivedSecretKind[] Kinds = [ArchivedSecretKind.Password, ArchivedSecretKind.SudoPassword];

    public async Task<SettingsTransferResult<SecretsArchiveContent>> CollectAsync(Guid exportId, IReadOnlyList<Guid> connectionIds, CancellationToken cancellationToken)
    {
        var secrets = new List<ArchivedSecret>();
        var collected = new SecretsArchiveContent(exportId, secrets);
        for (var i = 0; i < connectionIds.Count; i++)
        {
            for (var j = 0; j < Kinds.Length; j++)
            {
                var connectionId = connectionIds[i];
                var kind = Kinds[j];
                var key = ConnectionSecretKeys.For(connectionId, kind);
                var read = await credentialStore.Read(key, cancellationToken);
                if (read.IsSuccess)
                {
                    var secret = new ArchivedSecret(connectionId, kind, read.Value);
                    secrets.Add(secret);
                    continue;
                }

                if (read.Error.Code != CredentialErrorCode.NotFound)
                {
                    collected.Dispose();
                    var unavailable = new SettingsTransferError(SettingsTransferErrorCode.SecretsUnavailable, $"Saved passwords could not be read: {read.Error.Message}");
                    return SettingsTransferResult<SecretsArchiveContent>.Failure(unavailable);
                }
            }
        }

        return SettingsTransferResult<SecretsArchiveContent>.Success(collected);
    }
}
