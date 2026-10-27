using DTech.Core.Credentials;

namespace DTech.Entrypoint.Credentials;

public sealed class UnlockVaultRequest
{
    public string? MasterPassword { get; set; }

    public override string ToString()
    {
        return $"{nameof(UnlockVaultRequest)} {{ {nameof(MasterPassword)} = {Secret.Redacted} }}";
    }
}
