using DTech.Core.Connections;
using DTech.HostKeys.KnownHosts;
using DTech.HostKeys.Tests.Support;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DTech.HostKeys.Tests;

[TestClass]
public sealed class HostKeyVerifierTests
{
    private static readonly SshEndpoint Endpoint = new("server.example", 2222);

    private TemporaryDirectory _directory = null!;
    private string _knownHostsPath = null!;
    private ITrustedHostKeyStore _trustedStore = null!;
    private HostKeyVerifier _verifier = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = new TemporaryDirectory();
        _knownHostsPath = _directory.Combine("known_hosts");
        _trustedStore = Substitute.For<ITrustedHostKeyStore>();
        _trustedStore.GetTrustedHostKeys(default!).ReturnsForAnyArgs([]);
        var options = new HostKeyVerificationOptions
        {
            KnownHostsFiles = [_knownHostsPath],
        };
        var wrappedOptions = Options.Create(options);
        var files = new KnownHostsFiles(wrappedOptions);
        var reader = new KnownHostsReader(files, NullLogger<KnownHostsReader>.Instance);
        _verifier = new HostKeyVerifier(reader, _trustedStore);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _directory.Dispose();
    }

    [TestMethod]
    public void Verify_KeyInKnownHosts_IsTrusted()
    {
        var publicKey = TestPublicKey.CreateEd25519();
        WriteKnownHosts(publicKey.ToKnownHostsLine("[server.example]:2222"));

        var verification = Verify(publicKey.HostKey);

        verification.Status.Should().Be(HostKeyStatus.Trusted);
    }

    [TestMethod]
    public void Verify_KeyInHashedKnownHostsEntry_IsTrusted()
    {
        var publicKey = TestPublicKey.CreateEd25519();
        var hashedHost = HashedHostName.Create("[server.example]:2222");
        WriteKnownHosts("# hashed by ssh-keygen -H", publicKey.ToKnownHostsLine(hashedHost));

        var verification = Verify(publicKey.HostKey);

        verification.Status.Should().Be(HostKeyStatus.Trusted);
    }

    [TestMethod]
    public void Verify_NoEntriesForHost_IsUnknown()
    {
        var otherHostKey = TestPublicKey.CreateEd25519();
        WriteKnownHosts(otherHostKey.ToKnownHostsLine("other.example"));
        var presentedKey = TestPublicKey.CreateEd25519();

        var verification = Verify(presentedKey.HostKey);

        verification.Status.Should().Be(HostKeyStatus.Unknown);
        verification.KnownHostKeys.Should().BeEmpty();
    }

    [TestMethod]
    public void Verify_MissingKnownHostsFile_IsUnknown()
    {
        var presentedKey = TestPublicKey.CreateEd25519();

        var verification = Verify(presentedKey.HostKey);

        verification.Status.Should().Be(HostKeyStatus.Unknown);
    }

    [TestMethod]
    public void Verify_DifferentKeyOfSameAlgorithm_IsChangedAndReportsKnownKey()
    {
        var knownKey = TestPublicKey.CreateEd25519();
        WriteKnownHosts(knownKey.ToKnownHostsLine("[server.example]:2222"));
        var presentedKey = TestPublicKey.CreateEd25519();

        var verification = Verify(presentedKey.HostKey);

        verification.Status.Should().Be(HostKeyStatus.Changed);
        verification.KnownHostKeys.Should().Equal(knownKey.HostKey);
    }

    [TestMethod]
    public void Verify_OnlyKeysOfOtherAlgorithmsKnown_IsUnknownAndReportsThem()
    {
        var knownRsaKey = TestPublicKey.CreateRsa();
        WriteKnownHosts(knownRsaKey.ToKnownHostsLine("[server.example]:2222"));
        var presentedKey = TestPublicKey.CreateEd25519();

        var verification = Verify(presentedKey.HostKey);

        verification.Status.Should().Be(HostKeyStatus.Unknown);
        verification.KnownHostKeys.Should().Equal(knownRsaKey.HostKey);
    }

    [TestMethod]
    public void Verify_KeyMarkedRevoked_IsRevokedEvenWhenAlsoListed()
    {
        var publicKey = TestPublicKey.CreateEd25519();
        WriteKnownHosts(publicKey.ToKnownHostsLine("[server.example]:2222"), $"@revoked {publicKey.ToKnownHostsLine("*")}");

        var verification = Verify(publicKey.HostKey);

        verification.Status.Should().Be(HostKeyStatus.Revoked);
    }

    [TestMethod]
    public void Verify_RevokedLineForOtherKey_DoesNotAffectPresentedKey()
    {
        var publicKey = TestPublicKey.CreateEd25519();
        var revokedKey = TestPublicKey.CreateEd25519();
        WriteKnownHosts(publicKey.ToKnownHostsLine("[server.example]:2222"), $"@revoked {revokedKey.ToKnownHostsLine("*")}");

        var verification = Verify(publicKey.HostKey);

        verification.Status.Should().Be(HostKeyStatus.Trusted);
    }

    [TestMethod]
    public void Verify_CertificateAuthorityLine_IsIgnored()
    {
        var authorityKey = TestPublicKey.CreateEd25519();
        WriteKnownHosts($"@cert-authority {authorityKey.ToKnownHostsLine("*")}");

        var verification = Verify(authorityKey.HostKey);

        verification.Status.Should().Be(HostKeyStatus.Unknown);
        verification.KnownHostKeys.Should().BeEmpty();
    }

    [TestMethod]
    public void Verify_KeyInAppTrustedStore_IsTrusted()
    {
        var publicKey = TestPublicKey.CreateEd25519();
        _trustedStore.GetTrustedHostKeys(Endpoint).Returns([publicKey.HostKey]);

        var verification = Verify(publicKey.HostKey);

        verification.Status.Should().Be(HostKeyStatus.Trusted);
    }

    [TestMethod]
    public void Verify_AppStoreReplacementOfKnownHostsKey_IsTrusted()
    {
        var oldKey = TestPublicKey.CreateEd25519();
        var newKey = TestPublicKey.CreateEd25519();
        WriteKnownHosts(oldKey.ToKnownHostsLine("[server.example]:2222"));
        _trustedStore.GetTrustedHostKeys(Endpoint).Returns([newKey.HostKey]);

        var verification = Verify(newKey.HostKey);

        verification.Status.Should().Be(HostKeyStatus.Trusted);
    }

    [TestMethod]
    public void Verify_MalformedLines_AreSkipped()
    {
        var publicKey = TestPublicKey.CreateEd25519();
        WriteKnownHosts("garbage", "[server.example]:2222 ssh-ed25519 ###", publicKey.ToKnownHostsLine("[server.example]:2222"));

        var verification = Verify(publicKey.HostKey);

        verification.Status.Should().Be(HostKeyStatus.Trusted);
    }

    [TestMethod]
    public void Verify_DoesNotModifyKnownHostsFile()
    {
        var knownKey = TestPublicKey.CreateEd25519();
        WriteKnownHosts(knownKey.ToKnownHostsLine("[server.example]:2222"));
        var contentBefore = File.ReadAllText(_knownHostsPath);
        var presentedKey = TestPublicKey.CreateEd25519();

        Verify(presentedKey.HostKey);

        File.ReadAllText(_knownHostsPath).Should().Be(contentBefore);
    }

    private HostKeyVerification Verify(HostKey hostKey)
    {
        var request = new HostKeyVerificationRequest(Endpoint, hostKey);
        return _verifier.Verify(request);
    }

    private void WriteKnownHosts(params string[] lines)
    {
        File.WriteAllLines(_knownHostsPath, lines);
    }
}
