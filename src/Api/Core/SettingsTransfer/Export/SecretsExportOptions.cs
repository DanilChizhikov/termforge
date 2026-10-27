using DTech.Core.Credentials;

namespace DTech.Core.SettingsTransfer.Export;

public sealed record SecretsExportOptions(string Password, bool Confirmed)
{
    public override string ToString()
    {
        return $"{nameof(SecretsExportOptions)} {{ {nameof(Password)} = {Secret.Redacted}, {nameof(Confirmed)} = {Confirmed} }}";
    }
}
