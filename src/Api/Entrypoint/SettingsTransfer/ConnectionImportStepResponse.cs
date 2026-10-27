using System.Text.Json;
using DTech.Core.SettingsTransfer.Import;

namespace DTech.Entrypoint.SettingsTransfer;

public sealed record ConnectionImportStepResponse
{
    public required Guid ImportedId { get; init; }

    public required string Name { get; init; }

    public required string Host { get; init; }

    public required string UserName { get; init; }

    public int Port { get; init; }

    public required string Action { get; init; }

    public string? Resolution { get; init; }

    public required string LocalName { get; init; }

    public required IReadOnlyList<string> Changes { get; init; }

    public bool HasPassword { get; init; }

    public bool HasSudoPassword { get; init; }

    public static ConnectionImportStepResponse From(ConnectionImportStep step, SettingsImportPreview preview)
    {
        var importedId = step.Imported.Id;
        var actionName = step.Action.ToString();
        var action = JsonNamingPolicy.CamelCase.ConvertName(actionName);
        var resolutionName = step.Resolution.ToString();
        var resolution = step.Action == ConnectionImportAction.Conflict ? JsonNamingPolicy.CamelCase.ConvertName(resolutionName) : null;
        return new ConnectionImportStepResponse
        {
            ImportedId = importedId,
            Name = step.Imported.Name!.Trim(),
            Host = step.Draft.Host,
            UserName = step.Draft.UserName,
            Port = step.Draft.Port,
            Action = action,
            Resolution = resolution,
            LocalName = step.Draft.Name,
            Changes = step.Changes,
            HasPassword = preview.ConnectionsWithPassword.Contains(importedId),
            HasSudoPassword = preview.ConnectionsWithSudoPassword.Contains(importedId),
        };
    }
}
