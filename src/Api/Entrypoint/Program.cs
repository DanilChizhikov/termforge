using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Security;
using DTech.Entrypoint.Security.Network;
using DTech.Entrypoint.SpaHosting;
using DTech.Persistence;
using DTech.Credentials.Native.Processes;
using DTech.Entrypoint.Updates.Installation;

if (await UpdateStartup.TryRunWorkerAsync(args))
{
    return;
}

if (await CredentialHelperCommand.TryRunAsync(args))
{
    return;
}

if (PasswordHashCommand.TryRun(args))
{
    return;
}

var publishedContentRoot = PublishedContentRoot.Find(AppContext.BaseDirectory);
var builderOptions = new WebApplicationOptions { Args = args, ContentRootPath = publishedContentRoot };
var builder = WebApplication.CreateBuilder(builderOptions);
builder.AddLocalHostSecurity();
var recoveryInstallation = Environment.GetEnvironmentVariable("TERMFORGE_UPDATE_MODE") == "recovery"
    ? Environment.GetEnvironmentVariable("TERMFORGE_UPDATE_INSTALLATION") : null;
var updateJournal = new UpdateJournal(recoveryInstallation ?? AppContext.BaseDirectory);
using var applicationLease = UpdateStartup.AcquireApplicationLease(updateJournal);
var recovery = UpdateStartup.FindRecovery(updateJournal);
if (recovery is not null)
{
    var oldApplication = Path.Combine(updateJournal.JobDirectory(recovery.Id), "old");
    if (Directory.Exists(oldApplication) && recovery.Phase is "switching" or "starting")
    {
        applicationLease?.Dispose();
        var worker = new UpdateWorker(updateJournal);
        await worker.RecoverAsync(recovery.Id, "The update was interrupted. Data recovery requires your confirmation.");
        return;
    }
    await UpdateRecoveryHost.RunAsync(builder, updateJournal, recovery);
    return;
}
builder.AddApplicationLifecycle();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddCredentialStore(builder.Configuration);
builder.Services.AddOperationJournal();
builder.Services.AddUpdateCheck(builder.Configuration);
builder.Services.AddHostKeys(builder.Configuration);
builder.Services.AddSavedConnections();
builder.Services.AddWriteSafety();
builder.Services.AddCapabilityDiagnostics();
builder.Services.AddNetworkDiagnostics();
builder.Services.AddCommandHistory();
builder.Services.AddServerMonitoring();
builder.Services.AddTerminals();
builder.Services.AddTerminalRecording(builder.Configuration);
builder.Services.AddFileBrowsing();
builder.Services.AddFileSearch();
builder.Services.AddRemoteFileEditor(builder.Configuration);
builder.Services.AddSudoFileEditor();
builder.Services.AddTransfers(builder.Configuration);
builder.Services.AddDockerManagement();
builder.Services.AddWorkspacePersistence();
builder.Services.AddSettingsTransfer();
builder.Services.AddAiBridge(builder.Configuration);

var app = builder.Build();
await app.MigrateDatabaseAsync();
await app.RecoverOperationsAsync();
app.UseLocalHostSecurity();
app.UseApplicationLifecycle();
app.MapApplicationLifecycle();
app.MapCredentialEndpoints();
app.MapRealtimeEndpoints();
app.MapOperationEndpoints();
app.MapUpdateEndpoints();
app.MapSavedConnectionEndpoints();
app.MapConfirmationEndpoints();
app.MapHostKeyEndpoints();
app.MapDiagnosticsEndpoints();
app.MapNetworkDiagnosticEndpoints();
app.MapCommandHistoryEndpoints();
app.MapMonitoringEndpoints();
app.MapTerminalEndpoints();
app.MapTerminalRecordingEndpoints();
app.MapFileBrowsingEndpoints();
app.MapFileSearchEndpoints();
app.MapEditorEndpoints();
app.MapSudoEditorEndpoints();
app.MapTransferEndpoints();
app.MapDockerEndpoints();
app.MapWorkspaceEndpoints();
app.MapSettingsTransferEndpoints();
app.MapAiBridgeEndpoints();
app.UseSpaHosting();
app.Lifetime.ApplicationStarted.Register(() =>
{
    try { UpdateStartup.ConfirmStartup(updateJournal); }
    catch (Exception exception)
    {
        app.Logger.LogError(exception, "Updated application startup was not accepted");
        Environment.ExitCode = 1;
        app.Lifetime.StopApplication();
    }
});

app.Run();
