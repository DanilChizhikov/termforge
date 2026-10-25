using System.Buffers.Binary;
using System.Text;
using DTech.Core.Credentials;
using DTech.Credentials.Tests.Support;
using DTech.Credentials.Vault;
using FluentAssertions;

namespace DTech.Credentials.Tests.Vault;

[TestClass]
public sealed class EncryptedVaultTests
{
    private const string SecretText = "s3cr3t-value-that-must-never-leak";

    private VaultTestContext _context = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _context = new VaultTestContext();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [TestMethod]
    public async Task Unlock_WhenVaultDoesNotExist_CreatesVaultFile()
    {
        var vault = _context.CreateVault();
        var stateBefore = vault.State;

        var result = await UnlockWith(vault, VaultTestContext.MasterPassword);

        result.IsSuccess.Should().BeTrue();
        stateBefore.Should().Be(CredentialVaultState.NotCreated);
        vault.State.Should().Be(CredentialVaultState.Unlocked);
        File.Exists(_context.VaultPath).Should().BeTrue();
    }

    [TestMethod]
    public async Task Unlock_WithShortPasswordForNewVault_ReturnsWeakMasterPassword()
    {
        var vault = _context.CreateVault();

        var result = await UnlockWith(vault, "short");

        result.Error!.Code.Should().Be(CredentialErrorCode.WeakMasterPassword);
        File.Exists(_context.VaultPath).Should().BeFalse();
        vault.State.Should().Be(CredentialVaultState.NotCreated);
    }

    [TestMethod]
    public async Task StoreAndRead_AfterReopening_ReturnsSameSecret()
    {
        var key = TestKeys.Create("connections/1/password");
        await CreateVaultWithSecret(key, SecretText);
        var reopenedVault = _context.CreateVault();
        var reopenedState = reopenedVault.State;
        await UnlockWith(reopenedVault, VaultTestContext.MasterPassword);

        var result = await reopenedVault.Read(key, TestContext.CancellationToken);

        reopenedState.Should().Be(CredentialVaultState.Locked);
        result.IsSuccess.Should().BeTrue();
        using var secret = result.Value;
        secret.RevealString().Should().Be(SecretText);
    }

    [TestMethod]
    public async Task Store_WithExistingKey_ReplacesValue()
    {
        var key = TestKeys.Create("tokens/github");
        var vault = await CreateVaultWithSecret(key, "first-value");
        using var replacement = Secret.FromString("second-value");

        await vault.Store(key, replacement, TestContext.CancellationToken);
        var result = await vault.Read(key, TestContext.CancellationToken);

        using var secret = result.Value;
        secret.RevealString().Should().Be("second-value");
    }

    [TestMethod]
    public async Task Delete_WithExistingKey_RemovesSecret()
    {
        var key = TestKeys.Create("tokens/delete-me");
        var vault = await CreateVaultWithSecret(key, SecretText);

        var deleteResult = await vault.Delete(key, TestContext.CancellationToken);
        var readResult = await vault.Read(key, TestContext.CancellationToken);

        deleteResult.IsSuccess.Should().BeTrue();
        readResult.Error!.Code.Should().Be(CredentialErrorCode.NotFound);
    }

    [TestMethod]
    public async Task ReadAndDelete_WithMissingKey_ReturnNotFound()
    {
        var vault = _context.CreateVault();
        await UnlockWith(vault, VaultTestContext.MasterPassword);
        var key = TestKeys.Create("missing");

        var readResult = await vault.Read(key, TestContext.CancellationToken);
        var deleteResult = await vault.Delete(key, TestContext.CancellationToken);

        readResult.Error!.Code.Should().Be(CredentialErrorCode.NotFound);
        deleteResult.Error!.Code.Should().Be(CredentialErrorCode.NotFound);
    }

    [TestMethod]
    public async Task Operations_WhenLocked_ReturnVaultLocked()
    {
        var key = TestKeys.Create("tokens/locked");
        var vault = await CreateVaultWithSecret(key, SecretText);
        vault.Lock();
        using var secret = Secret.FromString(SecretText);

        var readResult = await vault.Read(key, TestContext.CancellationToken);
        var storeResult = await vault.Store(key, secret, TestContext.CancellationToken);
        var deleteResult = await vault.Delete(key, TestContext.CancellationToken);

        vault.State.Should().Be(CredentialVaultState.Locked);
        readResult.Error!.Code.Should().Be(CredentialErrorCode.VaultLocked);
        storeResult.Error!.Code.Should().Be(CredentialErrorCode.VaultLocked);
        deleteResult.Error!.Code.Should().Be(CredentialErrorCode.VaultLocked);
    }

    [TestMethod]
    public async Task Unlock_WithWrongPassword_ReturnsInvalidMasterPassword()
    {
        var key = TestKeys.Create("tokens/wrong-password");
        await CreateVaultWithSecret(key, SecretText);
        var reopenedVault = _context.CreateVault();

        var result = await UnlockWith(reopenedVault, "not the master password");

        result.Error!.Code.Should().Be(CredentialErrorCode.InvalidMasterPassword);
        reopenedVault.State.Should().Be(CredentialVaultState.Locked);
    }

