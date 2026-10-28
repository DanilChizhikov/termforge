using DTech.Core.Docker;
using FluentAssertions;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class DockerImageListParserTests
{
    [TestMethod]
    public void Parse_MapsImagesAndKeepsNoneMarkers()
    {
        const string output = "WARNING: something\n"
            + "{\"ID\":\"sha256:3f4e1b2c9d8a\",\"Repository\":\"nginx\",\"Tag\":\"1.27-alpine\",\"Size\":\"47.9MB\",\"CreatedSince\":\"3 weeks ago\"}\n"
            + "{\"ID\":\"sha256:a1b2c3d4e5f6\",\"Repository\":\"<none>\",\"Tag\":\"<none>\",\"Size\":\"120MB\",\"CreatedSince\":\"2 months ago\"}\n"
            + "{\"ID\":\"bad id;\",\"Repository\":\"x\",\"Tag\":\"y\"}\n"
            + "{not json\n";

        var images = DockerImageListParser.Parse(output);

        images.Should().HaveCount(2);
        images[0].Id.Should().Be("sha256:3f4e1b2c9d8a");
        images[0].Repository.Should().Be("nginx");
        images[0].Tag.Should().Be("1.27-alpine");
        images[0].Size.Should().Be("47.9MB");
        images[0].CreatedSince.Should().Be("3 weeks ago");
        images[1].Repository.Should().Be("<none>");
        images[1].Tag.Should().Be("<none>");
    }
}
