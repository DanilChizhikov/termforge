namespace DTech.Core.Docker;

public sealed record DockerImage
{
    public required string Id { get; init; }

    public required string Repository { get; init; }

    public required string Tag { get; init; }

    public string Size { get; init; } = string.Empty;

    public string CreatedSince { get; init; } = string.Empty;
}
