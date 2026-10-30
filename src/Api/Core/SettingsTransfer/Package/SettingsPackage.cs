using DTech.Core.SettingsTransfer.Secrets;

namespace DTech.Core.SettingsTransfer.Package;

public sealed class SettingsPackage(string settingsJson, SecretsArchiveContent secrets) : IDisposable
{
    public string SettingsJson { get; } = settingsJson;

    public SecretsArchiveContent Secrets { get; } = secrets;

    public void Dispose()
    {
        Secrets.Dispose();
    }

    public override string ToString()
    {
        return $"{nameof(SettingsPackage)} {{ {nameof(SettingsJson)} = {SettingsJson.Length} characters, {nameof(Secrets)} = {Secrets} }}";
    }
}
