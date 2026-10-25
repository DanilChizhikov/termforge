using DTech.Core.Files;
using DTech.Core.Terminal;
using DTech.Ssh.Connections;
using DTech.Ssh.Files;
using DTech.Ssh.Terminal;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Ssh;

public static class SshTransportServiceCollectionExtensions
{
    public static IServiceCollection AddSshTransport(this IServiceCollection services)
    {
        services.AddSingleton<SshAuthenticationFactory>();
        services.AddSingleton<SshClientConnector>();
        services.AddSingleton<ITerminalSessionFactory, SshNetTerminalSessionFactory>();
        services.AddSingleton<IRemoteFileClientFactory, SshNetRemoteFileClientFactory>();
        return services;
    }
}
