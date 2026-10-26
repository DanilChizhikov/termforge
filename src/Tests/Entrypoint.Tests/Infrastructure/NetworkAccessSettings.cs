using NetworkPasswordHash = DTech.Entrypoint.Security.Network.PasswordHash;

namespace DTech.Entrypoint.Tests.Infrastructure;

internal static class NetworkAccessSettings
{
    public const string Password = "correct horse battery staple";
    public const string AllowedHost = "termforge.lan";
    public const string NetworkOrigin = "https://termforge.lan";
    public const string NetworkBaseAddress = "https://termforge.lan/";
    public const string LoginPath = "/api/session/login";
    public const string LogoutPath = "/api/session/logout";

    private static readonly Lazy<string> TestPasswordHash = new(CreatePasswordHash);

    public static string PasswordHash => TestPasswordHash.Value;

    public static Dictionary<string, string?> CreateEnabled()
    {
        return new Dictionary<string, string?>
        {
            ["Kestrel:Endpoints:Http:Url"] = "https://0.0.0.0:5023",
            ["NetworkAccess:Enabled"] = "true",
            ["NetworkAccess:PasswordHash"] = PasswordHash,
            ["NetworkAccess:AllowedHosts:0"] = AllowedHost,
        };
    }

    private static string CreatePasswordHash()
    {
        return NetworkPasswordHash.Create(Password, NetworkPasswordHash.MinimumIterations);
    }
}
