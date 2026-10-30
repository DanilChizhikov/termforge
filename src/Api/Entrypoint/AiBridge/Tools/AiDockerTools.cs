using System.ComponentModel;
using DTech.Core.AiBridge.Docker;
using DTech.Core.Docker;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DTech.Entrypoint.AiBridge.Tools;

[McpServerToolType]
public sealed class AiDockerTools(AiDockerQueryService queryService, AiContainerLogReader logReader, AiContainerActionService actionService)
{
    [McpServerTool(Name = "docker_list_containers", ReadOnly = true)]
    [Description("Lists all Docker containers on a server with their state, status and ports.")]
    public async Task<AiContainersResponse> ListContainers(
        [Description("Server id from list_servers.")] Guid serverId,
        CancellationToken cancellationToken)
    {
        var listed = await queryService.ListContainersAsync(serverId, cancellationToken);
        var overview = AiToolResults.ValueOrThrow(listed);
        var containers = new List<AiContainerResponse>(overview.Containers.Count);
        for (var i = 0; i < overview.Containers.Count; i++)
        {
            var container = AiContainerResponse.From(overview.Containers[i]);
            containers.Add(container);
        }

        var availability = overview.Availability;
        var reason = availability.IsAvailable ? null : availability.Reason;
        return new AiContainersResponse(overview.ServerName, availability.IsAvailable, reason, containers);
    }

    [McpServerTool(Name = "docker_list_images", ReadOnly = true)]
    [Description("Lists the Docker images on a server.")]
    public async Task<AiImagesResponse> ListImages(
        [Description("Server id from list_servers.")] Guid serverId,
        CancellationToken cancellationToken)
    {
        var listed = await queryService.ListImagesAsync(serverId, cancellationToken);
        var images = AiToolResults.ValueOrThrow(listed);
        return new AiImagesResponse(images.ServerName, images.Images);
    }

    [McpServerTool(Name = "docker_container_logs", ReadOnly = true)]
    [Description("Returns the last log lines of a Docker container.")]
    public async Task<AiContainerLogs> ContainerLogs(
        [Description("Server id from list_servers.")] Guid serverId,
        [Description("Container name or id.")] string container,
        [Description("Number of lines from the end, 1 to 5000. Defaults to 200.")] int? tail = null,
        CancellationToken cancellationToken = default)
    {
        var request = new AiContainerLogRequest(serverId, container, tail);
        var read = await logReader.ReadAsync(request, cancellationToken);
        return AiToolResults.ValueOrThrow(read);
    }

    [McpServerTool(Name = "docker_container_action", Destructive = true)]
    [Description("Starts, stops, restarts or removes a Docker container. The user may have to approve it first.")]
    public async Task<AiContainerActionResponse> ContainerAction(
        [Description("Server id from list_servers.")] Guid serverId,
        [Description("Container name or id.")] string container,
        [Description("One of start, stop, restart, remove.")] string action,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DockerContainerAction>(action, true, out var containerAction) || !Enum.IsDefined(containerAction))
        {
            throw new McpException("The action must be start, stop, restart or remove.");
        }

        var request = new AiContainerActionRequest(serverId, container, containerAction);
        var completed = await actionService.RunAsync(request, cancellationToken);
        var result = AiToolResults.ValueOrThrow(completed);
        var actionName = result.Action.ToString().ToLowerInvariant();
        return new AiContainerActionResponse(result.Container, actionName, result.OperationId);
    }
}
