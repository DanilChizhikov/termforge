using DTech.Core.SettingsTransfer.Document;
using DTech.Core.SettingsTransfer.Secrets;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class LoadedSettingsImport(SettingsDocument document, SecretsArchiveContent? secrets) : IDisposable
{
    public SettingsDocument Document { get; } = document;

    public SecretsArchiveContent? Secrets { get; } = secrets;

    public void Dispose()
    {
        Secrets?.Dispose();
    }
}
