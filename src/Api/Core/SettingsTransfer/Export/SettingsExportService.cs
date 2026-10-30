using System.Text.Json;
using DTech.Core.Credentials;
using DTech.Core.SettingsTransfer.Document;
using DTech.Core.SettingsTransfer.Package;

namespace DTech.Core.SettingsTransfer.Export;

public sealed class SettingsExportService
{
    private readonly SettingsDocumentBuilder _documentBuilder;
    private readonly ConnectionSecretsCollector _secretsCollector;
    private readonly ISettingsPackageCipher _cipher;
    private readonly ExportKeyGenerator _keyGenerator;
    private readonly TimeProvider _timeProvider;

    public SettingsExportService(
        SettingsDocumentBuilder documentBuilder,
        ConnectionSecretsCollector secretsCollector,
        ISettingsPackageCipher cipher,
        ExportKeyGenerator keyGenerator,
        TimeProvider timeProvider)
    {
        _documentBuilder = documentBuilder;
        _secretsCollector = secretsCollector;
        _cipher = cipher;
        _keyGenerator = keyGenerator;
        _timeProvider = timeProvider;
    }

    public async Task<SettingsTransferResult<SettingsExport>> ExportAsync(SettingsExportRequest request, CancellationToken cancellationToken)
    {
        if (!request.Sections.Any)
        {
            var invalid = SettingsTransferError.Invalid("Choose at least one part of the settings to export.");
            return SettingsTransferResult<SettingsExport>.Failure(invalid);
        }

        var exportedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var exportId = Guid.NewGuid();
        var header = new SettingsDocumentHeader(exportId, exportedAt);
        var document = await _documentBuilder.BuildAsync(header, request.Sections, cancellationToken);
        var connectionIds = CollectConnectionIds(document);
        var secrets = await _secretsCollector.CollectAsync(exportId, connectionIds, cancellationToken);
        if (!secrets.IsSuccess)
        {
            return SettingsTransferResult<SettingsExport>.Failure(secrets.Error);
        }

        var settingsJson = JsonSerializer.Serialize(document, SettingsDocumentSchema.JsonOptions);
        using var package = new SettingsPackage(settingsJson, secrets.Value);
        var keyText = _keyGenerator.Generate();
        var encrypted = Encrypt(package, keyText);
        var export = new SettingsExport
        {
            FileName = SettingsExportFileNames.Package(exportedAt),
            Package = encrypted,
            Key = keyText,
            SecretCount = package.Secrets.Secrets.Count,
        };
        return SettingsTransferResult<SettingsExport>.Success(export);
    }

    private byte[] Encrypt(SettingsPackage package, string keyText)
    {
        var normalizedKey = ExportKeyNormalizer.Normalize(keyText)!;
        using var key = Secret.FromString(normalizedKey);
        return _cipher.Encrypt(package, key);
    }

    private static List<Guid> CollectConnectionIds(SettingsDocument document)
    {
        var connections = document.Connections ?? [];
        var connectionIds = new List<Guid>(connections.Count);
        for (var i = 0; i < connections.Count; i++)
        {
            connectionIds.Add(connections[i].Id);
        }

        return connectionIds;
    }
}
