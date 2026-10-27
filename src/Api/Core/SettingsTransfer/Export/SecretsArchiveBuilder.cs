using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer.Secrets;

namespace DTech.Core.SettingsTransfer.Export;

public sealed class SecretsArchiveBuilder(ICredentialStore credentialStore, ISecretsArchiveCipher cipher)
{
    private static readonly ArchivedSecretKind[] Kinds = [ArchivedSecretKind.Password, ArchivedSecretKind.SudoPassword];

    public async Task<SettingsTransferResult<SecretsArchiveBuild>> BuildAsync(SecretsArchiveRequest request, CancellationToken cancellationToken)
    {
        var secrets = new List<ArchivedSecret>();
        var collected = new SecretsArchiveContent(request.ExportId, secrets);
        using (collected)
        {
            var error = await CollectAsync(request.ConnectionIds, secrets, cancellationToken);
            if (error is not null)
            {
                return SettingsTransferResult<SecretsArchiveBuild>.Failure(error);
            }

            var archive = cipher.Encrypt(collected, request.Password);
            var build = new SecretsArchiveBuild(archive, secrets.Count);
            return SettingsTransferResult<SecretsArchiveBuild>.Success(build);
        }
    }

    private async Task<SettingsTransferError?> CollectAsync(IReadOnlyList<Guid> connectionIds, List<ArchivedSecret> secrets, CancellationToken cancellationToken)
    {
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
                    return new SettingsTransferError(SettingsTransferErrorCode.SecretsUnavailable, $"Saved passwords could not be read: {read.Error.Message}");
                }
            }
        }

        return null;
    }
}
