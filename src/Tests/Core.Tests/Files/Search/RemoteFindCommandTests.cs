using System.Diagnostics;
using System.Runtime.Versioning;
using DTech.Core.Diagnostics;
using DTech.Core.Files.Search;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Files.Search;

[TestClass]
public sealed class RemoteFindCommandTests
{
    private static readonly string[] InjectionAttempts =
    [
        "'; touch /tmp/pwned; '",
        "$(touch /tmp/pwned)",
        "`touch /tmp/pwned`",
        "a' -o -exec rm -rf {} + -o -name 'b",
        "\"; reboot; \"",
        "*$HOME*",
        "it's & | ; < > ( ) { } !",
    ];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void BuildScript_PassesPatternAsSingleQuotedInameArgument()
    {
        var query = CreateQuery("/srv/app", "*.log");

        var script = RemoteFindCommand.BuildScript(query);

        script.Should().StartWith("find '/srv/app' -mindepth 1 -maxdepth 4 -iname '*.log' ");
        script.Should().Contain("sleep 20; kill $search");
    }

    [TestMethod]
    public void BuildScript_InjectionAttempt_StaysInsideSingleQuotes()
    {
        var query = CreateQuery("/srv", "it's; rm -rf ~");

        var script = RemoteFindCommand.BuildScript(query);

        script.Should().Contain("-iname '*it'\\''s; rm -rf ~*' ");
    }

    [TestMethod]
    public void BuildScript_RootPathWithQuote_IsEscaped()
    {
        var query = CreateQuery("/srv/o'brien", "x");

        var script = RemoteFindCommand.BuildScript(query);

        script.Should().StartWith("find '/srv/o'\\''brien' ");
    }

    [TestMethod]
    public void CreateRequest_WrapsInShAndAddsGraceToTimeout()
    {
        var query = CreateQuery("/srv", "x");

        var request = RemoteFindCommand.CreateRequest(query);

        request.Command.Should().StartWith("sh -c '");
        request.Timeout.Should().Be(TimeSpan.FromSeconds(20) + RemoteFindCommand.GracePeriod);
        request.MaxOutputBytes.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public async Task ShellArgument_InjectionAttempts_AreReceivedVerbatimByShell()
    {
        if (OperatingSystem.IsWindows() || !File.Exists("/bin/sh"))
        {
            Assert.Inconclusive("A POSIX shell is required to verify quoting.");
        }

        for (var i = 0; i < InjectionAttempts.Length; i++)
        {
            var attempt = InjectionAttempts[i];
            var quoted = ShellArgument.Quote(attempt);
            var script = $"printf '%s' {quoted}";
            var shellRun = new ShellRun(script, null);

            var output = await RunShellAsync(shellRun);

            output.Should().Be(attempt);
        }
    }

    [TestMethod]
    [UnsupportedOSPlatform("windows")]
    public async Task Build_InjectionAttemptInPattern_ReachesFindAsSingleArgument()
    {
        if (OperatingSystem.IsWindows() || !File.Exists("/bin/sh"))
        {
            Assert.Inconclusive("A POSIX shell is required to verify quoting.");
        }

        using var fakeTools = new TemporaryDirectory();
        var fakeFind = fakeTools.Write("find", "#!/bin/sh\nfor argument in \"$@\"; do printf '[%s]' \"$argument\"; done\n");
        File.SetUnixFileMode(fakeFind, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var attempt = "x'; echo injected; '";
        var query = CreateQuery("/srv", attempt);
        var command = RemoteFindCommand.Build(query);
        var shellRun = new ShellRun(command, fakeTools.Path);

        var output = await RunShellAsync(shellRun);

        output.Should().Contain("[-iname][*x'; echo injected; '*]");
        output.Should().NotContain("injected\n");
    }

    private static FileSearchQuery CreateQuery(string root, string pattern)
    {
        var namePattern = FileNamePattern.Create(pattern).Value;
        var limits = new FileSearchLimits(4, 100, TimeSpan.FromSeconds(20));
        var criteria = new FileSearchCriteria(namePattern, limits);
        return new FileSearchQuery(root, criteria);
    }

    private async Task<string> RunShellAsync(ShellRun shellRun)
    {
        var startInfo = new ProcessStartInfo("/bin/sh")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(shellRun.Script);
        if (shellRun.ExtraPath is not null)
        {
            var currentPath = Environment.GetEnvironmentVariable("PATH");
            startInfo.Environment["PATH"] = $"{shellRun.ExtraPath}:{currentPath}";
        }

        using var process = Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
        await process.WaitForExitAsync(TestContext.CancellationToken);
        return output;
    }

    private sealed record ShellRun(string Script, string? ExtraPath);
}
