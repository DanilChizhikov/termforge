using DTech.Core.Credentials;

namespace DTech.Entrypoint.SavedConnections;

public sealed class SavedConnectionPasswordResponse
{
    public required string Password { get; init; }

    public override string ToString()
    {
        return $"{nameof(SavedConnectionPasswordResponse)} {{ {nameof(Password)} = {Secret.Redacted} }}";
    }
}
