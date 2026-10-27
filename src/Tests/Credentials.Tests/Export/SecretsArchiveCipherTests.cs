using System.Buffers.Binary;
using System.Text;
using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Secrets;
using DTech.Credentials.Export;
using DTech.Credentials.Vault;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Tests.Export;

[TestClass]
public sealed class SecretsArchiveCipherTests
{
    private const string ArchivePassword = "correct-horse-staple";
    private const string ServerPassword = "server-password-that-must-never-leak";
    private const string SudoPassword = "sudo-password-that-must-never-leak";
    private const int TestIterations = 1000;

    private readonly Guid _exportId = Guid.NewGuid();
    private readonly Guid _connectionId = Guid.NewGuid();
    private SecretsArchiveCipher _cipher = null!;

    [TestInitialize]
    public void Initialize()
    {
        var options = new CredentialStoreOptions { VaultKdfIterations = TestIterations };
        var wrapped = Options.Create(options);
        _cipher = new SecretsArchiveCipher(wrapped);
    }

    [TestMethod]
    public void Encrypt_ThenDecrypt_RoundTripsTheSecretsAndTheExportId()
    {
        var archive = EncryptSample();

        using var password = Secret.FromString(ArchivePassword);
        var decrypted = _cipher.Decrypt(archive, password);

        decrypted.IsSuccess.Should().BeTrue();
        using var content = decrypted.Value;
        content.ExportId.Should().Be(_exportId);
        content.Find(_connectionId, ArchivedSecretKind.Password)!.Value.RevealString().Should().Be(ServerPassword);
        content.Find(_connectionId, ArchivedSecretKind.SudoPassword)!.Value.RevealString().Should().Be(SudoPassword);
        content.Secrets.Should().HaveCount(2);
    }

    [TestMethod]
    public void Encrypt_NeverWritesThePlaintextSecrets()
    {
        var archive = EncryptSample();

        var serverPasswordBytes = Encoding.UTF8.GetBytes(ServerPassword);
        var sudoPasswordBytes = Encoding.UTF8.GetBytes(SudoPassword);
        var archivePasswordBytes = Encoding.UTF8.GetBytes(ArchivePassword);
        archive.AsSpan().IndexOf(serverPasswordBytes).Should().Be(-1);
        archive.AsSpan().IndexOf(sudoPasswordBytes).Should().Be(-1);
        archive.AsSpan().IndexOf(archivePasswordBytes).Should().Be(-1);
    }

    [TestMethod]
    public void Decrypt_WithAWrongPassword_ReportsWrongPassword()
    {
        var archive = EncryptSample();

        using var wrongPassword = Secret.FromString("not-the-archive-password");
        var decrypted = _cipher.Decrypt(archive, wrongPassword);

        decrypted.IsSuccess.Should().BeFalse();
        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.WrongPassword);
    }

    [TestMethod]
    public void Decrypt_WithATamperedCiphertext_ReportsADamagedArchive()
    {
        var archive = EncryptSample();
        archive[VaultFileFormat.HeaderLength + 3] ^= 0x01;

        var decrypted = DecryptWithArchivePassword(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.ArchiveDamaged);
    }

    [TestMethod]
    public void Decrypt_WithATamperedNonce_ReportsADamagedArchive()
    {
        var archive = EncryptSample();
        archive[VaultFileFormat.NonceOffset] ^= 0x01;

        var decrypted = DecryptWithArchivePassword(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.ArchiveDamaged);
    }

    [TestMethod]
    public void Decrypt_WithATamperedTag_ReportsADamagedArchive()
    {
        var archive = EncryptSample();
        archive[^1] ^= 0x01;

        var decrypted = DecryptWithArchivePassword(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.ArchiveDamaged);
    }

    [TestMethod]
    public void Decrypt_ATruncatedArchive_ReportsADamagedArchive()
    {
        var archive = EncryptSample();
        var truncated = archive[..(archive.Length - 5)];

        var decrypted = DecryptWithArchivePassword(truncated);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.ArchiveDamaged);
    }

    [TestMethod]
    public void Decrypt_AFileThatIsNotAnArchive_ReportsADamagedArchive()
    {
        var notAnArchive = Encoding.UTF8.GetBytes("{\"format\":\"termforge-settings\"}");

        var decrypted = DecryptWithArchivePassword(notAnArchive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.ArchiveDamaged);
    }

    [TestMethod]
    public void Decrypt_WithAnExcessiveIterationCount_IsRejectedBeforeDerivingTheKey()
    {
        var archive = EncryptSample();
        var iterations = archive.AsSpan(VaultFileFormat.IterationsOffset);
        BinaryPrimitives.WriteInt32BigEndian(iterations, int.MaxValue);

        var decrypted = DecryptWithArchivePassword(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.ArchiveDamaged);
    }

    [TestMethod]
    public void Decrypt_ANewerArchiveVersion_ReportsAnUnsupportedVersion()
    {
        var archive = EncryptSample();
        var version = archive.AsSpan(VaultFileFormat.VersionOffset);
        BinaryPrimitives.WriteUInt16BigEndian(version, 9);

        var decrypted = DecryptWithArchivePassword(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.UnsupportedVersion);
    }

    [TestMethod]
    public void Decrypt_AVaultWithoutAnExportId_ReportsADamagedArchive()
    {
        using var entries = new VaultEntries();
        var value = Encoding.UTF8.GetBytes(ServerPassword);
        entries.Set($"connection:{_connectionId:D}:password", value);
        var archive = EncryptEntries(entries);

        var decrypted = DecryptWithArchivePassword(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.ArchiveDamaged);
    }

    [TestMethod]
    public void Decrypt_AnArchiveWithAnUnknownEntry_ReportsADamagedArchive()
    {
        using var entries = new VaultEntries();
        var exportIdBytes = _exportId.ToByteArray();
        var value = Encoding.UTF8.GetBytes(ServerPassword);
        entries.Set(SecretsArchiveCipher.ExportIdEntryKey, exportIdBytes);
        entries.Set("network:password", value);
        var archive = EncryptEntries(entries);

        var decrypted = DecryptWithArchivePassword(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.ArchiveDamaged);
    }

    private byte[] EncryptSample()
    {
        var serverPassword = Secret.FromString(ServerPassword);
        var sudoPassword = Secret.FromString(SudoPassword);
        var passwordSecret = new ArchivedSecret(_connectionId, ArchivedSecretKind.Password, serverPassword);
        var sudoSecret = new ArchivedSecret(_connectionId, ArchivedSecretKind.SudoPassword, sudoPassword);
        ArchivedSecret[] secrets = [passwordSecret, sudoSecret];
        using var content = new SecretsArchiveContent(_exportId, secrets);
        using var password = Secret.FromString(ArchivePassword);
        return _cipher.Encrypt(content, password);
    }

    private static byte[] EncryptEntries(VaultEntries entries)
    {
        var plaintext = VaultEntriesSerializer.Serialize(entries);
        var kdfParameters = VaultKeyDerivation.CreateParameters(TestIterations);
        var passwordBytes = Encoding.UTF8.GetBytes(ArchivePassword);
        using var keys = VaultKeyDerivation.Derive(passwordBytes, kdfParameters);
        return VaultCipher.Encrypt(keys, plaintext);
    }

    private SettingsTransferResult<SecretsArchiveContent> DecryptWithArchivePassword(byte[] archive)
    {
        using var password = Secret.FromString(ArchivePassword);
        var decrypted = _cipher.Decrypt(archive, password);
        decrypted.IsSuccess.Should().BeFalse();
        return decrypted;
    }
}
