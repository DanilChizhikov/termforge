namespace DTech.Ssh.IntegrationTests.Harness;

[TestClass]
public sealed class SshTestAssemblyLifecycle
{
    [AssemblyCleanup]
    public static Task CleanupAsync()
    {
        return SshTestEnvironment.StopAsync();
    }
}
