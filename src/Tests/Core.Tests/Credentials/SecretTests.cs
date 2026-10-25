using System.Text.Json;
using DTech.Core.Credentials;
using FluentAssertions;

namespace DTech.Core.Tests.Credentials;

[TestClass]
public sealed class SecretTests
{
    private const string SecretText = "p@ssw0rd-that-must-not-leak";

    [TestMethod]
    public void ToString_NeverRevealsValue()
    {
        using var secret = Secret.FromString(SecretText);

        var text = secret.ToString();
        var interpolated = $"value: {secret}";

        text.Should().Be(Secret.Redacted);
        interpolated.Should().NotContain(SecretText);
    }

    [TestMethod]
    public void JsonSerialization_DoesNotRevealValue()
    {
        using var secret = Secret.FromString(SecretText);

        var json = JsonSerializer.Serialize(secret);

        json.Should().NotContain(SecretText);
    }

    [TestMethod]
    public void FailedResultContainingSecret_DoesNotRevealValue()
    {
        using var secret = Secret.FromString(SecretText);
        var result = CredentialResult<Secret>.Success(secret);

        var text = $"{result} {result.Value}";

        text.Should().NotContain(SecretText);
    }

    [TestMethod]
    public void RevealString_ReturnsOriginalValue()
    {
        using var secret = Secret.FromString(SecretText);

        var revealed = secret.RevealString();

        revealed.Should().Be(SecretText);
    }

    [TestMethod]
    public void FromBytes_CopiesInput()
    {
        var bytes = new byte[] { 1, 2, 3 };
        using var secret = Secret.FromBytes(bytes);

        bytes[0] = 9;

        secret.AsSpan().ToArray().Should().Equal(1, 2, 3);
    }

    [TestMethod]
    public void Dispose_PreventsFurtherAccess()
    {
        var secret = Secret.FromString(SecretText);

        secret.Dispose();
        var reveal = () => secret.RevealString();

        reveal.Should().Throw<ObjectDisposedException>().Which.Message.Should().NotContain(SecretText);
    }

    [TestMethod]
    public void ContentEquals_ComparesValues()
    {
        using var secret = Secret.FromString(SecretText);
        using var same = Secret.FromString(SecretText);
        using var other = Secret.FromString("other");

        var equalsSame = secret.ContentEquals(same);
        var equalsOther = secret.ContentEquals(other);

        equalsSame.Should().BeTrue();
        equalsOther.Should().BeFalse();
    }
}
