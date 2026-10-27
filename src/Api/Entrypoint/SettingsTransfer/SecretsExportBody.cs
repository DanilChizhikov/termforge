using DTech.Core.Credentials;

namespace DTech.Entrypoint.SettingsTransfer;

public sealed class SecretsExportBody
{
    public string? Password { get; set; }

    public bool Confirmed { get; set; }

    public override string ToString()
    {
        return $"{nameof(SecretsExportBody)} {{ {nameof(Password)} = {Secret.Redacted}, {nameof(Confirmed)} = {Confirmed} }}";
    }
}
