using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Import;
using FluentAssertions;

namespace DTech.Core.Tests.SettingsTransfer;

[TestClass]
public sealed class SettingsDocumentReaderTests
{
    [TestMethod]
    public void Read_AnExportedDocument_ReturnsItsContent()
    {
        var connection = SettingsSamples.Connection("web");
        var document = SettingsSamples.Document(connection);
        var json = SettingsSamples.Serialize(document);

        var read = SettingsDocumentReader.Read(json);

        read.IsSuccess.Should().BeTrue();
        read.Value.ExportId.Should().Be(document.ExportId);
        read.Value.Connections.Should().ContainSingle().Which.Should().BeEquivalentTo(connection);
    }

    [TestMethod]
    public void Read_ANewerSchemaVersion_IsRejectedAsUnsupported()
    {
        const string json = """{ "format": "termforge-settings", "schemaVersion": 2, "exportId": "4d7c3f0e-6f1d-4c55-9c39-0d2a8e1e2f10" }""";

        var read = SettingsDocumentReader.Read(json);

        read.Error!.Code.Should().Be(SettingsTransferErrorCode.UnsupportedVersion);
        read.Error.Message.Should().Contain("newer TermForge");
    }

    [TestMethod]
    [DataRow("""{ "format": "termforge-settings", "schemaVersion": 0 }""")]
    [DataRow("""{ "format": "termforge-settings" }""")]
    [DataRow("""{ "format": "termforge-settings", "schemaVersion": "1" }""")]
    public void Read_AMissingOrInvalidSchemaVersion_IsRejected(string json)
    {
        var read = SettingsDocumentReader.Read(json);

        read.Error!.Code.Should().Be(SettingsTransferErrorCode.Invalid);
    }

    [TestMethod]
    [DataRow("""{ "format": "something-else", "schemaVersion": 1 }""")]
    [DataRow("""[1, 2, 3]""")]
    [DataRow("""not json""")]
    [DataRow("")]
    public void Read_AFileThatIsNotASettingsExport_IsRejected(string json)
    {
        var read = SettingsDocumentReader.Read(json);

        read.Error!.Code.Should().Be(SettingsTransferErrorCode.Invalid);
    }

    [TestMethod]
    public void Read_AnUnknownProperty_IsRejectedBecauseTheSchemaIsStrict()
    {
        const string json = """{ "format": "termforge-settings", "schemaVersion": 1, "exportId": "4d7c3f0e-6f1d-4c55-9c39-0d2a8e1e2f10", "passwords": ["hunter2"] }""";

        var read = SettingsDocumentReader.Read(json);

        read.Error!.Code.Should().Be(SettingsTransferErrorCode.Invalid);
    }

    [TestMethod]
    public void Read_AFileOverTheSizeLimit_IsRejected()
    {
        var padding = new string(' ', SettingsTransferLimits.MaxSettingsFileCharacters);
        var json = $"{{ \"format\": \"termforge-settings\", \"schemaVersion\": 1 }}{padding}";

        var read = SettingsDocumentReader.Read(json);

        read.Error!.Code.Should().Be(SettingsTransferErrorCode.TooLarge);
    }

    [TestMethod]
    public void Read_DeeplyNestedJson_IsRejected()
    {
        var levels = Enumerable.Repeat("{\"a\":", 100);
        var opening = string.Concat(levels);
        var closing = new string('}', 100);
        var json = $"{opening}1{closing}";

        var read = SettingsDocumentReader.Read(json);

        read.Error!.Code.Should().Be(SettingsTransferErrorCode.Invalid);
    }
}
