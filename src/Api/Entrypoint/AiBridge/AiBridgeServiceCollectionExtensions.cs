using DTech.Core.AiBridge;
using DTech.Core.AiBridge.Approvals;
using DTech.Core.AiBridge.Commands;
using DTech.Core.AiBridge.Docker;
using DTech.Core.AiBridge.Files;
using DTech.Core.AiBridge.Settings;
using DTech.Core.AiBridge.Terminals;
using DTech.Entrypoint.AiBridge;
using DTech.Entrypoint.AiBridge.Tools;
using DTech.Persistence.Settings;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class AiBridgeServiceCollectionExtensions
{
    public static IServiceCollection AddAiBridge(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AiBridgeOptions.SectionName);
        services.AddOptions<AiBridgeOptions>().Bind(section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<AiBridgeOptions>, AiBridgeOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(CreateApprovalTimeout);
        services.AddSingleton<IAiApprovalNotifier, AiApprovalBroadcaster>();
        services.AddSingleton<AiApprovalBroker>();
        services.AddSingleton<IAiBridgeTokenGenerator, RandomAiBridgeTokenGenerator>();
        services.AddScoped<IAiBridgeSettingsStore, AiBridgeSettingsStore>();
        services.AddScoped<AiBridgeSettingsService>();
        services.AddScoped<AiServerAccessPolicy>();
        services.AddScoped<AiActionGate>();
        services.AddScoped<AiOperationRecorder>();
        services.AddScoped<AiCommandService>();
        services.AddScoped<AiTerminalLocator>();
        services.AddScoped<AiTerminalOutputReader>();
        services.AddScoped<AiTerminalInputSender>();
        services.AddScoped<AiDockerQueryService>();
        services.AddScoped<AiContainerLogReader>();
        services.AddScoped<AiContainerActionService>();
        services.AddScoped<AiFileReader>();
        services.AddScoped<AiFileWriter>();
        services.AddMcpServer()
            .WithHttpTransport()
            .WithTools<AiServerTools>()
            .WithTools<AiTerminalTools>()
            .WithTools<AiDockerTools>()
            .WithTools<AiFileTools>();
        return services;
    }

    private static AiApprovalTimeout CreateApprovalTimeout(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<AiBridgeOptions>>().Value;
        var timeout = TimeSpan.FromSeconds(options.ApprovalTimeoutSeconds);
        return new AiApprovalTimeout(timeout);
    }
}
