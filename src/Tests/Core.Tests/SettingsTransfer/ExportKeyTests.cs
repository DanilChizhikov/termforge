using DTech.Core.SettingsTransfer.Package;
using FluentAssertions;

namespace DTech.Core.Tests.SettingsTransfer;

[TestClass]
public sealed class ExportKeyTests
{
    [TestMethod]
    public void Generate_ReturnsEightGroupsOfFourSymbolsFromTheAlphabet()
    {
        var generator = new ExportKeyGenerator();

        var key = generator.Generate();

        var groups = key.Split(ExportKeyAlphabet.GroupSeparator);
        groups.Should().HaveCount(8).And.OnlyContain(group => group.Length == ExportKeyAlphabet.GroupLength);
        key.Replace("-", string.Empty, StringComparison.Ordinal).Should().MatchRegex("^[0-9ABCDEFGHJKMNPQRSTVWXYZ]{32}$");
    }

    [TestMethod]
    public void Generate_ReturnsADifferentKeyEveryTime()
    {
        var generator = new ExportKeyGenerator();

        var first = generator.Generate();
        var second = generator.Generate();

        first.Should().NotBe(second);
    }

    [TestMethod]
    public void Normalize_AGeneratedKey_RemovesTheSeparators()
    {
        var generator = new ExportKeyGenerator();
        var key = generator.Generate();

        var normalized = ExportKeyNormalizer.Normalize(key);

        var expected = key.Replace("-", string.Empty, StringComparison.Ordinal);
        normalized.Should().Be(expected);
    }

    [TestMethod]
    public void Normalize_AKeyTypedInLowerCaseWithSpacesAndLookAlikes_ReturnsTheCanonicalKey()
    {
        const string typed = " 7k3m 9qx2-hv4t pw8r-zb6d-nj5c-fgo5-ay1e ";

        var normalized = ExportKeyNormalizer.Normalize(typed);

        normalized.Should().Be("7K3M9QX2HV4TPW8RZB6DNJ5CFG05AY1E");
    }

    [TestMethod]
    public void Normalize_LookAlikeOneSymbols_MapToOne()
    {
        const string typed = "IL00-0000-0000-0000-0000-0000-0000-0000";

        var normalized = ExportKeyNormalizer.Normalize(typed);

        normalized.Should().Be("11000000000000000000000000000000");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("7K3M-9QX2")]
    [DataRow("7K3M-9QX2-HV4T-PW8R-ZB6D-NJ5C-FG05-AY1E-0000")]
    [DataRow("7K3M-9QX2-HV4T-PW8R-ZB6D-NJ5C-FG05-AY1U")]
    [DataRow("7K3M-9QX2-HV4T-PW8R-ZB6D-NJ5C-FG05-AY1!")]
    public void Normalize_AMalformedKey_ReturnsNull(string? typed)
    {
        var normalized = ExportKeyNormalizer.Normalize(typed);

        normalized.Should().BeNull();
    }
}
