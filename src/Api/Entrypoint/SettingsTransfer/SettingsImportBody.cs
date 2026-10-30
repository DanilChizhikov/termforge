using DTech.Core.Credentials;

namespace DTech.Entrypoint.SettingsTransfer;

public sealed class SettingsImportBody
{
    public string? File { get; set; }

    public string? Key { get; set; }

    public string? Mode { get; set; }

    public SettingsSectionsBody? Sections { get; set; }

    public Dictionary<string, string>? Resolutions { get; set; }

    public bool ConfirmReplace { get; set; }

    public override string ToString()
    {
        return $"{nameof(SettingsImportBody)} {{ {nameof(Mode)} = {Mode}, {nameof(Key)} = {Secret.Redacted} }}";
    }
}
