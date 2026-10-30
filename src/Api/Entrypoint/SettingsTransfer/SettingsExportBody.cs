using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Export;

namespace DTech.Entrypoint.SettingsTransfer;

public sealed class SettingsExportBody
{
    public SettingsSectionsBody? Sections { get; set; }

    public SettingsExportRequest ToRequest()
    {
        var sections = Sections?.ToSections() ?? default(SettingsSections);
        return new SettingsExportRequest(sections);
    }

    public override string ToString()
    {
        return $"{nameof(SettingsExportBody)} {{ {nameof(Sections)} = {Sections} }}";
    }
}
