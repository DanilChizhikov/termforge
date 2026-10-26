using DTech.Core.Terminal.Sessions;

namespace DTech.Core.Docker;

public sealed record ContainerShellRequest(TerminalLaunchRequest Launch, string? Container, ContainerShell Shell);
