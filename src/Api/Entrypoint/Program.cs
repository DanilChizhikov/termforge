using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddLocalHostSecurity();
builder.AddApplicationLifecycle();

var app = builder.Build();
app.UseLocalHostSecurity();
app.MapApplicationLifecycle();

app.MapGet("/", () => "TermForge");

app.Run();
