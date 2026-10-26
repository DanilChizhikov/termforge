using System.Net;
using DTech.Entrypoint.Security;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class BootstrapUrlFactory(IServer server, BootstrapToken bootstrapToken)
{
    public Uri? Create()
    {
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
