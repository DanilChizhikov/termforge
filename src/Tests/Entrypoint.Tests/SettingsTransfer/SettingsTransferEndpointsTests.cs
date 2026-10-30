using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DTech.Core.CommandHistory;
using DTech.Core.Credentials;
using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer.Package;
using DTech.Core.SettingsTransfer.Secrets;
using DTech.Entrypoint.Tests.SettingsTransfer.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Entrypoint.Tests.SettingsTransfer;

[TestClass]
public sealed class SettingsTransferEndpointsTests
{
    private const string ExportPath = "/api/settings-transfer/export";
    private const string PreviewPath = "/api/settings-transfer/import/preview";
    private const string ImportPath = "/api/settings-transfer/import";
    private const string ForeignKey = "0000-0000-0000-0000-0000-0000-0000-0000";
    private const string ServerPassword = "server-password-never-in-plaintext";
    private const string SudoPassword = "sudo-password-never-in-plaintext";

    private SettingsTransferApiHarness _source = null!;
    private SettingsTransferApiHarness _target = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _source = await SettingsTransferApiHarness.StartAsync(TestContext);
        _target = await SettingsTransferApiHarness.StartAsync(TestContext);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _source.DisposeAsync();
        await _target.DisposeAsync();
    }

    [TestMethod]
    public async Task Export_ThenImportIntoAnotherInstallation_RestoresEverySection()
    {
        var bastionId = await _source.CreateConnectionAsync("bastion");
        var webExtra = new { jumpHostId = bastionId, groupName = "Production" };
        var webId = await _source.CreateConnectionAsync("web", webExtra);
        var recording = new { enabled = false, maxRecordingSizeMegabytes = 20, maxRecordings = 10, retentionDays = 7 };
        using var recordingSaved = await _source.SendAsync(HttpMethod.Put, "/api/recordings/settings", recording);
        var exclusions = new { patterns = new[] { "^vault ", "token=" } };
        using var exclusionsSaved = await _source.SendAsync(HttpMethod.Put, "/api/command-history/exclusions", exclusions);
        var workspace = CreateWorkspace(webId);
        using var workspaceSaved = await _source.SendAsync(HttpMethod.Put, "/api/workspace", workspace);
        await AddPinnedCommandAsync(_source, webId, "docker compose ps");

        var export = await ExportAsync(_source);
        var importBody = ImportBody(export);
        using var imported = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);
        var result = await _target.ReadJsonAsync(imported);

        recordingSaved.StatusCode.Should().Be(HttpStatusCode.OK);
        exclusionsSaved.StatusCode.Should().Be(HttpStatusCode.OK);
        workspaceSaved.StatusCode.Should().Be(HttpStatusCode.NoContent);
        imported.StatusCode.Should().Be(HttpStatusCode.OK);
        export.GetProperty("secretCount").GetInt32().Should().Be(0);
        result.GetProperty("added").GetInt32().Should().Be(2);
        result.GetProperty("preferencesImported").GetBoolean().Should().BeTrue();
        result.GetProperty("workspaceImported").GetBoolean().Should().BeTrue();
        result.GetProperty("pinnedCommandsAdded").GetInt32().Should().Be(1);
        var connections = await _target.ListConnectionsAsync();
        var web = connections.EnumerateArray().Single(item => item.GetProperty("name").GetString() == "web");
        var bastion = connections.EnumerateArray().Single(item => item.GetProperty("name").GetString() == "bastion");
        web.GetProperty("jumpHostId").GetGuid().Should().Be(bastion.GetProperty("id").GetGuid());
        web.GetProperty("groupName").GetString().Should().Be("Production");
        var targetWebId = web.GetProperty("id").GetGuid();
        var recordingSettings = await GetJsonAsync(_target, "/api/recordings/settings");
        recordingSettings.GetProperty("maxRecordingSizeMegabytes").GetInt32().Should().Be(20);
        recordingSettings.GetProperty("maxRecordings").GetInt32().Should().Be(10);
        recordingSettings.GetProperty("retentionDays").GetInt32().Should().Be(7);
        var targetExclusions = await GetJsonAsync(_target, "/api/command-history/exclusions");
        targetExclusions.GetProperty("patterns").EnumerateArray().Select(item => item.GetString()).Should().Contain(["^vault ", "token="]);
        var restored = await GetJsonAsync(_target, "/api/workspace");
        var state = restored.GetProperty("state");
        state.GetProperty("selectedConnectionId").GetGuid().Should().Be(targetWebId);
        state.GetProperty("terminalTabs")[0].GetProperty("connectionId").GetGuid().Should().Be(targetWebId);
        state.GetProperty("remotePaths")[0].GetProperty("path").GetString().Should().Be("/var/www");
        state.GetProperty("collapsedPanels")[0].GetString().Should().Be("local-files");
        var history = await GetJsonAsync(_target, $"/api/connections/{targetWebId}/history");
        var pinned = history.EnumerateArray().Should().ContainSingle().Subject;
        pinned.GetProperty("command").GetString().Should().Be("docker compose ps");
        pinned.GetProperty("isPinned").GetBoolean().Should().BeTrue();
    }

    [TestMethod]
    public async Task Export_ThenImport_PutsThePasswordsIntoTheCredentialStore()
    {
        var passwordServer = new { authMethod = "Password", password = ServerPassword };
        var sourceId = await _source.CreateConnectionAsync("web", passwordServer);
        var sudoKey = ConnectionSecretKeys.For(sourceId, ArchivedSecretKind.SudoPassword);
        _source.CredentialStore.Put(sudoKey, SudoPassword);

        var export = await ExportAsync(_source);
        var importBody = ImportBody(export);
        using var imported = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);
        var result = await _target.ReadJsonAsync(imported);

        imported.StatusCode.Should().Be(HttpStatusCode.OK);
        export.GetProperty("file").GetProperty("fileName").GetString().Should().EndWith(".tfsettings");
        export.GetProperty("secretCount").GetInt32().Should().Be(2);
        result.GetProperty("secretsStored").GetInt32().Should().Be(2);
        var connections = await _target.ListConnectionsAsync();
        var web = connections.EnumerateArray().Single();
        web.GetProperty("hasPassword").GetBoolean().Should().BeTrue();
        var targetId = web.GetProperty("id").GetGuid();
        var passwordKey = PasswordCredentialKey.For(targetId);
        var targetSudoKey = ConnectionSecretKeys.For(targetId, ArchivedSecretKind.SudoPassword);
        _target.FindSecret(passwordKey).Should().Be(ServerPassword);
        _target.FindSecret(targetSudoKey).Should().Be(SudoPassword);
    }

    [TestMethod]
    public async Task Export_NeverWritesPlaintextSecretsToTheFilesTheDatabaseOrTheLogs()
    {
        var passwordServer = new { authMethod = "Password", password = ServerPassword };
        await _source.CreateConnectionAsync("web", passwordServer);
        var body = new { sections = AllSections() };

        using var response = await _source.SendAsync(HttpMethod.Post, ExportPath, body);
        var responseText = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        using var exportDocument = JsonDocument.Parse(responseText);
        var export = exportDocument.RootElement;
        var importBody = ImportBody(export);
        using var imported = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        imported.StatusCode.Should().Be(HttpStatusCode.OK);
        var key = ReadKey(export);
        var settingsContent = ReadSettingsContent(export);
        var package = ReadPackage(export);
        settingsContent.Should().NotContain(ServerPassword);
        settingsContent.Should().NotContain("password\":");
        responseText.Should().NotContain(ServerPassword);
        var plaintext = Encoding.UTF8.GetBytes(ServerPassword);
        var keyBytes = Encoding.UTF8.GetBytes(key);
        package.AsSpan().IndexOf(plaintext).Should().Be(-1);
        package.AsSpan().IndexOf(keyBytes).Should().Be(-1);
        AssertNoFileContains(_source.DatabaseDirectory, ServerPassword);
        AssertNoFileContains(_target.DatabaseDirectory, ServerPassword);
        AssertNoFileContains(_source.DatabaseDirectory, key);
        _source.Logs.Messages.Should().NotContain(message => message.Contains(ServerPassword) || message.Contains(key));
        _target.Logs.Messages.Should().NotContain(message => message.Contains(ServerPassword) || message.Contains(key));
    }

    [TestMethod]
    [DataRow(ForeignKey)]
    [DataRow("not-a-key")]
    public async Task Import_WithAWrongKey_IsRejectedAndChangesNothing(string wrongKey)
    {
        var passwordServer = new { authMethod = "Password", password = ServerPassword };
        await _source.CreateConnectionAsync("web", passwordServer);
        var export = await ExportAsync(_source);

        var importBody = ImportBody(export);
        importBody["key"] = wrongKey;
        using var preview = await _target.SendAsync(HttpMethod.Post, PreviewPath, importBody);
        using var imported = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);

        preview.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        imported.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await _target.ReadJsonAsync(imported);
        problem.GetProperty("code").GetString().Should().Be("WrongKey");
        var connections = await _target.ListConnectionsAsync();
        connections.GetArrayLength().Should().Be(0);
        _target.CredentialStore.Secrets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Import_WithATamperedFile_IsRejectedAsDamaged()
    {
        var passwordServer = new { authMethod = "Password", password = ServerPassword };
        await _source.CreateConnectionAsync("web", passwordServer);
        var export = await ExportAsync(_source);
        var package = ReadPackage(export);
        package[^20] ^= 0x40;
        var importBody = ImportBody(export);
        var tampered = Convert.ToBase64String(package);
        importBody["file"] = tampered;

        using var imported = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);
        var problem = await _target.ReadJsonAsync(imported);

        imported.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.GetProperty("code").GetString().Should().Be("FileDamaged");
        var connections = await _target.ListConnectionsAsync();
        connections.GetArrayLength().Should().Be(0);
    }

    [TestMethod]
    public async Task Import_APlainJsonSettingsFile_IsRejectedAsDamaged()
    {
        await _source.CreateConnectionAsync("web");
        var export = await ExportAsync(_source);
        var settingsContent = ReadSettingsContent(export);
        var plainBytes = Encoding.UTF8.GetBytes(settingsContent);
        var importBody = ImportBody(export);
        importBody["file"] = Convert.ToBase64String(plainBytes);

        using var imported = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);
        var problem = await _target.ReadJsonAsync(imported);

        imported.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.GetProperty("code").GetString().Should().Be("FileDamaged");
    }

    [TestMethod]
    public async Task Import_ASettingsFileFromANewerSchemaVersion_IsRejected()
    {
        await _source.CreateConnectionAsync("web");
        var export = await ExportAsync(_source);
        var settings = ReadSettings(export);
        settings["schemaVersion"] = 2;
        var importBody = ImportBodyWithSettings(export, settings);

        using var imported = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);
        var problem = await _target.ReadJsonAsync(imported);

        imported.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.GetProperty("code").GetString().Should().Be("UnsupportedVersion");
        var connections = await _target.ListConnectionsAsync();
        connections.GetArrayLength().Should().Be(0);
    }

    [TestMethod]
    public async Task Import_UntrustedValues_AreRejectedBeforeAnythingIsWritten()
    {
        await _source.CreateConnectionAsync("web");
        await _source.CreateConnectionAsync("db");
        var export = await ExportAsync(_source);
        var settings = ReadSettings(export);
        settings["connections"]![1]!["host"] = "-oProxyCommand=touch /tmp/pwned";
        var importBody = ImportBodyWithSettings(export, settings);

        using var imported = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);
        var problem = await _target.ReadJsonAsync(imported);

        imported.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.GetProperty("code").GetString().Should().Be("Invalid");
        var connections = await _target.ListConnectionsAsync();
        connections.GetArrayLength().Should().Be(0);
    }

    [TestMethod]
    public async Task Preview_AConflictingServer_IsReportedAndChangesNothing()
    {
        await _source.CreateConnectionAsync("web");
        await _source.CreateConnectionAsync("db");
        var export = await ExportAsync(_source);
        var conflicting = new { host = "10.0.0.5" };
        var targetWebId = await _target.CreateConnectionAsync("web", conflicting);

        var importBody = ImportBody(export);
        using var response = await _target.SendAsync(HttpMethod.Post, PreviewPath, importBody);
        var preview = await _target.ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var steps = preview.GetProperty("connections").EnumerateArray().ToList();
        var web = steps.Single(step => step.GetProperty("name").GetString() == "web");
        web.GetProperty("action").GetString().Should().Be("conflict");
        web.GetProperty("resolution").GetString().Should().Be("keepExisting");
        web.GetProperty("changes").EnumerateArray().Select(change => change.GetString()).Should().Contain("host");
        steps.Single(step => step.GetProperty("name").GetString() == "db").GetProperty("action").GetString().Should().Be("add");
        var connections = await _target.ListConnectionsAsync();
        connections.EnumerateArray().Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(targetWebId);
    }

    [TestMethod]
    public async Task Import_Merge_ResolvesConflictsPerServer()
    {
        var sourceWebId = await _source.CreateConnectionAsync("web");
        var sourceDbId = await _source.CreateConnectionAsync("db");
        await _source.CreateConnectionAsync("cache");
        var export = await ExportAsync(_source);
        var webHost = new { host = "10.0.0.5" };
        var dbHost = new { host = "10.0.0.6" };
        var targetWebId = await _target.CreateConnectionAsync("web", webHost);
        var targetDbId = await _target.CreateConnectionAsync("db", dbHost);
        var importBody = ImportBody(export);
        importBody["resolutions"] = new Dictionary<string, string>
        {
            [sourceWebId.ToString()] = "useImported",
            [sourceDbId.ToString()] = "keepBoth",
        };

        using var imported = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);
        var result = await _target.ReadJsonAsync(imported);

        imported.StatusCode.Should().Be(HttpStatusCode.OK);
        result.GetProperty("updated").GetInt32().Should().Be(1);
        result.GetProperty("added").GetInt32().Should().Be(2);
        var connections = (await _target.ListConnectionsAsync()).EnumerateArray().ToList();
        connections.Select(item => item.GetProperty("name").GetString()).Should().BeEquivalentTo("web", "db", "db (imported)", "cache");
        var web = connections.Single(item => item.GetProperty("id").GetGuid() == targetWebId);
        web.GetProperty("host").GetString().Should().Be("web.example.test");
        var db = connections.Single(item => item.GetProperty("id").GetGuid() == targetDbId);
        db.GetProperty("host").GetString().Should().Be("10.0.0.6");
    }

    [TestMethod]
    public async Task Import_Replace_RequiresConfirmationThenRemovesServersMissingFromTheFile()
    {
        await _source.CreateConnectionAsync("web");
        var export = await ExportAsync(_source);
        var oldPassword = new { authMethod = "Password", password = "old-password" };
        var oldId = await _target.CreateConnectionAsync("old", oldPassword);
        var importBody = ImportBody(export);
        importBody["mode"] = "replace";

        using var preview = await _target.SendAsync(HttpMethod.Post, PreviewPath, importBody);
        var previewBody = await _target.ReadJsonAsync(preview);
        using var unconfirmed = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);
        var afterUnconfirmed = await _target.ListConnectionsAsync();
        importBody["confirmReplace"] = true;
        using var confirmed = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);
        var result = await _target.ReadJsonAsync(confirmed);

        previewBody.GetProperty("removals").EnumerateArray().Should().ContainSingle().Which.GetProperty("name").GetString().Should().Be("old");
        unconfirmed.StatusCode.Should().Be(HttpStatusCode.Conflict);
        afterUnconfirmed.EnumerateArray().Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(oldId);
        confirmed.StatusCode.Should().Be(HttpStatusCode.OK);
        result.GetProperty("removed").GetInt32().Should().Be(1);
        var connections = await _target.ListConnectionsAsync();
        connections.EnumerateArray().Should().ContainSingle().Which.GetProperty("name").GetString().Should().Be("web");
        var oldKey = PasswordCredentialKey.For(oldId);
        _target.FindSecret(oldKey).Should().BeNull();
    }

    [TestMethod]
    public async Task Import_NeverTurnsTerminalRecordingOn()
    {
        var recording = new { enabled = true, maxRecordingSizeMegabytes = 25, maxRecordings = 50, retentionDays = 30 };
        using var saved = await _source.SendAsync(HttpMethod.Put, "/api/recordings/settings", recording);
        var export = await ExportAsync(_source);

        var importBody = ImportBody(export);
        using var imported = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);
        var settings = await GetJsonAsync(_target, "/api/recordings/settings");

        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        imported.StatusCode.Should().Be(HttpStatusCode.OK);
        settings.GetProperty("enabled").GetBoolean().Should().BeFalse();
        settings.GetProperty("maxRecordingSizeMegabytes").GetInt32().Should().Be(25);
    }

    [TestMethod]
    public async Task Export_WithoutAnySection_IsRejected()
    {
        var body = new { sections = new { connections = false } };

        using var response = await _source.SendAsync(HttpMethod.Post, ExportPath, body);
        var problem = await _source.ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.GetProperty("code").GetString().Should().Be("Invalid");
    }

    [TestMethod]
    public async Task Import_WithoutAKey_IsRejected()
    {
        await _source.CreateConnectionAsync("web");
        var export = await ExportAsync(_source);
        var importBody = ImportBody(export);
        importBody["key"] = null;

        using var imported = await _target.SendAsync(HttpMethod.Post, ImportPath, importBody);
        var problem = await _target.ReadJsonAsync(imported);

        imported.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.GetProperty("code").GetString().Should().Be("Invalid");
    }

    [TestMethod]
    public async Task Export_WithOnlySomeSections_LeavesTheOthersOut()
    {
        await _source.CreateConnectionAsync("web");
        var body = new { sections = new { preferences = true } };

        using var response = await _source.SendAsync(HttpMethod.Post, ExportPath, body);
        var export = await _source.ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = ReadSettings(export);
        settings["connections"].Should().BeNull();
        settings["workspace"].Should().BeNull();
        settings["preferences"].Should().NotBeNull();
        export.GetProperty("file").GetProperty("fileName").GetString().Should().StartWith("termforge-settings-").And.EndWith(".tfsettings");
    }

    [TestMethod]
    public async Task Summary_CountsServersGroupsAndPinnedCommands()
    {
        var group = new { groupName = "Production" };
        var webId = await _source.CreateConnectionAsync("web", group);
        await _source.CreateConnectionAsync("db", group);
        await AddPinnedCommandAsync(_source, webId, "uptime");

        var summary = await GetJsonAsync(_source, "/api/settings-transfer/summary");

        summary.GetProperty("connections").GetInt32().Should().Be(2);
        summary.GetProperty("groups").GetInt32().Should().Be(1);
        summary.GetProperty("pinnedCommands").GetInt32().Should().Be(1);
    }

    private static string ReadKey(JsonElement export)
    {
        return export.GetProperty("key").GetString()!;
    }

    private static byte[] ReadPackage(JsonElement export)
    {
        var encoded = export.GetProperty("file").GetProperty("contentBase64").GetString()!;
        return Convert.FromBase64String(encoded);
    }

    private static object AllSections()
    {
        return new { connections = true, preferences = true, workspace = true, pinnedCommands = true };
    }

    private static Dictionary<string, object?> ImportBody(JsonElement export)
    {
        return new Dictionary<string, object?>
        {
            ["file"] = export.GetProperty("file").GetProperty("contentBase64").GetString(),
            ["key"] = ReadKey(export),
            ["mode"] = "merge",
            ["sections"] = AllSections(),
        };
    }

    private string ReadSettingsContent(JsonElement export)
    {
        using var package = DecryptPackage(export);
        return package.SettingsJson;
    }

    private JsonNode ReadSettings(JsonElement export)
    {
        var content = ReadSettingsContent(export);
        return JsonNode.Parse(content)!;
    }

    private Dictionary<string, object?> ImportBodyWithSettings(JsonElement export, JsonNode settings)
    {
        var cipher = _source.Factory.Services.GetRequiredService<ISettingsPackageCipher>();
        using var original = DecryptPackage(export);
        var settingsJson = settings.ToJsonString();
        var modified = new SettingsPackage(settingsJson, original.Secrets);
        using var key = NormalizedKey(export);
        var encrypted = cipher.Encrypt(modified, key);
        var importBody = ImportBody(export);
        importBody["file"] = Convert.ToBase64String(encrypted);
        return importBody;
    }

    private SettingsPackage DecryptPackage(JsonElement export)
    {
        var cipher = _source.Factory.Services.GetRequiredService<ISettingsPackageCipher>();
        var package = ReadPackage(export);
        using var key = NormalizedKey(export);
        var decrypted = cipher.Decrypt(package, key);
        decrypted.IsSuccess.Should().BeTrue();
        return decrypted.Value;
    }

    private static Secret NormalizedKey(JsonElement export)
    {
        var key = ReadKey(export);
        var normalized = ExportKeyNormalizer.Normalize(key)!;
        return Secret.FromString(normalized);
    }

    private static object CreateWorkspace(Guid connectionId)
    {
        var state = new
        {
            selectedConnectionId = connectionId,
            activeView = "workspace",
            terminalTabs = new[] { new { key = "terminal-1", connectionId, connectionName = "web" } },
            activeTerminalKey = "terminal-1",
            localPath = "/home/dev",
            remotePaths = new[] { new { connectionId, path = "/var/www" } },
            panelLayouts = new[] { new { groupId = "app-layout", sizes = new Dictionary<string, double> { ["servers"] = 30, ["main"] = 70 } } },
            collapsedPanels = new[] { "local-files" },
            editorTabs = Array.Empty<object>(),
            activeEditorTab = (object?)null,
        };
        return new { state, drafts = Array.Empty<object>() };
    }

    private static void AssertNoFileContains(string directory, string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        for (var i = 0; i < files.Length; i++)
        {
            using var stream = new FileStream(files[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var content = memory.ToArray();
            content.AsSpan().IndexOf(secretBytes).Should().Be(-1, $"'{files[i]}' must not contain the plaintext secret");
        }
    }

    private async Task<JsonElement> ExportAsync(SettingsTransferApiHarness harness)
    {
        var body = new { sections = AllSections() };
        using var response = await harness.SendAsync(HttpMethod.Post, ExportPath, body);
        var content = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private async Task<JsonElement> GetJsonAsync(SettingsTransferApiHarness harness, string path)
    {
        using var response = await harness.SendAsync(HttpMethod.Get, path, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await harness.ReadJsonAsync(response);
    }

    private async Task AddPinnedCommandAsync(SettingsTransferApiHarness harness, Guid connectionId, string command)
    {
        using var scope = harness.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICommandHistoryRepository>();
        var entryId = Guid.NewGuid();
        var executedAt = DateTime.UtcNow;
        var entry = new CommandHistoryEntry(entryId, connectionId, command, executedAt, 0, true);
        await repository.AddAsync(entry, TestContext.CancellationToken);
    }
}
