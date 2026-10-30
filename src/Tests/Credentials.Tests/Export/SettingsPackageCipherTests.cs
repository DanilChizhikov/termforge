using System.Buffers.Binary;
using System.Text;
using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer;
using DTech.Core.SettingsTransfer.Package;
using DTech.Core.SettingsTransfer.Secrets;
using DTech.Credentials.Export;
using DTech.Credentials.Vault;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Tests.Export;

[TestClass]
public sealed class SettingsPackageCipherTests
{
    private const string ExportKey = "7K3M9QX2HV4TPW8RZB6DNJ5CFG0SAY1E";
    private const string SettingsJson = "{\"format\":\"termforge-settings\"}";
    private const string ServerPassword = "server-password-that-must-never-leak";
    private const string SudoPassword = "sudo-password-that-must-never-leak";
    private const int TestIterations = 1000;

    private readonly Guid _exportId = Guid.NewGuid();
    private readonly Guid _connectionId = Guid.NewGuid();
    private SettingsPackageCipher _cipher = null!;

    [TestInitialize]
    public void Initialize()
    {
        var options = new CredentialStoreOptions { VaultKdfIterations = TestIterations };
        var wrapped = Options.Create(options);
        _cipher = new SettingsPackageCipher(wrapped);
    }

    [TestMethod]
    public void Encrypt_ThenDecrypt_RoundTripsTheSettingsTheSecretsAndTheExportId()
    {
        var archive = EncryptSample();

        using var key = Secret.FromString(ExportKey);
        var decrypted = _cipher.Decrypt(archive, key);

        decrypted.IsSuccess.Should().BeTrue();
        using var package = decrypted.Value;
        package.SettingsJson.Should().Be(SettingsJson);
        var content = package.Secrets;
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
        var exportKeyBytes = Encoding.UTF8.GetBytes(ExportKey);
        archive.AsSpan().IndexOf(serverPasswordBytes).Should().Be(-1);
        archive.AsSpan().IndexOf(sudoPasswordBytes).Should().Be(-1);
        archive.AsSpan().IndexOf(exportKeyBytes).Should().Be(-1);
    }

