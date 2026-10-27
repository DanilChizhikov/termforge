using System.Text.Json;
using DTech.Core.Terminal.Recording;
using DTech.Persistence.Database;
using Microsoft.EntityFrameworkCore;

namespace DTech.Persistence.Settings;

public sealed class TerminalRecordingSettingsStore(TermForgeDbContext context, TimeProvider timeProvider) : ITerminalRecordingSettingsStore
{
    public const string SettingKey = "terminal_recording.settings";

    public async Task<TerminalRecordingSettings?> LoadAsync(CancellationToken cancellationToken)
    {
        var setting = await context.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Key == SettingKey, cancellationToken);
        if (setting is null)
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<TerminalRecordingSettingsDocument>(setting.Value);
            return document?.ToSettings();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(TerminalRecordingSettings settings, CancellationToken cancellationToken)
    {
        var document = TerminalRecordingSettingsDocument.From(settings);
        var json = JsonSerializer.Serialize(document);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var setting = await context.AppSettings.FirstOrDefaultAsync(item => item.Key == SettingKey, cancellationToken);
        if (setting is null)
        {
            setting = new AppSetting { Key = SettingKey, Value = json, UpdatedAt = now };
            context.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = json;
            setting.UpdatedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
        context.Entry(setting).State = EntityState.Detached;
    }
}
