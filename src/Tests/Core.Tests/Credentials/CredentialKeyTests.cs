using DTech.Core.Credentials;
using FluentAssertions;

namespace DTech.Core.Tests.Credentials;

[TestClass]
public sealed class CredentialKeyTests
{
    [TestMethod]
    [DataRow("connections/42/password")]
    [DataRow("user@host:22")]
    [DataRow("token.github_main-1")]
    public void Create_WithAllowedCharacters_Succeeds(string value)
    {
        var result = CredentialKey.Create(value);

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(value);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("has space")]
    [DataRow("new\nline")]
    [DataRow("quote\"")]
    [DataRow("unicode-é")]
    public void Create_WithInvalidValue_ReturnsInvalidKey(string? value)
    {
        var result = CredentialKey.Create(value);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(CredentialErrorCode.InvalidKey);
    }

    [TestMethod]
    public void Create_WithTooLongValue_ReturnsInvalidKey()
    {
        var value = new string('a', CredentialKey.MaxLength + 1);

        var result = CredentialKey.Create(value);

        result.Error!.Code.Should().Be(CredentialErrorCode.InvalidKey);
    }

    [TestMethod]
    public void Value_OfFailedResult_Throws()
    {
        var result = CredentialKey.Create(string.Empty);

        var access = () => result.Value;

        access.Should().Throw<InvalidOperationException>();
    }
}
