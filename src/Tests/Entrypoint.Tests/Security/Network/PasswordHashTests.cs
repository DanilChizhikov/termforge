using DTech.Entrypoint.Security.Network;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Security.Network;

[TestClass]
public sealed class PasswordHashTests
{
    private const string Password = "correct horse battery staple";

    [TestMethod]
    public void Created_hash_verifies_the_same_password_only()
    {
        var hashText = PasswordHash.Create(Password, PasswordHash.MinimumIterations);

        var isParsed = PasswordHash.TryParse(hashText, out var passwordHash);

        isParsed.Should().BeTrue();
        passwordHash!.Verify(Password).Should().BeTrue();
        passwordHash.Verify("correct horse battery stapler").Should().BeFalse();
        passwordHash.Verify(string.Empty).Should().BeFalse();
        passwordHash.Verify(null).Should().BeFalse();
    }

    [TestMethod]
    public void Created_hash_uses_pbkdf2_format_with_random_salt_and_no_plain_password()
    {
        var firstHash = PasswordHash.Create(Password, PasswordHash.MinimumIterations);
        var secondHash = PasswordHash.Create(Password, PasswordHash.MinimumIterations);

        firstHash.Should().StartWith("pbkdf2-sha256$100000$");
        firstHash.Should().NotContain(Password);
        firstHash.Should().NotBe(secondHash);
    }

    [TestMethod]
    public void Default_iteration_count_is_strong()
    {
        PasswordHash.DefaultIterations.Should().BeGreaterThanOrEqualTo(600_000);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("plain-password")]
    [DataRow("sha256$100000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [DataRow("pbkdf2-sha256$1000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [DataRow("pbkdf2-sha256$abc$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [DataRow("pbkdf2-sha256$100000$not-base64$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [DataRow("pbkdf2-sha256$100000$AAAAAAAAAAAAAAAAAAAAAA==$AAAA")]
    [DataRow("pbkdf2-sha256$100000$AAAA$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void Malformed_or_weak_hash_is_rejected(string? hashText)
    {
        var isParsed = PasswordHash.TryParse(hashText, out var passwordHash);

        isParsed.Should().BeFalse();
        passwordHash.Should().BeNull();
    }
}
