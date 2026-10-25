namespace DTech.Core.Connections;

public sealed record HostKeyVerificationRequest(SshEndpoint Endpoint, HostKey HostKey);
