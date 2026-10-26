using DTech.Core.Terminal.Recording;
using DTech.Entrypoint.Recordings;
using DTech.Persistence.Settings;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class TerminalRecordingServiceCollectionExtensions
{
    public static IServiceCollection AddTerminalRecording(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(TerminalRecordingOptions.SectionName);
        services.Configure<TerminalRecordingOptions>(section);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(TerminalRecordingDirectoryResolver.Resolve);
        services.AddSingleton<TerminalRecordingStorage>();
        services.AddSingleton<TerminalRecordingRegistry>();
        services.AddSingleton<TerminalRecorderFactory>();
        services.AddSingleton<TerminalRecordingRetention>();
        services.AddSingleton<TerminalRecordingLibrary>();
        services.AddScoped<ITerminalRecordingSettingsStore, TerminalRecordingSettingsStore>();
        services.AddScoped<TerminalRecordingSettingsService>();
        services.AddScoped<TerminalRecordingService>();
        services.AddHostedService<TerminalRecordingStartupService>();
        return services;
    }
}
