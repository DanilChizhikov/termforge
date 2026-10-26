namespace DTech.Core.SshConfig;

public sealed record SshConfigEnvironment(string HomeDirectory, string UserName)
{
    public string DefaultConfigPath => Path.Combine(HomeDirectory, ".ssh", "config");

    public string SshDirectory => Path.Combine(HomeDirectory, ".ssh");

    public static SshConfigEnvironment ForCurrentUser()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new SshConfigEnvironment(home, Environment.UserName);
    }
}
