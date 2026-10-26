using System.Globalization;
using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public static class DockerCommands
{
    public const string ListContainers = "docker ps --all --no-trunc --format '{{json .}}'";

    private const string AutoShellScript = "if command -v bash >/dev/null 2>&1; then exec bash; fi; exec sh";
    private const string ClearScreenCommand = "printf '\\033[H\\033[2J'";

    public static string Action(DockerContainerAction action, ContainerReference container)
    {
        var verb = action switch
        {
            DockerContainerAction.Start => "start",
            DockerContainerAction.Stop => "stop",
            DockerContainerAction.Restart => "restart",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported container action."),
        };
        return $"docker {verb} {container.Quoted}";
    }

    public static string Logs(ContainerReference container, ContainerLogOptions options)
    {
        var tail = options.Tail.ToString(CultureInfo.InvariantCulture);
        var follow = options.Follow ? " --follow" : string.Empty;
        return $"docker logs --tail {tail}{follow} {container.Quoted} 2>&1";
    }

    public static string Shell(ContainerReference container, ContainerShell shell)
    {
        var program = ShellProgram(shell);
        return $"docker exec -it {container.Quoted} {program}";
    }

    public static string ShellStartupLine(ContainerReference container, ContainerShell shell)
    {
        var execCommand = Shell(container, shell);
        return $" {ClearScreenCommand}; exec {execCommand}\r";
    }

    private static string ShellProgram(ContainerShell shell)
    {
        if (shell == ContainerShell.Bash)
        {
            return "bash";
        }

        if (shell == ContainerShell.Sh)
        {
            return "sh";
        }

        var quotedScript = ShellArgument.Quote(AutoShellScript);
        return $"sh -c {quotedScript}";
    }
}
