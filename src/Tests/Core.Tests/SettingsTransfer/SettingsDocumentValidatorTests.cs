using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Document;
using DTech.Core.SettingsTransfer.Import;
using DTech.Core.Workspace;
using FluentAssertions;

namespace DTech.Core.Tests.SettingsTransfer;

[TestClass]
public sealed class SettingsDocumentValidatorTests
{
    [TestMethod]
    public void Validate_AValidDocument_Passes()
    {
        var jumpHost = SettingsSamples.Connection("bastion");
        var web = SettingsSamples.Connection("web", jumpHostId: jumpHost.Id);
        var document = SettingsSamples.Document(jumpHost, web);

        var error = SettingsDocumentValidator.Validate(document);

        error.Should().BeNull();
    }

    [TestMethod]
    [DataRow("-oProxyCommand=evil", "deploy")]
    [DataRow("web.example.test", "-l")]
    [DataRow("web\u0007.example.test", "deploy")]
    [DataRow("web.example.test", "dep\u001bloy")]
    public void Validate_HostOrUserThatLooksLikeAnOptionOrHasControlCharacters_IsRejected(string host, string userName)
    {
        var connection = SettingsSamples.Connection("web") with { Host = host, UserName = userName };
        var document = SettingsSamples.Document(connection);

        var error = SettingsDocumentValidator.Validate(document);

        error!.Code.Should().Be(SettingsTransferErrorCode.Invalid);
    }

    [TestMethod]
    public void Validate_AKeyPathWithControlCharacters_IsRejected()
    {
        var connection = SettingsSamples.Connection("web") with { AuthMethod = "KeyFile", PrivateKeyPath = "~/.ssh/id\nrm -rf" };
        var document = SettingsSamples.Document(connection);

        var error = SettingsDocumentValidator.Validate(document);

        error.Should().NotBeNull();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(70000)]
    public void Validate_APortOutOfRange_IsRejected(int port)
    {
        var connection = SettingsSamples.Connection("web") with { Port = port };
        var document = SettingsSamples.Document(connection);

        var error = SettingsDocumentValidator.Validate(document);

        error!.Message.Should().Contain("port");
    }

    [TestMethod]
    [DataRow("Telnet")]
    [DataRow("2")]
    [DataRow(null)]
    public void Validate_AnUnknownAuthenticationMethod_IsRejected(string? authMethod)
    {
        var connection = SettingsSamples.Connection("web") with { AuthMethod = authMethod };
        var document = SettingsSamples.Document(connection);

        var error = SettingsDocumentValidator.Validate(document);

        error.Should().NotBeNull();
    }

    [TestMethod]
    public void Validate_DuplicateNames_AreRejected()
    {
        var first = SettingsSamples.Connection("web");
        var second = SettingsSamples.Connection("WEB");
        var document = SettingsSamples.Document(first, second);

        var error = SettingsDocumentValidator.Validate(document);

        error!.Message.Should().Contain("more than once");
    }

    [TestMethod]
    public void Validate_AJumpHostOutsideTheFile_IsRejected()
    {
        var connection = SettingsSamples.Connection("web", jumpHostId: Guid.NewGuid());
        var document = SettingsSamples.Document(connection);

        var error = SettingsDocumentValidator.Validate(document);

        error!.Message.Should().Contain("not in the settings file");
    }

    [TestMethod]
    public void Validate_AJumpHostLoop_IsRejected()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var first = SettingsSamples.Connection("a", firstId, secondId);
        var second = SettingsSamples.Connection("b", secondId, firstId);
        var document = SettingsSamples.Document(first, second);

        var error = SettingsDocumentValidator.Validate(document);

        error!.Message.Should().Contain("loops");
    }

    [TestMethod]
    public void Validate_AnInvalidExclusionPattern_IsRejected()
    {
        var patterns = new[] { "(unclosed" };
        var preferences = new ExportedPreferences { CommandExclusionPatterns = patterns };
        var document = SettingsSamples.Document() with { Preferences = preferences };

        var error = SettingsDocumentValidator.Validate(document);

        error!.Message.Should().Contain("not a valid regular expression");
    }

    [TestMethod]
    public void Validate_RecordingLimitsOutOfRange_AreRejected()
    {
        var limits = new ExportedRecordingLimits { MaxRecordingSizeMegabytes = 100000, MaxRecordings = 50, RetentionDays = 30 };
        var preferences = new ExportedPreferences { TerminalRecording = limits };
        var document = SettingsSamples.Document() with { Preferences = preferences };

        var error = SettingsDocumentValidator.Validate(document);

        error.Should().NotBeNull();
    }

    [TestMethod]
    public void Validate_AWorkspaceWithAPathTraversalIdentifier_IsRejected()
    {
        var workspace = WorkspaceState.Empty with { CollapsedPanels = ["../escape"] };
        var document = SettingsSamples.Document() with { Workspace = workspace };

        var error = SettingsDocumentValidator.Validate(document);

        error!.Message.Should().Contain("workspace");
    }

    [TestMethod]
    public void Validate_APinnedCommandWithControlCharacters_IsRejected()
    {
        var command = new ExportedPinnedCommand { ConnectionId = Guid.NewGuid(), Command = "ls\u001b[2J" };
        var commands = new[] { command };
        var document = SettingsSamples.Document() with { PinnedCommands = commands };

        var error = SettingsDocumentValidator.Validate(document);

        error.Should().NotBeNull();
    }

    [TestMethod]
    public void Validate_TooManyConnections_AreRejected()
    {
        var connections = new ExportedConnection[SettingsTransferLimits.MaxConnections + 1];
        for (var i = 0; i < connections.Length; i++)
        {
            connections[i] = SettingsSamples.Connection($"server-{i}");
        }

        var document = SettingsSamples.Document(connections);

        var error = SettingsDocumentValidator.Validate(document);

        error!.Message.Should().Contain("more than");
    }
}
