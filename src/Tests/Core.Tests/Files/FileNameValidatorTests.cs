using DTech.Core.Files;
using FluentAssertions;

namespace DTech.Core.Tests.Files;

[TestClass]
public sealed class FileNameValidatorTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(".")]
    [DataRow("..")]
    [DataRow("a/b")]
    [DataRow("a\\b")]
    [DataRow("../escape")]
    [DataRow("bad\0name")]
    [DataRow("line\nbreak")]
    public void Validate_RejectsUnsafeNames(string? name)
    {
        var error = FileNameValidator.Validate(name);

        error.Should().NotBeNull();
        error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    public void Validate_RejectsTooLongName()
    {
        var name = new string('a', 256);

        var error = FileNameValidator.Validate(name);

        error.Should().NotBeNull();
    }

    [TestMethod]
    [DataRow("notes.txt")]
    [DataRow(".hidden")]
    [DataRow("with space")]
    [DataRow("...")]
    public void Validate_AcceptsOrdinaryNames(string name)
    {
        var error = FileNameValidator.Validate(name);

        error.Should().BeNull();
    }
}
