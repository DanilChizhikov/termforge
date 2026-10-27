using DTech.Core.SavedConnections;

namespace DTech.Core.SettingsTransfer.Import;

public sealed class SettingsImportPreparer(SettingsImportLoader loader, ISavedConnectionRepository repository, PreferencesImportPlanner preferencesPlanner)
{
    public async Task<SettingsTransferResult<PreparedSettingsImport>> PrepareAsync(SettingsImportRequest request, CancellationToken cancellationToken)
    {
        var loaded = loader.Load(request);
        if (!loaded.IsSuccess)
        {
            return SettingsTransferResult<PreparedSettingsImport>.Failure(loaded.Error);
        }

        var import = loaded.Value;
        var document = import.Document;
        var options = request.Options;
        var sectionsInFile = document.IncludedSections();
        var sections = options.Sections.Intersect(sectionsInFile);
        var effectiveOptions = options with { Sections = sections };
        var existing = await repository.ListAsync(cancellationToken);
        var plan = SettingsImportPlanner.Plan(document, existing, effectiveOptions);
        if (!plan.IsSuccess)
        {
            import.Dispose();
            return SettingsTransferResult<PreparedSettingsImport>.Failure(plan.Error);
        }

        PreferencesImportPlan? preferences = null;
        if (sections.Preferences)
        {
            preferences = await preferencesPlanner.PlanAsync(document.Preferences!, options.Mode, cancellationToken);
        }

        var prepared = new PreparedSettingsImport(import, plan.Value, sections) { Preferences = preferences };
        return SettingsTransferResult<PreparedSettingsImport>.Success(prepared);
    }
}
