using System.Net;
using DTech.Entrypoint.Security;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class BootstrapUrlFactory(IServer server, BootstrapToken bootstrapToken, IOptions<LifecycleOptions> options)
{
    public Uri? Create()
    {
        var browserOrigin = options.Value.BrowserOrigin;
        if (!string.IsNullOrWhiteSpace(browserOrigin))
        {
            if (!Uri.TryCreate(browserOrigin, UriKind.Absolute, out var browserUri)
                || browserUri.Scheme is not ("http" or "https")
                || !LoopbackHost.IsLoopback(browserUri.Host)
                || browserUri.AbsolutePath != "/" || browserUri.Query.Length != 0
                || browserUri.Fragment.Length != 0 || browserUri.UserInfo.Length != 0)
            {
                throw new InvalidOperationException("The browser origin must be a loopback HTTP origin.");
            }

            var configuredOrigin = browserUri.GetLeftPart(UriPartial.Authority);
            return new Uri($"{configuredOrigin}{SecurityPaths.BootstrapPage}#token={bootstrapToken.Value}");
        }

        var addressesFeature = server.Features.Get<IServerAddressesFeature>();
        if (addressesFeature is null)
        {
            return null;
        }

        foreach (var address in addressesFeature.Addresses)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var addressUri))
            {
                continue;
            }

            var origin = FindLocalOrigin(addressUri);
            if (origin is not null)
            {
                return new Uri($"{origin}{SecurityPaths.BootstrapPage}#token={bootstrapToken.Value}");
            }
        }

        return null;
    }

    private static string? FindLocalOrigin(Uri addressUri)
    {
        if (LoopbackHost.IsLoopback(addressUri.Host))
        {
            return addressUri.GetLeftPart(UriPartial.Authority);
        }

        var hostText = addressUri.Host.Trim('[', ']');
        if (!IPAddress.TryParse(hostText, out var hostAddress))
        {
            return null;
        }

        if (hostAddress.Equals(IPAddress.Any))
        {
            return $"{addressUri.Scheme}://{IPAddress.Loopback}:{addressUri.Port}";
        }

        if (hostAddress.Equals(IPAddress.IPv6Any))
        {
            return $"{addressUri.Scheme}://[{IPAddress.IPv6Loopback}]:{addressUri.Port}";
        }

        return null;
    }
}
