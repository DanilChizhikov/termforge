using DTech.Core.Editor;
using DTech.Ssh.Editor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DTech.Ssh;

public static class RemoteEditorTransportServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteEditorTransport(this IServiceCollection services)
    {
        services.TryAddSingleton<IRemoteEditorFileClientFactory, SshNetRemoteEditorFileClientFactory>();
        return services;
    }
}
