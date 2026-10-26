using System.Text.Json;
using DTech.Core.CommandHistory;
using DTech.Persistence.Database;
using DTech.Persistence.Settings;
using Microsoft.EntityFrameworkCore;

namespace DTech.Persistence.CommandHistory;

public sealed class CommandExclusionPatternStore(TermForgeDbContext context, TimeProvider timeProvider) : ICommandExclusionPatternStore
{
    public const string SettingKey = "command_history.exclusion_patterns";

    public async Task<IReadOnlyList<string>?> LoadAsync(CancellationToken cancellationToken)
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
            return JsonSerializer.Deserialize<List<string>>(setting.Value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(IReadOnlyList<string> patterns, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(patterns);
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