    [TestMethod]
    public async Task Unlock_WithTamperedCiphertext_ReturnsVaultCorrupted()
    {
        var key = TestKeys.Create("tokens/tampered");
        await CreateVaultWithSecret(key, SecretText);
        _context.FlipBit(VaultFileFormat.HeaderLength + 1);
        var reopenedVault = _context.CreateVault();

        var result = await UnlockWith(reopenedVault, VaultTestContext.MasterPassword);

        result.Error!.Code.Should().Be(CredentialErrorCode.VaultCorrupted);
        reopenedVault.State.Should().Be(CredentialVaultState.Locked);
    }

    [TestMethod]
    public async Task Unlock_WithTamperedAuthenticationTag_ReturnsVaultCorrupted()
    {
        var key = TestKeys.Create("tokens/tampered-tag");
        await CreateVaultWithSecret(key, SecretText);
        var length = _context.ReadVaultBytes().Length;
        _context.FlipBit(length - 1);
        var reopenedVault = _context.CreateVault();

        var result = await UnlockWith(reopenedVault, VaultTestContext.MasterPassword);

        result.Error!.Code.Should().Be(CredentialErrorCode.VaultCorrupted);
    }

    [TestMethod]
    public async Task Unlock_WithTamperedHeaderNonce_ReturnsVaultCorrupted()
    {
        var key = TestKeys.Create("tokens/tampered-header");
        await CreateVaultWithSecret(key, SecretText);
        _context.FlipBit(VaultFileFormat.NonceOffset);
        var reopenedVault = _context.CreateVault();

        var result = await UnlockWith(reopenedVault, VaultTestContext.MasterPassword);

        result.Error!.Code.Should().Be(CredentialErrorCode.VaultCorrupted);
    }

    [TestMethod]
    public async Task Unlock_WithTamperedCiphertextLength_ReturnsVaultCorrupted()
    {
        var key = TestKeys.Create("tokens/tampered-length");
        await CreateVaultWithSecret(key, SecretText);
        _context.FlipBit(VaultFileFormat.CiphertextLengthOffset + 3);
        var reopenedVault = _context.CreateVault();

        var result = await UnlockWith(reopenedVault, VaultTestContext.MasterPassword);

        result.Error!.Code.Should().Be(CredentialErrorCode.VaultCorrupted);
    }

    [TestMethod]
    public async Task Unlock_WithTruncatedFile_ReturnsVaultCorrupted()
    {
        var key = TestKeys.Create("tokens/truncated");
        await CreateVaultWithSecret(key, SecretText);
        var bytes = _context.ReadVaultBytes();
        var truncated = bytes[..(bytes.Length - 5)];
        _context.WriteVaultBytes(truncated);
        var reopenedVault = _context.CreateVault();

        var result = await UnlockWith(reopenedVault, VaultTestContext.MasterPassword);

        result.Error!.Code.Should().Be(CredentialErrorCode.VaultCorrupted);
    }

    [TestMethod]
    public async Task Unlock_WithForeignFile_ReturnsVaultCorrupted()
    {
        var foreignContent = "this is not a vault"u8.ToArray();
        _context.WriteVaultBytes(foreignContent);
        var vault = _context.CreateVault();

        var result = await UnlockWith(vault, VaultTestContext.MasterPassword);

        result.Error!.Code.Should().Be(CredentialErrorCode.VaultCorrupted);
    }

    [TestMethod]
    public async Task Unlock_WithNewerFormatVersion_ReturnsUnsupportedVaultVersion()
    {
        var key = TestKeys.Create("tokens/version");
        await CreateVaultWithSecret(key, SecretText);
        var bytes = _context.ReadVaultBytes();
        var versionSpan = bytes.AsSpan(VaultFileFormat.VersionOffset);
        BinaryPrimitives.WriteUInt16BigEndian(versionSpan, 2);
        _context.WriteVaultBytes(bytes);
        var reopenedVault = _context.CreateVault();

        var result = await UnlockWith(reopenedVault, VaultTestContext.MasterPassword);

        result.Error!.Code.Should().Be(CredentialErrorCode.UnsupportedVaultVersion);
        result.Error.Message.Should().Contain("version 2");
    }

    [TestMethod]
    public async Task VaultFile_StartsWithVersionedHeader()
    {
        var key = TestKeys.Create("tokens/header");
        await CreateVaultWithSecret(key, SecretText);

        var bytes = _context.ReadVaultBytes();

        var span = bytes.AsSpan();
        var magic = Encoding.ASCII.GetString(bytes, 0, VaultFileFormat.Magic.Length);
        var version = BinaryPrimitives.ReadUInt16BigEndian(span[VaultFileFormat.VersionOffset..]);
        var iterations = BinaryPrimitives.ReadInt32BigEndian(span[VaultFileFormat.IterationsOffset..]);
        var ciphertextLength = BinaryPrimitives.ReadInt32BigEndian(span[VaultFileFormat.CiphertextLengthOffset..]);
        magic.Should().Be("TFVT");
        version.Should().Be(VaultFileFormat.CurrentVersion);
        bytes[VaultFileFormat.KdfAlgorithmOffset].Should().Be(VaultFileFormat.Pbkdf2HmacSha256);
        iterations.Should().Be(VaultTestContext.TestIterations);
        bytes.Length.Should().Be(VaultFileFormat.HeaderLength + ciphertextLength + VaultFileFormat.TagLength);
    }

