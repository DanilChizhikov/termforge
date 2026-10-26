using System.Text.Json;
using System.Text.RegularExpressions;
using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public static partial class DockerContainerListParser
{
    private const int ImageMaxLength = 200;
    private const int StatusMaxLength = 120;
    private const int PortsMaxLength = 300;
    private const int CreatedAtMaxLength = 60;

    public static IReadOnlyList<DockerContainer> Parse(string output)
    {
        var lines = RemoteText.SplitLines(output);
        var containers = new List<DockerContainer>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var container = TryParseLine(line);
            if (container is not null)
            {
                containers.Add(container);
            }
        }

        return containers;
    }

    private static DockerContainer? TryParseLine(string line)
    {
        if (line[0] != '{')
        {
            return null;
        }

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

    private static DockerContainer? Map(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = ReadString(element, "ID");
        var names = ReadString(element, "Names");
        var name = FirstName(names);
        var hasValidId = ContainerReference.TryParse(id, out _);
        var hasValidName = ContainerReference.TryParse(name, out _);
        if (!hasValidId || !hasValidName)
        {
            return null;
        }

        var status = ReadString(element, "Status");
        var stateText = ReadString(element, "State");
        var image = ReadString(element, "Image");
        var ports = ReadString(element, "Ports");
        var createdAt = ReadString(element, "CreatedAt");
        return new DockerContainer
        {
            Id = id,
            Name = name,
            Image = RemoteText.Sanitize(image, ImageMaxLength),
            State = ReadState(stateText, status),
            Status = RemoteText.Sanitize(status, StatusMaxLength),
            Health = FindHealth(status),
            Ports = RemoteText.Sanitize(ports, PortsMaxLength),
            CreatedAt = RemoteText.Sanitize(createdAt, CreatedAtMaxLength),
        };
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return property.GetString() ?? string.Empty;
    }

    private static string FirstName(string names)
    {
        var separatorIndex = names.IndexOf(',', StringComparison.Ordinal);
        var first = separatorIndex < 0 ? names : names[..separatorIndex];
        return first.Trim().TrimStart('/');
    }

    private static DockerContainerState ReadState(string stateText, string status)
    {
        var fromState = ParseStateName(stateText.Trim().ToLowerInvariant());
        return fromState == DockerContainerState.Unknown ? ParseStatus(status) : fromState;
    }

    private static DockerContainerState ParseStateName(string state)
    {
        return state switch
        {
            "created" => DockerContainerState.Created,
            "running" => DockerContainerState.Running,
            "paused" => DockerContainerState.Paused,
            "restarting" => DockerContainerState.Restarting,
            "removing" => DockerContainerState.Removing,
            "exited" => DockerContainerState.Exited,
            "dead" => DockerContainerState.Dead,
            _ => DockerContainerState.Unknown,
        };
    }

    private static DockerContainerState ParseStatus(string status)
    {
        var trimmed = status.Trim();
        if (trimmed.StartsWith("Up", StringComparison.OrdinalIgnoreCase))
        {
            var isPaused = trimmed.Contains("(Paused)", StringComparison.OrdinalIgnoreCase);
            return isPaused ? DockerContainerState.Paused : DockerContainerState.Running;
        }

        if (trimmed.StartsWith("Restarting", StringComparison.OrdinalIgnoreCase))
        {
            return DockerContainerState.Restarting;
        }

        if (trimmed.StartsWith("Exited", StringComparison.OrdinalIgnoreCase))
        {
            return DockerContainerState.Exited;
        }

        if (trimmed.StartsWith("Created", StringComparison.OrdinalIgnoreCase))
        {
            return DockerContainerState.Created;
        }

        if (trimmed.StartsWith("Dead", StringComparison.OrdinalIgnoreCase))
        {
            return DockerContainerState.Dead;
        }

        if (trimmed.StartsWith("Removal", StringComparison.OrdinalIgnoreCase))
        {
            return DockerContainerState.Removing;
        }

        return DockerContainerState.Unknown;
    }

    private static string? FindHealth(string status)
    {
        var match = HealthPattern().Match(status);
        if (!match.Success)
        {
            return null;
        }

        var health = match.Groups["health"].Value.ToLowerInvariant();
        return health == "health: starting" ? "starting" : health;
    }

    [GeneratedRegex(@"\((?<health>healthy|unhealthy|health: starting)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HealthPattern();
}
