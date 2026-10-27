using DTech.Core.Docker;
using DTech.Core.Tests.Docker.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class DockerContainerListParserTests
{
    [TestMethod]
    public void Parse_RecordedOutput_MapsEveryContainer()
    {
        var containers = DockerContainerListParser.Parse(DockerRecordedOutputs.ContainerList);

        containers.Should().HaveCount(3);
        var web = containers[0];
        web.Name.Should().Be("web");
        web.Id.Should().StartWith("3f4e1b2c9d8a");
        web.Image.Should().Be("nginx:1.27-alpine");
        web.State.Should().Be(DockerContainerState.Running);
        web.Status.Should().Be("Up 6 days (healthy)");
        web.Health.Should().Be("healthy");
        web.Ports.Should().Be("0.0.0.0:8080->80/tcp, [::]:8080->80/tcp");
        containers[1].State.Should().Be(DockerContainerState.Exited);
        containers[1].Health.Should().BeNull();
        containers[2].Name.Should().Be("worker.paused");
        containers[2].State.Should().Be(DockerContainerState.Paused);
    }

    [TestMethod]
    public void Parse_WithoutStateField_DerivesStateFromStatusAndUsesFirstName()
    {
        var containers = DockerContainerListParser.Parse(DockerRecordedOutputs.LegacyContainerWithoutState);

        var cache = containers.Should().ContainSingle().Subject;
        cache.Name.Should().Be("cache");
        cache.State.Should().Be(DockerContainerState.Running);
        cache.Health.Should().Be("starting");
    }

    [TestMethod]
    public void Parse_SkipsWarningsMalformedJsonAndUnsafeIds()
    {
        var containers = DockerContainerListParser.Parse(DockerRecordedOutputs.MalformedLines);

        var api = containers.Should().ContainSingle().Subject;
        api.Name.Should().Be("api");
        api.State.Should().Be(DockerContainerState.Created);
    }

    [TestMethod]
    public void Parse_EmptyOutput_ReturnsNoContainers()
    {
        var containers = DockerContainerListParser.Parse(string.Empty);

        containers.Should().BeEmpty();
    }
}
