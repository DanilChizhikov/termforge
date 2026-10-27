using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Security;
using DTech.Entrypoint.Security.Network;
using DTech.Entrypoint.SpaHosting;
using DTech.Persistence;

if (PasswordHashCommand.TryRun(args))
{
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.AddLocalHostSecurity();
builder.AddApplicationLifecycle();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddCredentialStore(builder.Configuration);
builder.Services.AddOperationJournal();
builder.Services.AddUpdateCheck(builder.Configuration);
builder.Services.AddHostKeys(builder.Configuration);
builder.Services.AddSavedConnections();
builder.Services.AddWriteSafety();
builder.Services.AddCapabilityDiagnostics();
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

var app = builder.Build();
await app.MigrateDatabaseAsync();
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
app.UseSpaHosting();

app.Run();
