using DTech.Core.Credentials;
using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer.Document;
using DTech.Core.SettingsTransfer.Import;
using DTech.Core.SettingsTransfer.Secrets;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;

namespace DTech.Core.Tests.SettingsTransfer;

[TestClass]
public sealed class ConnectionImportApplierTests
{
    private const string ImportedPassword = "imported-server-password";

    private InMemorySavedConnectionRepository _repository = null!;
    private InMemoryCredentialStore _credentialStore = null!;
    private ConnectionImportApplier _applier = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _repository = new InMemorySavedConnectionRepository();
        _credentialStore = new InMemoryCredentialStore();
        var secretWriter = new ConnectionSecretWriter(_credentialStore);
        _applier = new ConnectionImportApplier(_repository, secretWriter, TimeProvider.System);
    }

    [TestMethod]
    public async Task Apply_AddsServersWithTheirPasswordsStoredUnderTheLocalId()
    {
        var imported = SettingsSamples.Connection("web") with { AuthMethod = "Password" };
        var plan = await PlanAsync(ImportMode.Merge, imported);
        using var secrets = CreateSecrets(imported.Id, ArchivedSecretKind.Password);

        var outcome = await _applier.ApplyAsync(plan, secrets, TestContext.CancellationToken);

        outcome.Added.Should().Be(1);
        outcome.SecretsStored.Should().Be(1);
        var saved = _repository.Snapshot.Single();
        saved.HasPassword.Should().BeTrue();
        var key = PasswordCredentialKey.For(saved.Id);
        _credentialStore.Secrets[key.Value].Should().Be(ImportedPassword);
    }

    [TestMethod]
    public async Task Apply_StoresTheSudoPasswordForTheLocalServer()
    {
        var imported = SettingsSamples.Connection("web");
        var plan = await PlanAsync(ImportMode.Merge, imported);
        using var secrets = CreateSecrets(imported.Id, ArchivedSecretKind.SudoPassword);

        await _applier.ApplyAsync(plan, secrets, TestContext.CancellationToken);

        var saved = _repository.Snapshot.Single();
        var key = ConnectionSecretKeys.For(saved.Id, ArchivedSecretKind.SudoPassword);
        _credentialStore.Secrets[key.Value].Should().Be(ImportedPassword);
    }

    [TestMethod]
    public async Task Apply_WhenTheCredentialStoreRejectsThePassword_ImportsTheServerWithoutItAndWarns()
    {
        var imported = SettingsSamples.Connection("web") with { AuthMethod = "Password" };
        var plan = await PlanAsync(ImportMode.Merge, imported);
        using var secrets = CreateSecrets(imported.Id, ArchivedSecretKind.Password);
        _credentialStore.FailureToReturn = CredentialErrors.VaultLocked();

        var outcome = await _applier.ApplyAsync(plan, secrets, TestContext.CancellationToken);

        _repository.Snapshot.Single().HasPassword.Should().BeFalse();
        outcome.SecretsStored.Should().Be(0);
        outcome.Warnings.Should().ContainSingle().Which.Should().Contain("could not be saved").And.NotContain(ImportedPassword);
    }

    [TestMethod]
    public async Task Apply_KeepExisting_LeavesTheServerAndItsPasswordAlone()
    {
        var existing = SettingsSamples.Saved("web");
        await _repository.AddAsync(existing, TestContext.CancellationToken);
        var imported = SettingsSamples.Connection("web") with { Host = "10.0.0.9", AuthMethod = "Password" };
        var plan = await PlanAsync(ImportMode.Merge, imported);
        using var secrets = CreateSecrets(imported.Id, ArchivedSecretKind.Password);

        var outcome = await _applier.ApplyAsync(plan, secrets, TestContext.CancellationToken);

        outcome.Kept.Should().Be(1);
        _repository.Snapshot.Single().Should().Be(existing);
        _credentialStore.Secrets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Apply_Replace_RemovesMissingServersWithTheirSavedPasswords()
    {
        var removed = SettingsSamples.Saved("db") with { HasPassword = true };
        await _repository.AddAsync(removed, TestContext.CancellationToken);
        var passwordKey = PasswordCredentialKey.For(removed.Id);
        using var storedPassword = Secret.FromString("old");
        await _credentialStore.Store(passwordKey, storedPassword, TestContext.CancellationToken);
        var imported = SettingsSamples.Connection("web");
        var plan = await PlanAsync(ImportMode.Replace, imported);

        var outcome = await _applier.ApplyAsync(plan, null, TestContext.CancellationToken);

        outcome.Removed.Should().Be(1);
        outcome.Added.Should().Be(1);
        _repository.Snapshot.Should().ContainSingle().Which.Details.Name.Should().Be("web");
        _credentialStore.Secrets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Apply_UseImportedThatDropsPasswordSignIn_RemovesTheOldPassword()
    {
        var existing = SettingsSamples.Saved("web");
        var existingWithPassword = existing with { HasPassword = true, Details = existing.Details with { AuthMethod = ConnectionAuthMethod.Password } };
        await _repository.AddAsync(existingWithPassword, TestContext.CancellationToken);
        var passwordKey = PasswordCredentialKey.For(existing.Id);
        using var storedPassword = Secret.FromString("old");
        await _credentialStore.Store(passwordKey, storedPassword, TestContext.CancellationToken);
        var imported = SettingsSamples.Connection("web");
        var resolutions = new Dictionary<Guid, ConnectionConflictResolution> { [imported.Id] = ConnectionConflictResolution.UseImported };
        var plan = await PlanAsync(ImportMode.Merge, imported, resolutions);

        var outcome = await _applier.ApplyAsync(plan, null, TestContext.CancellationToken);

        outcome.Updated.Should().Be(1);
        var saved = _repository.Snapshot.Single();
        saved.Id.Should().Be(existing.Id);
        saved.HasPassword.Should().BeFalse();
        saved.Details.AuthMethod.Should().Be(ConnectionAuthMethod.Agent);
        _credentialStore.Secrets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Apply_AnUnchangedServer_ReceivesItsPasswordFromTheArchive()
    {
        var existing = SettingsSamples.Saved("web");
        var passwordServer = existing with { Details = existing.Details with { AuthMethod = ConnectionAuthMethod.Password } };
        await _repository.AddAsync(passwordServer, TestContext.CancellationToken);
        var imported = SettingsSamples.Connection("web") with { AuthMethod = "Password" };
        var plan = await PlanAsync(ImportMode.Merge, imported);
        using var secrets = CreateSecrets(imported.Id, ArchivedSecretKind.Password);

        var outcome = await _applier.ApplyAsync(plan, secrets, TestContext.CancellationToken);

        outcome.Unchanged.Should().Be(1);
        _repository.Snapshot.Single().HasPassword.Should().BeTrue();
        var key = PasswordCredentialKey.For(existing.Id);
        _credentialStore.Secrets[key.Value].Should().Be(ImportedPassword);
    }

    private async Task<SettingsImportPlan> PlanAsync(ImportMode mode, ExportedConnection imported, Dictionary<Guid, ConnectionConflictResolution>? resolutions = null)
    {
        var existing = await _repository.ListAsync(TestContext.CancellationToken);
        var document = SettingsSamples.Document(imported);
        var options = SettingsSamples.Options(mode, resolutions);
        return SettingsImportPlanner.Plan(document, existing, options).Value;
    }

    private static SecretsArchiveContent CreateSecrets(Guid importedId, ArchivedSecretKind kind)
    {
        var value = Secret.FromString(ImportedPassword);
        var secret = new ArchivedSecret(importedId, kind, value);
        ArchivedSecret[] secrets = [secret];
        var exportId = Guid.NewGuid();
        return new SecretsArchiveContent(exportId, secrets);
    }
}
