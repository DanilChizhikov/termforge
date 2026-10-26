using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Security.Network;

internal sealed class NetworkPasswordVerifier
{
    private readonly PasswordHash? _passwordHash;

    public NetworkPasswordVerifier(IOptions<NetworkAccessOptions> options)
    {
        var configuredHash = options.Value.PasswordHash;
        _passwordHash = PasswordHash.TryParse(configuredHash, out var passwordHash) ? passwordHash : null;
    }

    public bool Verify(string? password)
    {
        return _passwordHash is not null && _passwordHash.Verify(password);
    }
}
