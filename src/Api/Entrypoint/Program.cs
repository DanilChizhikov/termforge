using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Security;
using DTech.Entrypoint.SpaHosting;
using DTech.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddLocalHostSecurity();
builder.AddApplicationLifecycle();
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();
await app.MigrateDatabaseAsync();
app.UseLocalHostSecurity();
app.MapApplicationLifecycle();
app.UseSpaHosting();

app.Run();
