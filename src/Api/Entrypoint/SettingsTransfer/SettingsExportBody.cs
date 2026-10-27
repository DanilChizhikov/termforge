using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Export;

namespace DTech.Entrypoint.SettingsTransfer;

public sealed class SettingsExportBody
{
    public SettingsSectionsBody? Sections { get; set; }

    public SecretsExportBody? Secrets { get; set; }

    public SettingsExportRequest ToRequest()
    {
        var sections = Sections?.ToSections() ?? default(SettingsSections);
        var secrets = Secrets is null ? null : new SecretsExportOptions(Secrets.Password ?? string.Empty, Secrets.Confirmed);
        Secrets = null;
        return new SettingsExportRequest(sections, secrets);
    }

    public override string ToString()
    {
        return $"{nameof(SettingsExportBody)} {{ {nameof(Sections)} = {Sections}, {nameof(Secrets)} = {Secrets} }}";
    }
}
