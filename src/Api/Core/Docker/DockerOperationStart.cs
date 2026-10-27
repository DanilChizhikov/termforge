namespace DTech.Core.Docker;

public readonly record struct DockerOperationStart(DockerContainerAction Action, string Server, ContainerReference Container);
