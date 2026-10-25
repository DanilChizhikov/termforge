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

            if (LoopbackHost.IsLoopback(addressUri.Host))
            {
                var origin = addressUri.GetLeftPart(UriPartial.Authority);
                return new Uri($"{origin}{SecurityPaths.BootstrapPage}#token={bootstrapToken.Value}");
            }
        }

        return null;
    }
}
