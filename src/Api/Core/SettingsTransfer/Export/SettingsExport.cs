using DTech.Core.Credentials;

namespace DTech.Core.SettingsTransfer.Export;

public sealed record SettingsExport
{
    public required string FileName { get; init; }

    public required byte[] Package { get; init; }

    public required string Key { get; init; }

    public int SecretCount { get; init; }

    public override string ToString()
    {
        return $"{nameof(SettingsExport)} {{ {nameof(FileName)} = {FileName}, {nameof(Package)} = {Package.Length} bytes, {nameof(Key)} = {Secret.Redacted}, {nameof(SecretCount)} = {SecretCount} }}";
    }
}
