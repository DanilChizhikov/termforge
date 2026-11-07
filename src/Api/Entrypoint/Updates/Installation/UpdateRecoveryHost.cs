using System.Diagnostics;
using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Security;

namespace DTech.Entrypoint.Updates.Installation;

internal static class UpdateRecoveryHost
{
    public static async Task RunAsync(WebApplicationBuilder builder, UpdateJournal journal, UpdateJob job)
    {
        builder.Services.Configure<LifecycleOptions>(builder.Configuration.GetSection(LifecycleOptions.SectionName));
        builder.Services.AddSingleton<IBrowserLauncher, SystemBrowserLauncher>();
        builder.Services.AddSingleton<BootstrapUrlFactory>();
        builder.Services.AddSingleton<BootstrapRedirectFile>();
        builder.Services.AddHostedService<BrowserOpeningService>();
        var app = builder.Build();
        app.UseLocalHostSecurity();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/mcp")) context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            else await next(context);
        });
        app.MapGet("/", () => Results.Content(RecoveryPage.Html, "text/html"));
        app.MapGet("/recovery.js", () => Results.Text(RecoveryPage.Script, "text/javascript"));
        app.MapGet("/recovery.css", () =>
        {
            var assets = Path.Combine(job.InstallationDirectory, "wwwroot", "assets");
            var stylesheet = Directory.Exists(assets) ? Directory.GetFiles(assets, "*.css").FirstOrDefault() : null;
            return stylesheet is null ? Results.Text("", "text/css") : Results.File(stylesheet, "text/css");
        });
        app.MapGet("/api/updates/recovery", () => new { error = job.Error ?? "An update was interrupted.", backupRequired = job.DataBackupComplete, backupAvailable = File.Exists(Path.Combine(journal.JobDirectory(job.Id), "data", "complete")) });
        var gate = new Lock();
        var restoring = false;
        app.MapPost("/api/updates/restore", (RollbackUpdateRequest request, HttpContext context) =>
        {
            lock (gate)
            {
                if (restoring) return Results.Conflict();
                using var process = Process.GetCurrentProcess();
                var restore = job with
                {
                    Mode = "restore", Phase = "stopping", SaveCurrentData = request.SaveCurrentData,
                    ParentProcessId = process.Id, ParentStartTicks = process.StartTime.ToUniversalTime().Ticks,
                    ChildProcessId = 0, ChildStartTicks = 0,
                };
                journal.Save(restore);
                UpdateProcess.LaunchWorker(journal, restore);
                restoring = true;
                context.Response.OnCompleted(() => { app.Lifetime.StopApplication(); return Task.CompletedTask; });
                return Results.Accepted();
            }
        });
        await app.RunAsync();
    }
}
