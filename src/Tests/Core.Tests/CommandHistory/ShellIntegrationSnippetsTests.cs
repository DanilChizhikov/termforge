using System.Diagnostics;
using System.Text;
using DTech.Core.CommandHistory;
using FluentAssertions;

namespace DTech.Core.Tests.CommandHistory;

[TestClass]
public sealed class ShellIntegrationSnippetsTests
{
    private static readonly TimeSpan ShellTimeout = TimeSpan.FromSeconds(20);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void For_BothShells_StartWithSpaceSoTheSnippetItselfIsNeverStoredAndEmitTheMarkers()
    {
        var bash = ShellIntegrationSnippets.For(ShellKind.Bash);
        var zsh = ShellIntegrationSnippets.For(ShellKind.Zsh);

        bash.Should().StartWith(" ").And.Contain("633;E").And.Contain("133;D").And.Contain("PROMPT_COMMAND");
        zsh.Should().StartWith(" ").And.Contain("633;E").And.Contain("133;D").And.Contain("add-zsh-hook preexec");
        bash.Should().NotContain("\n");
        zsh.Should().NotContain("\n");
    }

    [TestMethod]
    public async Task BashSnippet_ReportsCommandsAndExitCodesAndSkipsLeadingSpaceCommands()
    {
        var bashPath = FindExecutable("bash");
        if (bashPath is null)
        {
            Assert.Inconclusive("bash is not available on this machine.");
        }

        var script = new StringBuilder();
        script.Append(ShellIntegrationSnippets.For(ShellKind.Bash)).Append('\n');
        script.Append("echo \"a;b\"\n");
        script.Append(" echo hidden-from-history\n");
        script.Append("false\n");
        script.Append("printf 'a\\\\b'\n");
        script.Append("exit\n");

        var output = await RunInteractiveAsync(bashPath!, ["--norc", "--noprofile", "-i"], script.ToString());
        var parser = new ShellIntegrationParser();
        var events = parser.Feed(Encoding.UTF8.GetBytes(output));
        var commands = CollectCommands(events);

        commands.Should().NotContain(command => command.Text.Contains("hidden-from-history", StringComparison.Ordinal));
        commands.Select(command => command.Text).Should().Contain(["echo \"a;b\"", "false", "printf 'a\\\\b'"]);
        commands.Single(command => command.Text == "false").ExitCode.Should().Be(1);
        commands.Single(command => command.Text == "echo \"a;b\"").ExitCode.Should().Be(0);
    }

    [TestMethod]
    public async Task ZshSnippet_ReportsCommandsAndExitCodes()
    {
        var zshPath = FindExecutable("zsh");
        if (zshPath is null)
        {
            Assert.Inconclusive("zsh is not available on this machine.");
        }

        var script = new StringBuilder();
        script.Append(ShellIntegrationSnippets.For(ShellKind.Zsh)).Append('\n');
        script.Append("echo hello; echo again\n");
        script.Append("false\n");
        script.Append("exit\n");

        var output = await RunInteractiveAsync(zshPath!, ["-f", "-i"], script.ToString());
        var parser = new ShellIntegrationParser();
        var events = parser.Feed(Encoding.UTF8.GetBytes(output));
        var commands = CollectCommands(events);

        commands.Select(command => command.Text).Should().Contain(["echo hello; echo again", "false"]);
        commands.Single(command => command.Text == "false").ExitCode.Should().Be(1);
    }

    private static List<CapturedCommand> CollectCommands(IReadOnlyList<ShellIntegrationEvent> events)
    {
        var commands = new List<CapturedCommand>();
        string? pending = null;
        for (var i = 0; i < events.Count; i++)
        {
            var shellEvent = events[i];
            if (shellEvent.Kind == ShellIntegrationEventKind.CommandLine)
            {
                pending = shellEvent.CommandLine;
            }
            else if (pending is not null)
            {
                commands.Add(new CapturedCommand(pending, shellEvent.ExitCode));
                pending = null;
            }
        }

        return commands;
    }

    private async Task<string> RunInteractiveAsync(string shellPath, string[] arguments, string input)
    {
        var startInfo = new ProcessStartInfo(shellPath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        for (var i = 0; i < arguments.Length; i++)
        {
            startInfo.ArgumentList.Add(arguments[i]);
        }

        startInfo.Environment["HISTCONTROL"] = "ignorespace";
        startInfo.Environment["HOME"] = Path.GetTempPath();
        startInfo.Environment["HISTFILE"] = string.Empty;
        using var process = Process.Start(startInfo)!;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(ShellTimeout);
        var standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var standardError = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.StandardInput.WriteAsync(input.AsMemory(), timeout.Token);
        process.StandardInput.Close();
        await process.WaitForExitAsync(timeout.Token);
        var output = await standardOutput;
        _ = await standardError;
        return output;
    }

    private static string? FindExecutable(string name)
    {
        var searchPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var directories = searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < directories.Length; i++)
        {
            var candidate = Path.Combine(directories[i], name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private sealed record CapturedCommand(string Text, int? ExitCode);
}
