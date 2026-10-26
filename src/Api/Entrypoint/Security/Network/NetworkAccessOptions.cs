namespace DTech.Entrypoint.Security.Network;

internal sealed class NetworkAccessOptions
{
    public const string SectionName = "NetworkAccess";

    public bool Enabled { get; set; }

    public string? PasswordHash { get; set; }

    public List<string> AllowedHosts { get; set; } = [];

    public bool AllowInsecureHttp { get; set; }

    public int MaxFailedLoginAttempts { get; set; } = 5;

    public int GlobalMaxFailedLoginAttempts { get; set; } = 20;

    public int LockoutMinutes { get; set; } = 15;

    public int SessionLifetimeHours { get; set; } = 12;
}
