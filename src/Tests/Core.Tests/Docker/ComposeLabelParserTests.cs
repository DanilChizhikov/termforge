using DTech.Core.Docker;
using FluentAssertions;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class ComposeLabelParserTests
{
    [TestMethod]
    public void Parse_ReadsProjectWorkingDirectoryAndMultipleConfigFiles()
    {
        const string labels = "com.docker.compose.config-hash=ab12,com.docker.compose.project=shop,"
            + "com.docker.compose.project.config_files=/srv/shop/compose.yml,/srv/shop/compose.prod.yml,"
            + "com.docker.compose.project.working_dir=/srv/shop,maintainer=ops@example.test";

        var compose = ComposeLabelParser.Parse(labels);

        compose.Should().NotBeNull();
        compose!.Project.Should().Be("shop");
        compose.WorkingDirectory.Should().Be("/srv/shop");
        compose.ConfigFiles.Should().Equal("/srv/shop/compose.yml", "/srv/shop/compose.prod.yml");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("maintainer=ops@example.test")]
    [DataRow("com.docker.compose.project=Shop;reboot")]
    public void Parse_WithoutValidProject_ReturnsNull(string labels)
    {
        ComposeLabelParser.Parse(labels).Should().BeNull();
    }

    [TestMethod]
    public void Parse_DropsRelativeOrControlCharacterPaths()
    {
        const string labels = "com.docker.compose.project=shop,com.docker.compose.project.working_dir=relative/dir,"
            + "com.docker.compose.project.config_files=/srv/a\nb.yml";

        var compose = ComposeLabelParser.Parse(labels);

        compose!.WorkingDirectory.Should().BeEmpty();
        compose.ConfigFiles.Should().BeEmpty();
    }

    [TestMethod]
    public void ListParser_AttachesComposeLabelsToContainer()
    {
        const string output = "{\"ID\":\"3f4e1b2c9d8a\",\"Image\":\"nginx\",\"Names\":\"shop-web-1\",\"State\":\"running\",\"Status\":\"Up 1 hour\","
            + "\"Labels\":\"com.docker.compose.project=shop,com.docker.compose.project.working_dir=/srv/shop\"}\n";

        var container = DockerContainerListParser.Parse(output).Should().ContainSingle().Subject;

        container.Compose!.Project.Should().Be("shop");
        container.Compose.WorkingDirectory.Should().Be("/srv/shop");
    }
}
