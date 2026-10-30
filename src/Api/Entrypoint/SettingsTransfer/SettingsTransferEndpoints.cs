using DTech.Core.SettingsTransfer.Export;
using DTech.Core.SettingsTransfer.Import;
using DTech.Entrypoint.SettingsTransfer;

namespace Microsoft.AspNetCore.Builder;

public static class SettingsTransferEndpoints
{
    public const string RoutePrefix = "/api/settings-transfer";

    public static IEndpointRouteBuilder MapSettingsTransferEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/summary", GetSummary);
        group.MapPost("/export", Export);
        group.MapPost("/import/preview", Preview);
        group.MapPost("/import", Import);
        return endpoints;
    }

    private static async Task<IResult> GetSummary(SettingsTransferSummaryReader reader, CancellationToken cancellationToken)
    {
        var summary = await reader.ReadAsync(cancellationToken);
        return Results.Ok(summary);
    }

    private static async Task<IResult> Export(SettingsExportBody body, SettingsExportService service, ILogger<SettingsExportService> logger, CancellationToken cancellationToken)
    {
        var request = body.ToRequest();
        var export = await service.ExportAsync(request, cancellationToken);
        if (!export.IsSuccess)
        {
            logger.LogWarning($"Settings export failed: {export.Error.Code}");
            return SettingsTransferErrorResults.ToProblem(export.Error);
        }

        logger.LogInformation($"Settings exported with {export.Value.SecretCount} secrets");
        var response = SettingsExportResponse.From(export.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> Preview(SettingsImportBody body, SettingsImportService service, CancellationToken cancellationToken)
    {
        var request = SettingsImportRequestMapper.ToRequest(body);
        if (!request.IsSuccess)
        {
            return SettingsTransferErrorResults.ToProblem(request.Error);
        }

        var preview = await service.PreviewAsync(request.Value, cancellationToken);
        if (!preview.IsSuccess)
        {
            return SettingsTransferErrorResults.ToProblem(preview.Error);
        }

        var response = SettingsImportPreviewResponse.From(preview.Value);
        return Results.Ok(response);
    }

    private static async Task<IResult> Import(SettingsImportBody body, SettingsImportService service, ILogger<SettingsImportService> logger, CancellationToken cancellationToken)
    {
        var request = SettingsImportRequestMapper.ToRequest(body);
        if (!request.IsSuccess)
        {
            return SettingsTransferErrorResults.ToProblem(request.Error);
        }

        var result = await service.ApplyAsync(request.Value, cancellationToken);
        if (!result.IsSuccess)
        {
            logger.LogWarning($"Settings import failed: {result.Error.Code}");
            return SettingsTransferErrorResults.ToProblem(result.Error);
        }

        var imported = result.Value;
        logger.LogInformation($"Settings imported: {imported.Added} added, {imported.Updated} updated, {imported.Removed} removed, {imported.SecretsStored} secrets stored");
        return Results.Ok(imported);
    }
}
