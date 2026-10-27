using DTech.Core.Credentials;

namespace DTech.Core.SettingsTransfer.Import;

public sealed record SecretsImportInput(byte[] Archive, string Password)
{
    public override string ToString()
    {
        return $"{nameof(SecretsImportInput)} {{ {nameof(Archive)} = {Archive.Length} bytes, {nameof(Password)} = {Secret.Redacted} }}";
    }
}
