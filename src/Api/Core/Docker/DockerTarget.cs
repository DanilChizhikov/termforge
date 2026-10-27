using DTech.Core.Connections;

namespace DTech.Core.Docker;

public sealed record DockerTarget(Guid ConnectionId, string ServerName, SshConnectionTarget Target);
