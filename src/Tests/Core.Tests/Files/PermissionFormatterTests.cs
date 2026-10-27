using DTech.Core.Files;
using FluentAssertions;

namespace DTech.Core.Tests.Files;

[TestClass]
public sealed class PermissionFormatterTests
{
    [TestMethod]
    public void Format_FullAccess_ReturnsRwxForEveryone()
    {
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

        var text = PermissionFormatter.Format(mode);

        text.Should().Be("rwxrwxrwx");
    }

    [TestMethod]
    public void Format_TypicalFileMode_ReturnsRw_R_R()
    {
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

        var text = PermissionFormatter.Format(mode);

        text.Should().Be("rw-r--r--");
    }

    [TestMethod]
    public void Format_NoBits_ReturnsDashes()
    {
        var text = PermissionFormatter.Format(UnixFileMode.None);

        text.Should().Be("---------");
    }
}
