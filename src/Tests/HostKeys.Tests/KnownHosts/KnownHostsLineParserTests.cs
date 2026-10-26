using DTech.HostKeys.KnownHosts;
using DTech.HostKeys.Tests.Support;
using FluentAssertions;

namespace DTech.HostKeys.Tests.KnownHosts;

[TestClass]
public sealed class KnownHostsLineParserTests
{
    [TestMethod]
    public void Parse_PlainLine_ReturnsEntryWithFingerprint()
    {
        var publicKey = TestPublicKey.CreateEd25519();
        var line = $"{publicKey.ToKnownHostsLine("example.com,10.0.0.1")} user@laptop";

        var entry = KnownHostsLineParser.Parse(line);

        entry.Should().NotBeNull();
        entry!.Marker.Should().Be(KnownHostsMarker.None);
        entry.HostPatterns.Should().Be("example.com,10.0.0.1");
        entry.HostKey.Should().Be(publicKey.HostKey);
        entry.HostKey.Algorithm.Should().Be("ssh-ed25519");
        entry.HostKey.Sha256Fingerprint.Should().StartWith("SHA256:");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("# a comment")]
    [DataRow("   # indented comment")]
    public void Parse_BlankOrCommentLine_ReturnsNull(string line)
    {
        KnownHostsLineParser.Parse(line).Should().BeNull();
    }

    [TestMethod]
    [DataRow("@revoked", "Revoked")]
    [DataRow("@cert-authority", "CertificateAuthority")]
    public void Parse_LineWithMarker_ReturnsMarker(string marker, string expectedMarkerName)
    {
        var expectedMarker = Enum.Parse<KnownHostsMarker>(expectedMarkerName);
        var publicKey = TestPublicKey.CreateEd25519();
        var line = $"{marker} {publicKey.ToKnownHostsLine("*")}";

        var entry = KnownHostsLineParser.Parse(line);

        entry!.Marker.Should().Be(expectedMarker);
        entry.HostKey.Should().Be(publicKey.HostKey);
    }

    [TestMethod]
    public void Parse_UnknownMarker_ReturnsNull()
    {
        var publicKey = TestPublicKey.CreateEd25519();
        var line = $"@unknown {publicKey.ToKnownHostsLine("*")}";

        KnownHostsLineParser.Parse(line).Should().BeNull();
    }

    [TestMethod]
    public void Parse_MalformedBase64_ReturnsNull()
    {
        KnownHostsLineParser.Parse("example.com ssh-ed25519 not*base64").Should().BeNull();
    }

    [TestMethod]
    public void Parse_KeyTypeDifferentFromBlob_ReturnsNull()
    {
        var publicKey = TestPublicKey.CreateEd25519();
        var line = $"example.com ssh-rsa {publicKey.EncodedBlob}";

        KnownHostsLineParser.Parse(line).Should().BeNull();
    }

    [TestMethod]
    public void Parse_MissingKey_ReturnsNull()
    {
        KnownHostsLineParser.Parse("example.com ssh-ed25519").Should().BeNull();
    }

    [TestMethod]
    public void Parse_TabSeparatedFields_ReturnsEntry()
    {
        var publicKey = TestPublicKey.CreateRsa();
        var line = $"example.com\t{publicKey.Algorithm}\t{publicKey.EncodedBlob}";

        var entry = KnownHostsLineParser.Parse(line);

        entry!.HostKey.Should().Be(publicKey.HostKey);
    }
}
