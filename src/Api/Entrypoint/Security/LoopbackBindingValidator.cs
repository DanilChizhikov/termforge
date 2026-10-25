using Microsoft.AspNetCore.Http;

namespace DTech.Entrypoint.Security;

internal static class LoopbackBindingValidator
{
    private const string UrlsKey = "urls";
    private const string HttpPortsKey = "http_ports";
    private const string HttpsPortsKey = "https_ports";
    private const string PreferHostingUrlsKey = "preferHostingUrls";
    private const string KestrelEndpointsSection = "Kestrel:Endpoints";
    private const string KestrelEndpointUrlKey = "Url";
    private const char UrlSeparator = ';';

    public static IReadOnlyList<string> FindNonLoopbackBindings(IConfiguration configuration)
    {
        var violations = new List<string>();
        var hasKestrelEndpoints = AddKestrelEndpointViolations(configuration, violations);
        var preferHostingUrls = configuration.GetValue<bool>(PreferHostingUrlsKey);
        if (!hasKestrelEndpoints || preferHostingUrls)
        {
            AddHostingAddressViolations(configuration, violations);
        }

        return violations;
    }

    private static bool AddKestrelEndpointViolations(IConfiguration configuration, List<string> violations)
    {
        var hasKestrelEndpoints = false;
        var endpoints = configuration.GetSection(KestrelEndpointsSection).GetChildren();
        foreach (var endpoint in endpoints)
        {
            var endpointUrl = endpoint[KestrelEndpointUrlKey];
            if (string.IsNullOrWhiteSpace(endpointUrl))
            {
                continue;
            }

            hasKestrelEndpoints = true;
            AddUrlViolations(endpointUrl, violations);
        }

        return hasKestrelEndpoints;
    }

    private static void AddHostingAddressViolations(IConfiguration configuration, List<string> violations)
    {
        var configuredUrls = configuration[UrlsKey];
        if (!string.IsNullOrWhiteSpace(configuredUrls))
        {
            AddUrlViolations(configuredUrls, violations);
            return;
        }

        AddPortViolation(configuration, HttpPortsKey, violations);
        AddPortViolation(configuration, HttpsPortsKey, violations);
    }

    private static void AddPortViolation(IConfiguration configuration, string key, List<string> violations)
    {
        var ports = configuration[key];
        if (!string.IsNullOrWhiteSpace(ports))
        {
            violations.Add($"{key}={ports} binds to every network interface");
        }
    }

    private static void AddUrlViolations(string urls, List<string> violations)
    {
        var separatedUrls = urls.Split(UrlSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < separatedUrls.Length; i++)
        {
            var url = separatedUrls[i];
            if (!IsLoopbackUrl(url))
            {
                violations.Add($"{url} is not a loopback address");
            }
        }
    }

    private static bool IsLoopbackUrl(string url)
    {
        BindingAddress bindingAddress;
        try
        {
            bindingAddress = BindingAddress.Parse(url);
        }
        catch (FormatException)
        {
            return false;
        }

        if (bindingAddress.IsUnixPipe || bindingAddress.IsNamedPipe)
        {
            return false;
        }

        return LoopbackHost.IsLoopback(bindingAddress.Host);
    }
}
