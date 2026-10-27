using DTech.Core.SettingsTransfer.Document;
using DTech.Core.SettingsTransfer.Import;
using FluentAssertions;

namespace DTech.Core.Tests.SettingsTransfer;

[TestClass]
public sealed class ExportedConnectionMapperTests
{
    [TestMethod]
    public void ExportAndImport_KeepTheReadOnlyFlag()
    {
        var saved = SettingsSamples.Saved("db-01");
        var readOnly = saved with { Details = saved.Details with { ReadOnly = true } };

        var exported = ExportedConnectionMapper.ToExported(readOnly);
        var imported = ExportedConnectionMapper.TryToDraft(exported);

        exported.ReadOnly.Should().BeTrue();
        imported!.ReadOnly.Should().BeTrue();
    }

    [TestMethod]
    public void ChangeDetector_ReportsReadOnlyChange()
    {
        var current = SettingsSamples.Saved("db-01").Details;
        var imported = current with { ReadOnly = true };

        var changes = ConnectionChangeDetector.Detect(current, imported);

        changes.Should().Equal("read-only");
    }
}