    [TestMethod]
    public void Decrypt_WithAWrongKey_ReportsWrongKey()
    {
        var archive = EncryptSample();

        using var wrongKey = Secret.FromString("00000000000000000000000000000000");
        var decrypted = _cipher.Decrypt(archive, wrongKey);

        decrypted.IsSuccess.Should().BeFalse();
        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.WrongKey);
    }

    [TestMethod]
    public void Decrypt_WithATamperedCiphertext_ReportsADamagedArchive()
    {
        var archive = EncryptSample();
        archive[VaultFileFormat.HeaderLength + 3] ^= 0x01;

        var decrypted = DecryptWithExportKey(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.FileDamaged);
    }

    [TestMethod]
    public void Decrypt_WithATamperedNonce_ReportsADamagedArchive()
    {
        var archive = EncryptSample();
        archive[VaultFileFormat.NonceOffset] ^= 0x01;

        var decrypted = DecryptWithExportKey(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.FileDamaged);
    }

    [TestMethod]
    public void Decrypt_WithATamperedTag_ReportsADamagedArchive()
    {
        var archive = EncryptSample();
        archive[^1] ^= 0x01;

        var decrypted = DecryptWithExportKey(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.FileDamaged);
    }

    [TestMethod]
    public void Decrypt_ATruncatedArchive_ReportsADamagedArchive()
    {
        var archive = EncryptSample();
        var truncated = archive[..(archive.Length - 5)];

        var decrypted = DecryptWithExportKey(truncated);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.FileDamaged);
    }

    [TestMethod]
    public void Decrypt_AFileThatIsNotAnArchive_ReportsADamagedArchive()
    {
        var notAnArchive = Encoding.UTF8.GetBytes("{\"format\":\"termforge-settings\"}");

        var decrypted = DecryptWithExportKey(notAnArchive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.FileDamaged);
    }

    [TestMethod]
    public void Decrypt_WithAnExcessiveIterationCount_IsRejectedBeforeDerivingTheKey()
    {
        var archive = EncryptSample();
        var iterations = archive.AsSpan(VaultFileFormat.IterationsOffset);
        BinaryPrimitives.WriteInt32BigEndian(iterations, int.MaxValue);

        var decrypted = DecryptWithExportKey(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.FileDamaged);
    }

    [TestMethod]
    public void Decrypt_ANewerArchiveVersion_ReportsAnUnsupportedVersion()
    {
        var archive = EncryptSample();
        var version = archive.AsSpan(VaultFileFormat.VersionOffset);
        BinaryPrimitives.WriteUInt16BigEndian(version, 9);

        var decrypted = DecryptWithExportKey(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.UnsupportedVersion);
    }

    [TestMethod]
    public void Decrypt_AVaultWithoutAnExportId_ReportsADamagedArchive()
    {
        using var entries = new VaultEntries();
        var value = Encoding.UTF8.GetBytes(ServerPassword);
        var settings = Encoding.UTF8.GetBytes(SettingsJson);
        entries.Set(SettingsPackageCipher.SettingsEntryKey, settings);
        entries.Set($"connection:{_connectionId:D}:password", value);
        var archive = EncryptEntries(entries);

        var decrypted = DecryptWithExportKey(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.FileDamaged);
    }

    [TestMethod]
    public void Decrypt_AnArchiveWithAnUnknownEntry_ReportsADamagedArchive()
    {
        using var entries = new VaultEntries();
        var exportIdBytes = _exportId.ToByteArray();
        var value = Encoding.UTF8.GetBytes(ServerPassword);
        var settings = Encoding.UTF8.GetBytes(SettingsJson);
        entries.Set(SettingsPackageCipher.ExportIdEntryKey, exportIdBytes);
        entries.Set(SettingsPackageCipher.SettingsEntryKey, settings);
        entries.Set("network:password", value);
        var archive = EncryptEntries(entries);

        var decrypted = DecryptWithExportKey(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.FileDamaged);
    }

    [TestMethod]
    public void Decrypt_AVaultWithoutSettings_ReportsADamagedArchive()
    {
        using var entries = new VaultEntries();
        var exportIdBytes = _exportId.ToByteArray();
        entries.Set(SettingsPackageCipher.ExportIdEntryKey, exportIdBytes);
        var archive = EncryptEntries(entries);

        var decrypted = DecryptWithExportKey(archive);

        decrypted.Error!.Code.Should().Be(SettingsTransferErrorCode.FileDamaged);
    }

    private byte[] EncryptSample()
    {
        var serverPassword = Secret.FromString(ServerPassword);
        var sudoPassword = Secret.FromString(SudoPassword);
        var passwordSecret = new ArchivedSecret(_connectionId, ArchivedSecretKind.Password, serverPassword);
        var sudoSecret = new ArchivedSecret(_connectionId, ArchivedSecretKind.SudoPassword, sudoPassword);
        ArchivedSecret[] secrets = [passwordSecret, sudoSecret];
        var content = new SecretsArchiveContent(_exportId, secrets);
        using var package = new SettingsPackage(SettingsJson, content);
        using var key = Secret.FromString(ExportKey);
        return _cipher.Encrypt(package, key);
    }

    private static byte[] EncryptEntries(VaultEntries entries)
    {
        var plaintext = VaultEntriesSerializer.Serialize(entries);
        var kdfParameters = VaultKeyDerivation.CreateParameters(TestIterations);
        var passwordBytes = Encoding.UTF8.GetBytes(ExportKey);
        using var keys = VaultKeyDerivation.Derive(passwordBytes, kdfParameters);
        return VaultCipher.Encrypt(keys, plaintext);
    }

    private SettingsTransferResult<SettingsPackage> DecryptWithExportKey(byte[] archive)
    {
        using var key = Secret.FromString(ExportKey);
        var decrypted = _cipher.Decrypt(archive, key);
        decrypted.IsSuccess.Should().BeFalse();
        return decrypted;
    }
}
