using DTech.Core.Diagnostics.Parsing;
using DTech.Core.Tests.Diagnostics.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Diagnostics;

[TestClass]
public sealed class SystemInfoParserTests
{
    [TestMethod]
    public void Parse_GlibcUbuntu_ReadsDistributionKernelAndShell()
    {
        var info = SystemInfoParser.Parse(RecordedOutputs.UbuntuSystem);

        info.Distribution.Should().Be("Ubuntu 24.04.1 LTS");
        info.Kernel.Should().Be("Linux x86_64");
        info.Shell.Should().Be("/bin/bash");
        info.Describe().Should().Be("Ubuntu 24.04.1 LTS, Linux x86_64, shell /bin/bash");
    }

    [TestMethod]
    public void Parse_Alpine_ReadsPrettyName()
    {
        var info = SystemInfoParser.Parse(RecordedOutputs.AlpineSystem);

        info.Distribution.Should().Be("Alpine Linux v3.22");
        info.Shell.Should().Be("/bin/sh");
    }

    [TestMethod]
    public void Parse_WithoutPrettyName_FallsBackToNameAndVersion()
    {
        var info = SystemInfoParser.Parse(RecordedOutputs.AlpineWithoutPrettyName);

        info.Distribution.Should().Be("Alpine Linux 3.19.0");
    }

    [TestMethod]
    public void Parse_WithoutOsRelease_StillReportsKernel()
    {
        var info = SystemInfoParser.Parse(RecordedOutputs.DarwinWithoutOsRelease);

        info.Distribution.Should().BeNull();
        info.Kernel.Should().Be("Darwin arm64");
        info.IsKnown.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("@@@@\n@@@@\n\n")]
    [DataRow("\u0000\u0001garbage without separators")]
    public void Parse_EmptyOrGarbage_NeverThrows(string? output)
    {
        var act = () => SystemInfoParser.Parse(output);

        act.Should().NotThrow();
    }

    [TestMethod]
    public void Parse_EmptyOutput_IsNotKnown()
    {
        var info = SystemInfoParser.Parse(RecordedOutputs.MissingEverything);

        info.IsKnown.Should().BeFalse();
        info.Shell.Should().BeNull();
    }

    [TestMethod]
    public void Parse_ShellThatIsNotAnAbsolutePath_IsIgnored()
    {
        var info = SystemInfoParser.Parse("Linux x86_64\n@@@@\n@@@@\nrm -rf / ; echo\n");

        info.Shell.Should().BeNull();
    }

    [TestMethod]
    public void Parse_OversizedFields_AreTruncatedAndStripControlCharacters()
    {
        var huge = new string('A', 100_000);
        var output = $"Linux\u001b[31m x86_64\n@@@@\nPRETTY_NAME=\"{huge}\"\n@@@@\n/bin/{huge}\n";

        var info = SystemInfoParser.Parse(output);

        info.Kernel.Should().NotContain("\u001b");
        info.Distribution!.Length.Should().BeLessThanOrEqualTo(80);
        info.Shell!.Length.Should().BeLessThanOrEqualTo(80);
    }
}