    [TestMethod]
    public async Task Store_DoesNotWritePlaintextToDisk()
    {
        var key = TestKeys.Create("plaintext/marker-key");
        await CreateVaultWithSecret(key, SecretText);

        var bytes = _context.ReadVaultBytes();

        var secretBytes = Encoding.UTF8.GetBytes(SecretText);
        var keyBytes = Encoding.UTF8.GetBytes(key.Value);
        var passwordBytes = Encoding.UTF8.GetBytes(VaultTestContext.MasterPassword);
        bytes.AsSpan().IndexOf(secretBytes).Should().Be(-1);
        bytes.AsSpan().IndexOf(keyBytes).Should().Be(-1);
        bytes.AsSpan().IndexOf(passwordBytes).Should().Be(-1);
    }

    [TestMethod]
    public async Task Store_EachWriteUsesFreshNonce()
    {
        var key = TestKeys.Create("tokens/nonce");
        var vault = await CreateVaultWithSecret(key, SecretText);
        var firstNonce = _context.ReadVaultBytes()[VaultFileFormat.NonceOffset..VaultFileFormat.CiphertextLengthOffset];
        using var sameSecret = Secret.FromString(SecretText);

        await vault.Store(key, sameSecret, TestContext.CancellationToken);

        var secondNonce = _context.ReadVaultBytes()[VaultFileFormat.NonceOffset..VaultFileFormat.CiphertextLengthOffset];
        secondNonce.Should().NotEqual(firstNonce);
    }

    [TestMethod]
    public async Task Store_LeavesOnlyTheVaultFileInItsDirectory()
    {
        var key = TestKeys.Create("tokens/atomic");
        var vault = await CreateVaultWithSecret(key, SecretText);
        using var secret = Secret.FromString("another");

        await vault.Store(key, secret, TestContext.CancellationToken);

        var files = Directory.GetFiles(_context.DirectoryPath);
        files.Should().ContainSingle().Which.Should().Be(_context.VaultPath);
    }

    [TestMethod]
    public async Task VaultFile_IsReadableOnlyByOwner()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix file modes do not apply on Windows; the vault inherits the ACL of the user profile directory.");
        }

        var key = TestKeys.Create("tokens/mode");
        await CreateVaultWithSecret(key, SecretText);

        var mode = File.GetUnixFileMode(_context.VaultPath);

        mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [TestMethod]
    public async Task Read_AfterVaultFileWasReplaced_ReturnsVaultCorrupted()
    {
        var key = TestKeys.Create("tokens/replaced");
        var vault = await CreateVaultWithSecret(key, SecretText);
        using var otherContext = new VaultTestContext();
        var otherVault = otherContext.CreateVault();
        await UnlockWith(otherVault, VaultTestContext.MasterPassword);
        var otherBytes = File.ReadAllBytes(otherContext.VaultPath);
        _context.WriteVaultBytes(otherBytes);

        var result = await vault.Read(key, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(CredentialErrorCode.VaultCorrupted);
    }

    [TestMethod]
    public async Task VaultOperations_NeverLogSecretsOrMasterPassword()
    {
        var key = TestKeys.Create("tokens/logging");
        var vault = await CreateVaultWithSecret(key, SecretText);
        var reopenedVault = _context.CreateVault();
        await UnlockWith(reopenedVault, "wrong master password");
        var readResult = await vault.Read(key, TestContext.CancellationToken);
        using var secret = readResult.Value;

        var logText = _context.VaultLogger.AllText;

        _context.VaultLogger.Entries.Should().NotBeEmpty();
        logText.Should().NotContain(SecretText);
        logText.Should().NotContain(VaultTestContext.MasterPassword);
        logText.Should().NotContain("wrong master password");
    }

    private async Task<EncryptedVault> CreateVaultWithSecret(CredentialKey key, string secretText)
    {
        var vault = _context.CreateVault();
        var unlockResult = await UnlockWith(vault, VaultTestContext.MasterPassword);
        unlockResult.IsSuccess.Should().BeTrue();
        using var secret = Secret.FromString(secretText);
        var storeResult = await vault.Store(key, secret, TestContext.CancellationToken);
        storeResult.IsSuccess.Should().BeTrue();
        return vault;
    }

    private async Task<CredentialResult> UnlockWith(EncryptedVault vault, string password)
    {
        using var masterPassword = Secret.FromString(password);
        return await vault.Unlock(masterPassword, TestContext.CancellationToken);
    }
}
