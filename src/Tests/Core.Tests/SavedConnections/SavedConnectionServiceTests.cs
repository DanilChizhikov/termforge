using DTech.Core.Credentials;
using DTech.Core.SavedConnections;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;

namespace DTech.Core.Tests.SavedConnections;

[TestClass]
public sealed class SavedConnectionServiceTests
{
    private const string Password = "s3cret-value-1";

    private InMemorySavedConnectionRepository _repository = null!;
    private InMemoryCredentialStore _credentials = null!;
    private SavedConnectionService _service = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _repository = new InMemorySavedConnectionRepository();
        _credentials = new InMemoryCredentialStore();
        var passwordStore = new ConnectionPasswordStore(_credentials);
        _service = new SavedConnectionService(_repository, passwordStore, TimeProvider.System);
    }

    [TestMethod]
    public async Task Create_WithValidDraft_StoresNormalizedConnection()
    {
        var draft = CreateDraft("  web  ") with { GroupName = "  prod  ", Host = " 10.0.0.1 " };

        var result = await _service.CreateAsync(new SavedConnectionSubmission(draft, null), TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Details.Name.Should().Be("web");
        result.Value.Details.GroupName.Should().Be("prod");
        result.Value.Details.Host.Should().Be("10.0.0.1");
        _repository.Snapshot.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Create_WithPassword_StoresPasswordOnlyInCredentialStore()
    {
        var draft = CreateDraft("db") with { AuthMethod = ConnectionAuthMethod.Password };

        var result = await _service.CreateAsync(new SavedConnectionSubmission(draft, Password), TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasPassword.Should().BeTrue();
        var key = PasswordCredentialKey.For(result.Value.Id);
        _credentials.Secrets[key.Value].Should().Be(Password);
        _repository.Snapshot.Single().ToString().Should().NotContain(Password);
        typeof(SavedConnection).GetProperties().Select(property => property.Name).Should().NotContain(name => name.Contains("Password") && name != nameof(SavedConnection.HasPassword));
        typeof(SavedConnectionDraft).GetProperties().Select(property => property.Name).Should().NotContain(name => name.Contains("Password"));
    }

    [TestMethod]
    public async Task Create_WhenCredentialStoreFails_DoesNotSaveConnection()
    {
        _credentials.FailureToReturn = CredentialErrors.VaultLocked();
        var draft = CreateDraft("db") with { AuthMethod = ConnectionAuthMethod.Password };

        var result = await _service.CreateAsync(new SavedConnectionSubmission(draft, Password), TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(SavedConnectionErrorCode.CredentialStoreFailure);
        result.Error.CredentialCode.Should().Be(CredentialErrorCode.VaultLocked);
        _repository.Snapshot.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Create_WithPasswordForNonPasswordAuthentication_IsRejected()
    {
        var draft = CreateDraft("web");

        var result = await _service.CreateAsync(new SavedConnectionSubmission(draft, Password), TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(SavedConnectionErrorCode.Invalid);
        _credentials.Secrets.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("", "host", 22, "user")]
    [DataRow("name", "", 22, "user")]
    [DataRow("name", "bad host", 22, "user")]
    [DataRow("name", "host", 0, "user")]
    [DataRow("name", "host", 70000, "user")]
    [DataRow("name", "host", 22, "")]
    public async Task Create_WithInvalidFields_ReturnsInvalid(string name, string host, int port, string userName)
    {
        var draft = new SavedConnectionDraft { Name = name, Host = host, Port = port, UserName = userName };

        var result = await _service.CreateAsync(new SavedConnectionSubmission(draft, null), TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(SavedConnectionErrorCode.Invalid);
        _repository.Snapshot.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Create_KeyFileWithoutPath_ReturnsInvalid()
    {
        var draft = CreateDraft("web") with { AuthMethod = ConnectionAuthMethod.KeyFile };

        var result = await _service.CreateAsync(new SavedConnectionSubmission(draft, null), TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.Invalid);
    }

    [TestMethod]
    public async Task Create_WithExistingNameIgnoringCase_ReturnsNameTaken()
    {
        await CreateAsync(CreateDraft("Web"));

        var result = await _service.CreateAsync(new SavedConnectionSubmission(CreateDraft("web"), null), TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.NameTaken);
    }

    [TestMethod]
    public async Task Create_WithUnknownJumpHost_ReturnsInvalid()
    {
        var draft = CreateDraft("web") with { JumpHostId = Guid.NewGuid() };

        var result = await _service.CreateAsync(new SavedConnectionSubmission(draft, null), TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.Invalid);
    }

    [TestMethod]
    public async Task Update_ChangesFieldsAndKeepsStoredPasswordWhenNoneSent()
    {
        var created = await CreateAsync(CreateDraft("db") with { AuthMethod = ConnectionAuthMethod.Password }, Password);
        var changed = created.Details with { Host = "10.9.9.9", GroupName = "prod" };

        var result = await _service.UpdateAsync(created.Id, new SavedConnectionSubmission(changed, null), TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Details.Host.Should().Be("10.9.9.9");
        result.Value.HasPassword.Should().BeTrue();
        _credentials.Secrets[PasswordCredentialKey.For(created.Id).Value].Should().Be(Password);
    }

    [TestMethod]
    public async Task Update_WithNewPassword_ReplacesStoredPassword()
    {
        var created = await CreateAsync(CreateDraft("db") with { AuthMethod = ConnectionAuthMethod.Password }, Password);

        await _service.UpdateAsync(created.Id, new SavedConnectionSubmission(created.Details, "replacement-1"), TestContext.CancellationToken);

        _credentials.Secrets[PasswordCredentialKey.For(created.Id).Value].Should().Be("replacement-1");
    }

    [TestMethod]
    public async Task Update_SwitchingAwayFromPassword_RemovesStoredPassword()
    {
        var created = await CreateAsync(CreateDraft("db") with { AuthMethod = ConnectionAuthMethod.Password }, Password);
        var changed = created.Details with { AuthMethod = ConnectionAuthMethod.Agent };

        var result = await _service.UpdateAsync(created.Id, new SavedConnectionSubmission(changed, null), TestContext.CancellationToken);

        result.Value.HasPassword.Should().BeFalse();
        _credentials.Secrets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Update_OfUnknownConnection_ReturnsNotFound()
    {
        var result = await _service.UpdateAsync(Guid.NewGuid(), new SavedConnectionSubmission(CreateDraft("x"), null), TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.NotFound);
    }

    [TestMethod]
    public async Task Update_WithJumpHostLoop_ReturnsInvalid()
    {
        var first = await CreateAsync(CreateDraft("first"));
        var second = await CreateAsync(CreateDraft("second") with { JumpHostId = first.Id });
        var looping = first.Details with { JumpHostId = second.Id };

        var result = await _service.UpdateAsync(first.Id, new SavedConnectionSubmission(looping, null), TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.Invalid);
    }

    [TestMethod]
    public async Task Update_KeepingOwnNameIsAllowed()
    {
        var created = await CreateAsync(CreateDraft("web"));

        var result = await _service.UpdateAsync(created.Id, new SavedConnectionSubmission(created.Details, null), TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public async Task Delete_RemovesConnectionAndItsPassword()
    {
        var created = await CreateAsync(CreateDraft("db") with { AuthMethod = ConnectionAuthMethod.Password }, Password);

        var result = await _service.DeleteAsync(created.Id, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _repository.Snapshot.Should().BeEmpty();
        _credentials.Secrets.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Delete_WhenPasswordCannotBeRemoved_KeepsConnection()
    {
        var created = await CreateAsync(CreateDraft("db") with { AuthMethod = ConnectionAuthMethod.Password }, Password);
        _credentials.FailureToReturn = CredentialErrors.VaultLocked();

        var result = await _service.DeleteAsync(created.Id, TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        _repository.Snapshot.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Delete_OfJumpHostInUse_ReturnsInUse()
    {
        var bastion = await CreateAsync(CreateDraft("bastion"));
        await CreateAsync(CreateDraft("inner") with { JumpHostId = bastion.Id });

        var result = await _service.DeleteAsync(bastion.Id, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.InUse);
        _repository.Snapshot.Should().HaveCount(2);
    }

    [TestMethod]
    public async Task Delete_OfUnknownConnection_ReturnsNotFound()
    {
        var result = await _service.DeleteAsync(Guid.NewGuid(), TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.NotFound);
    }

    [TestMethod]
    public async Task RevealPassword_WithStoredPassword_ReturnsIt()
    {
        var draft = CreateDraft("db") with { AuthMethod = ConnectionAuthMethod.Password };
        var created = await CreateAsync(draft, Password);

        var result = await _service.RevealPasswordAsync(created.Id, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Password);
    }

    [TestMethod]
    public async Task RevealPassword_WithoutStoredPassword_ReturnsNotFound()
    {
        var draft = CreateDraft("web");
        var created = await CreateAsync(draft);

        var result = await _service.RevealPasswordAsync(created.Id, TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(SavedConnectionErrorCode.NotFound);
    }

    [TestMethod]
    public async Task RevealPassword_OfUnknownConnection_ReturnsNotFound()
    {
        var unknownId = Guid.NewGuid();

        var result = await _service.RevealPasswordAsync(unknownId, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.NotFound);
    }

    [TestMethod]
    public async Task RevealPassword_WhenVaultIsLocked_ReturnsCredentialError()
    {
        var draft = CreateDraft("db") with { AuthMethod = ConnectionAuthMethod.Password };
        var created = await CreateAsync(draft, Password);
        _credentials.FailureToReturn = CredentialErrors.VaultLocked();

        var result = await _service.RevealPasswordAsync(created.Id, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.CredentialStoreFailure);
        result.Error.CredentialCode.Should().Be(CredentialErrorCode.VaultLocked);
    }

    [TestMethod]
    public async Task RenameGroup_MovesConnectionsToNewGroup()
    {
        await CreateAsync(CreateDraft("one") with { GroupName = "old" });
        await CreateAsync(CreateDraft("two") with { GroupName = "other" });

        var result = await _service.RenameGroupAsync("old", " new ", TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _repository.Snapshot.Select(connection => connection.Details.GroupName).Should().BeEquivalentTo("new", "other");
    }

    [TestMethod]
    public async Task RenameGroup_WithEmptyName_ReturnsInvalid()
    {
        var result = await _service.RenameGroupAsync("old", "  ", TestContext.CancellationToken);

        result.Error!.Code.Should().Be(SavedConnectionErrorCode.Invalid);
    }

    [TestMethod]
    public async Task RemoveGroup_UngroupsConnections()
    {
        await CreateAsync(CreateDraft("one") with { GroupName = "old" });

        await _service.RemoveGroupAsync("old", TestContext.CancellationToken);

        _repository.Snapshot.Single().Details.GroupName.Should().BeNull();
    }

    private static SavedConnectionDraft CreateDraft(string name)
    {
        return new SavedConnectionDraft { Name = name, Host = "example.org", UserName = "deploy" };
    }

    private async Task<SavedConnection> CreateAsync(SavedConnectionDraft draft, string? password = null)
    {
        var submission = new SavedConnectionSubmission(draft, password);
        var result = await _service.CreateAsync(submission, TestContext.CancellationToken);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value;
    }
}
