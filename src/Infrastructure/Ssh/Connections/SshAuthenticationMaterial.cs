using Renci.SshNet;

namespace DTech.Ssh.Connections;

internal sealed record SshAuthenticationMaterial(AuthenticationMethod Method, IReadOnlyList<IDisposable> OwnedResources);
