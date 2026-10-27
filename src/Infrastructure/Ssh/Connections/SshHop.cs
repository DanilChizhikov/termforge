using DTech.Core.Connections;
using Renci.SshNet;

namespace DTech.Ssh.Connections;

internal sealed record SshHop<TClient>(SshConnectionTarget Target, SshEndpoint DialEndpoint, Func<ConnectionInfo, TClient> CreateClient)
    where TClient : BaseClient;
