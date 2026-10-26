using DTech.Core.Docker;
using FluentAssertions;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class DockerCommandsTests
{
    [TestMethod]
    public void ListContainers_UsesJsonFormatForAllContainers()
    {
        DockerCommands.ListContainers.Should().Be("docker ps --all --no-trunc --format '{{json .}}'");
    }

    [TestMethod]
    [DataRow(DockerContainerAction.Start, "docker start 'web'")]
    [DataRow(DockerContainerAction.Stop, "docker stop 'web'")]
    [DataRow(DockerContainerAction.Restart, "docker restart 'web'")]
    public void Action_QuotesContainer(DockerContainerAction action, string expected)
    {
        var container = Parse("web");

        var command = DockerCommands.Action(action, container);

        command.Should().Be(expected);
    }

    [TestMethod]
    public void Logs_WithFollow_AddsTailAndFollowAndMergesStreams()
    {
        var container = Parse("web");
        var options = ContainerLogOptions.Create(500, true);

        var command = DockerCommands.Logs(container, options);

        command.Should().Be("docker logs --tail 500 --follow 'web' 2>&1");
    }

    [TestMethod]
    public void Logs_WithoutFollow_OmitsFollow()
    {
        var container = Parse("db");
        var options = ContainerLogOptions.Create(null, false);

        var command = DockerCommands.Logs(container, options);

        command.Should().Be("docker logs --tail 200 'db' 2>&1");
    }

    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(-5, 1)]
    [DataRow(100000, ContainerLogOptions.MaxTail)]
    public void LogOptions_ClampTailToLimit(int requested, int expected)
    {
        var options = ContainerLogOptions.Create(requested, false);

        options.Tail.Should().Be(expected);
    }

    [TestMethod]
    public void Shell_Auto_FallsBackFromBashToSh()
    {
        var container = Parse("web");

        var command = DockerCommands.Shell(container, ContainerShell.Auto);

        command.Should().Be("docker exec -it 'web' sh -c 'if command -v bash >/dev/null 2>&1; then exec bash; fi; exec sh'");
    }

    [TestMethod]
    [DataRow(ContainerShell.Bash, "docker exec -it 'web' bash")]
    [DataRow(ContainerShell.Sh, "docker exec -it 'web' sh")]
    public void Shell_Explicit_UsesRequestedShell(ContainerShell shell, string expected)
    {
        var container = Parse("web");

        var command = DockerCommands.Shell(container, shell);

        command.Should().Be(expected);
    }

    [TestMethod]
    public void ShellStartupLine_ReplacesLoginShellAndSkipsHistory()
    {
        var container = Parse("web");

        var line = DockerCommands.ShellStartupLine(container, ContainerShell.Sh);

        line.Should().StartWith(" ");
        line.Should().Contain("; exec docker exec -it 'web' sh");
        line.Should().EndWith("\r");
    }

    private static ContainerReference Parse(string value)
    {
        ContainerReference.TryParse(value, out var reference).Should().BeTrue();
        return reference;
    }
}
