using DTech.Core.Docker;

namespace DTech.Core.AiBridge.Docker;

public readonly record struct AiContainerActionRequest(Guid ConnectionId, string? Container, DockerContainerAction Action);
