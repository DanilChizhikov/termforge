using DTech.Core.Files;
using DTech.Core.Files.Search;
using FluentAssertions;

namespace DTech.Core.Tests.Files.Search;

[TestClass]
public sealed class FileNamePatternTests
{
    [TestMethod]
    [DataRow("log", "app.log", true)]
    [DataRow("LOG", "app.log", true)]
    [DataRow("log", "readme.txt", false)]
    [DataRow("*.log", "app.log", true)]
    [DataRow("*.log", "app.log.gz", false)]
    [DataRow("a?c", "abc", true)]
    [DataRow("a?c", "abbc", false)]
    [DataRow("*a*b*", "xxaYYbzz", true)]
    [DataRow("[x]", "[x].txt", true)]
    [DataRow("[x]", "x.txt", false)]
    public void IsMatch_UsesCaseInsensitiveGlobWithSubstringDefault(string pattern, string name, bool expected)
    {
        var created = FileNamePattern.Create(pattern);

        created.Value.IsMatch(name).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("etc/passwd")]
    [DataRow("..\\x")]
    [DataRow("line\nbreak")]
    [DataRow("nul\0byte")]
    public void Create_InvalidPattern_IsRejected(string pattern)
    {
        var created = FileNamePattern.Create(pattern);

        created.IsSuccess.Should().BeFalse();
        created.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    public void Create_TooLongPattern_IsRejected()
    {
        var pattern = new string('a', FileNamePattern.MaxLength + 1);

        var created = FileNamePattern.Create(pattern);

        created.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public void ToFindGlob_EscapesBracketsAndKeepsWildcards()
    {
        var created = FileNamePattern.Create("[ab]*.log");

        created.Value.ToFindGlob().Should().Be("\\[ab\\]*.log");
    }

    [TestMethod]
    public void ToFindGlob_WithoutWildcard_WrapsInAnySequence()
    {
        var created = FileNamePattern.Create("nginx");

        created.Value.ToFindGlob().Should().Be("*nginx*");
    }
}
