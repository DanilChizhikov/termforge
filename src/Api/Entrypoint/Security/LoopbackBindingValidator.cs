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
    private const string HttpsUrlPrefix = "https://";

    public static IReadOnlyList<string> FindNonLoopbackBindings(IConfiguration configuration)
    {
        var bindings = FindNonLoopbackBindingDetails(configuration);
        var descriptions = new List<string>(bindings.Count);
        for (var i = 0; i < bindings.Count; i++)
        {
            descriptions.Add(bindings[i].Description);
        }

        return descriptions;
    }

    public static IReadOnlyList<NonLoopbackBinding> FindNonLoopbackBindingDetails(IConfiguration configuration)
    {
        var violations = new List<NonLoopbackBinding>();
        var hasKestrelEndpoints = AddKestrelEndpointViolations(configuration, violations);
        var preferHostingUrls = configuration.GetValue<bool>(PreferHostingUrlsKey);
        if (!hasKestrelEndpoints || preferHostingUrls)
        {
            AddHostingAddressViolations(configuration, violations);
        }

        return violations;
    }

    private static bool AddKestrelEndpointViolations(IConfiguration configuration, List<NonLoopbackBinding> violations)
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

    private static void AddHostingAddressViolations(IConfiguration configuration, List<NonLoopbackBinding> violations)
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

    private static void AddPortViolation(IConfiguration configuration, string key, List<NonLoopbackBinding> violations)
    {
        var ports = configuration[key];
        if (!string.IsNullOrWhiteSpace(ports))
        {
            var isHttps = string.Equals(key, HttpsPortsKey, StringComparison.Ordinal);
            var binding = new NonLoopbackBinding($"{key}={ports} binds to every network interface", isHttps);
            violations.Add(binding);
        }
    }

    private static void AddUrlViolations(string urls, List<NonLoopbackBinding> violations)
    {
        var separatedUrls = urls.Split(UrlSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < separatedUrls.Length; i++)
        {
            var url = separatedUrls[i];
            if (!IsLoopbackUrl(url))
            {
                var isHttps = url.StartsWith(HttpsUrlPrefix, StringComparison.OrdinalIgnoreCase);
                var binding = new NonLoopbackBinding($"{url} is not a loopback address", isHttps);
                violations.Add(binding);
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
