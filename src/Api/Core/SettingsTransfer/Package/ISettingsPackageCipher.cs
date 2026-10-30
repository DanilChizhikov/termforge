using DTech.Core.Credentials;

namespace DTech.Core.SettingsTransfer.Package;

public interface ISettingsPackageCipher
{
    byte[] Encrypt(SettingsPackage package, Secret key);

    SettingsTransferResult<SettingsPackage> Decrypt(byte[] package, Secret key);
}
