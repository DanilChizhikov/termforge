using System.Globalization;
using System.Text;
using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public static class DockerCommands
{
    public const string ListContainers = "docker ps --all --no-trunc --format '{{json .}}'";
    public const string ListImages = "docker image ls --all --no-trunc --format '{{json .}}'";

    private const string AutoShellScript = "if command -v bash >/dev/null 2>&1; then exec bash; fi; exec sh";
    private const string ClearScreenCommand = "printf '\\033[H\\033[2J'";

    public static string Action(DockerContainerAction action, ContainerReference container)
    {
        var verb = action switch
        {
            DockerContainerAction.Start => "start",
            DockerContainerAction.Stop => "stop",
            DockerContainerAction.Restart => "restart",
            DockerContainerAction.Remove => "rm -f",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported container action."),
        };
        return $"docker {verb} {container.Quoted}";
    }

    public static string ComposeProjectContainers(ComposeProjectName project)
    {
        var filter = ShellArgument.Quote($"label={ComposeLabelParser.ProjectLabel}={project.Value}");
        return $"docker ps --all --no-trunc --filter {filter} --format '{{{{json .}}}}'";
    }

    public static string Compose(ComposeAction action, ComposeProjectName project, ComposeLabels labels)
    {
        if (action == ComposeAction.Restart)
        {
            return $"docker compose -p {project.Quoted} restart";
        }

        var compose = ComposeWithFiles(project, labels);
        return $"{compose} down && {compose} up -d";
    }

    public static string RemoveImage(ImageReference image)
    {
        return $"docker rmi {image.Quoted}";
    }

    public static string Pull(ImageReference image)
    {
        return $"docker pull {image.Quoted} 2>&1";
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

    private static string ComposeWithFiles(ComposeProjectName project, ComposeLabels labels)
    {
        var builder = new StringBuilder($"docker compose -p {project.Quoted}");
        if (labels.WorkingDirectory.Length > 0)
        {
            var workingDirectory = ShellArgument.Quote(labels.WorkingDirectory);
            builder.Append($" --project-directory {workingDirectory}");
        }

        for (var i = 0; i < labels.ConfigFiles.Count; i++)
        {
            var configFile = ShellArgument.Quote(labels.ConfigFiles[i]);
            builder.Append($" -f {configFile}");
        }

        return builder.ToString();
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
