using DTech.Core.Credentials;

namespace DTech.Entrypoint.SettingsTransfer;

public sealed class SettingsImportBody
{
    public string? SettingsFile { get; set; }

    public string? SecretsArchive { get; set; }

    public string? Password { get; set; }

    public string? Mode { get; set; }

    public SettingsSectionsBody? Sections { get; set; }

    public Dictionary<string, string>? Resolutions { get; set; }

    public bool ConfirmReplace { get; set; }

    public override string ToString()
    {
        return $"{nameof(SettingsImportBody)} {{ {nameof(Mode)} = {Mode}, {nameof(Password)} = {Secret.Redacted} }}";
    }
}
