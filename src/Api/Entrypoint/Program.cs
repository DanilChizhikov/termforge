using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Security;
using DTech.Entrypoint.SpaHosting;
using DTech.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddLocalHostSecurity();
builder.AddApplicationLifecycle();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddCredentialStore(builder.Configuration);
builder.Services.AddOperationJournal();
builder.Services.AddUpdateCheck(builder.Configuration);
builder.Services.AddSavedConnections();

var app = builder.Build();
await app.MigrateDatabaseAsync();
app.UseLocalHostSecurity();
app.MapApplicationLifecycle();
app.MapCredentialEndpoints();
app.MapRealtimeEndpoints();
app.MapOperationEndpoints();
app.MapUpdateEndpoints();
app.MapSavedConnectionEndpoints();
app.UseSpaHosting();

app.Run();
