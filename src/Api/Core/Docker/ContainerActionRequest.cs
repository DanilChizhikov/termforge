using DTech.Core.Safety;

namespace DTech.Core.Docker;

public sealed record ContainerActionRequest(Guid ConnectionId, string? Container, DockerContainerAction Action)
{
    public string? ConfirmationToken { get; init; }

    public DangerousOperation? DangerousOperation => Action switch
    {
        DockerContainerAction.Stop => Safety.DangerousOperation.ContainerStop,
        DockerContainerAction.Restart => Safety.DangerousOperation.ContainerRestart,
        DockerContainerAction.Remove => Safety.DangerousOperation.ContainerRemove,
        _ => null,
    };
}
