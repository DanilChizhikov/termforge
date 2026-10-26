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
builder.Services.AddCapabilityDiagnostics();
builder.Services.AddServerMonitoring();
builder.Services.AddTerminals();
builder.Services.AddFileBrowsing();

var app = builder.Build();
await app.MigrateDatabaseAsync();
app.UseLocalHostSecurity();
app.MapApplicationLifecycle();
app.MapCredentialEndpoints();
app.MapRealtimeEndpoints();
app.MapOperationEndpoints();
app.MapUpdateEndpoints();
app.MapSavedConnectionEndpoints();
app.MapHostKeyEndpoints();
app.MapDiagnosticsEndpoints();
app.MapMonitoringEndpoints();
app.MapTerminalEndpoints();
app.MapFileBrowsingEndpoints();
app.UseSpaHosting();

app.Run();
