using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.Files;
using DTech.Ssh.Commands;
using DTech.Core.Terminal;
using DTech.Ssh.Connections;
using DTech.Ssh.Files;
using DTech.Ssh.Terminal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DTech.Ssh;

public static class SshTransportServiceCollectionExtensions
{
    public static IServiceCollection AddSshTransport(this IServiceCollection services)
    {
        services.TryAddSingleton<IHostKeyTrustPrompt, RejectingHostKeyTrustPrompt>();
        services.AddSingleton<SshAuthenticationFactory>();
        services.AddSingleton<SshHopConnector>();
        services.AddSingleton<SshClientConnector>();
        services.AddSingleton<ITerminalSessionFactory, SshNetTerminalSessionFactory>();
        services.AddSingleton<IRemoteFileClientFactory, SshNetRemoteFileClientFactory>();
        services.AddSingleton<IRemoteCommandRunnerFactory, SshNetRemoteCommandRunnerFactory>();
        services.AddSingleton<IRemoteCommandStreamFactory, SshNetRemoteCommandStreamFactory>();
        return services;
    }
}
