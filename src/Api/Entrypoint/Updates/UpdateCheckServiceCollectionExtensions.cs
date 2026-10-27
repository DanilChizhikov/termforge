using System.Net.Http.Headers;
using DTech.Core.Updates;
using DTech.Entrypoint.Updates;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static class UpdateCheckServiceCollectionExtensions
{
    private const long MaxResponseBytes = 1024 * 1024;
    private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(15);

    public static IServiceCollection AddUpdateCheck(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(UpdateOptions.SectionName);
        services.AddOptions<UpdateOptions>().Bind(section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<UpdateOptions>, UpdateOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICurrentVersionProvider, AssemblyVersionProvider>();
        services.AddHttpClient<IReleaseSource, GitHubReleaseSource>(ConfigureClient)
            .ConfigurePrimaryHttpMessageHandler(CreateHandler);
        services.TryAddSingleton<IUpdateChecker>(CreateChecker);
        services.AddHostedService<UpdateCheckStartupService>();
        return services;
    }

    private static void ConfigureClient(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<UpdateOptions>>().Value;
        var versionProvider = provider.GetRequiredService<ICurrentVersionProvider>();
        var baseUrl = options.ApiBaseUrl.EndsWith('/') ? options.ApiBaseUrl : $"{options.ApiBaseUrl}/";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.MaxResponseContentBufferSize = MaxResponseBytes;
        var product = new ProductInfoHeaderValue("TermForge", versionProvider.Current.ToString());
        client.DefaultRequestHeaders.UserAgent.Add(product);
        var accept = new MediaTypeWithQualityHeaderValue("application/vnd.github+json");
        client.DefaultRequestHeaders.Accept.Add(accept);
    }

    private static HttpMessageHandler CreateHandler()
    {
        return new SocketsHttpHandler { PooledConnectionLifetime = ConnectionLifetime };
    }

    private static UpdateChecker CreateChecker(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<UpdateOptions>>().Value;
        var cacheSettings = new UpdateCacheSettings(
            TimeSpan.FromMinutes(options.CacheMinutes),
            TimeSpan.FromMinutes(options.FailureCacheMinutes));
        var source = provider.GetRequiredService<IReleaseSource>();
        var versionProvider = provider.GetRequiredService<ICurrentVersionProvider>();
        var timeProvider = provider.GetRequiredService<TimeProvider>();
        return new UpdateChecker(source, versionProvider, timeProvider, cacheSettings);
    }
}
