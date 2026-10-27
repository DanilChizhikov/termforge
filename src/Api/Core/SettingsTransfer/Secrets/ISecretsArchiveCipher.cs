using DTech.Core.Credentials;

namespace DTech.Core.SettingsTransfer.Secrets;

public interface ISecretsArchiveCipher
{
    byte[] Encrypt(SecretsArchiveContent content, Secret password);

    SettingsTransferResult<SecretsArchiveContent> Decrypt(byte[] archive, Secret password);
}
