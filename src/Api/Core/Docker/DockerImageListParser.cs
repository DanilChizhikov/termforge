using System.Text.Json;
using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public static class DockerImageListParser
{
    private const string MissingValue = "<none>";
    private const int RepositoryMaxLength = 255;
    private const int TagMaxLength = 128;
    private const int SizeMaxLength = 32;
    private const int CreatedSinceMaxLength = 60;

    public static IReadOnlyList<DockerImage> Parse(string output)
    {
        var lines = RemoteText.SplitLines(output);
        var images = new List<DockerImage>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line[0] != '{')
            {
                continue;
            }

            var image = TryParseLine(line);
            if (image is not null)
            {
                images.Add(image);
            }
        }

        return images;
    }

    private static DockerImage? TryParseLine(string line)
    {
        try
        {
            using var document = JsonDocument.Parse(line);
            return Map(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DockerImage? Map(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = ReadString(element, "ID");
        if (!ImageReference.TryParse(id, out _))
        {
            return null;
        }

        var repository = ReadString(element, "Repository");
        var tag = ReadString(element, "Tag");
        var size = ReadString(element, "Size");
        var createdSince = ReadString(element, "CreatedSince");
        return new DockerImage
        {
            Id = id,
            Repository = SanitizeName(repository, RepositoryMaxLength),
            Tag = SanitizeName(tag, TagMaxLength),
            Size = RemoteText.Sanitize(size, SizeMaxLength),
            CreatedSince = RemoteText.Sanitize(createdSince, CreatedSinceMaxLength),
        };
    }

    private static string SanitizeName(string value, int maxLength)
    {
        var sanitized = RemoteText.Sanitize(value, maxLength);
        return sanitized.Length == 0 ? MissingValue : sanitized;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return property.GetString() ?? string.Empty;
    }
}
