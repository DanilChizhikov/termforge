namespace DTech.Core.Docker;

public sealed record ContainerActionRequest(Guid ConnectionId, string? Container, DockerContainerAction Action)
{
    public bool Confirmed { get; init; }

    public bool RequiresConfirmation => Action is DockerContainerAction.Stop or DockerContainerAction.Restart;
}
