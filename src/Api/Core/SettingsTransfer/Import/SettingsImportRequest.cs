using DTech.Core.Credentials;

namespace DTech.Core.SettingsTransfer.Import;

public sealed record SettingsImportRequest(byte[] Package, string Key, SettingsImportOptions Options)
{
    public override string ToString()
    {
        return $"{nameof(SettingsImportRequest)} {{ {nameof(Package)} = {Package.Length} bytes, {nameof(Key)} = {Secret.Redacted}, {nameof(Options)} = {Options} }}";
    }
}
