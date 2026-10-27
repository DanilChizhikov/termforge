using DTech.Core.Editor.Sudo;
using DTech.Ssh.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DTech.Ssh;

public static class RemoteShellTransportServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteShellTransport(this IServiceCollection services)
    {
        services.TryAddSingleton<IRemoteShellCommandRunnerFactory, SshNetRemoteShellCommandRunnerFactory>();
        return services;
    }
}
