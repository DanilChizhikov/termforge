using System.Diagnostics;
using System.Reflection;

namespace DTech.Credentials.Native.Processes;

public sealed class CredentialHelperProcessFactory : ICredentialHelperProcessFactory
{
    public Process Start()
    {
        var target = FindTarget();
        var startInfo = CreateStartInfo(target);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("The credential helper could not be started.");
    }

    internal static ProcessStartInfo CreateStartInfo(CredentialHelperLaunchTarget target)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = target.Executable,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        if (target.AssemblyPath is not null)
        {
            startInfo.ArgumentList.Add(target.AssemblyPath);
        }

        startInfo.ArgumentList.Add(CredentialHelperCommand.Argument);
        return startInfo;
    }

    private static CredentialHelperLaunchTarget FindTarget()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("The application executable path is unavailable.");
        var entryName = Assembly.GetEntryAssembly()?.GetName().Name;
        var isDotnet = string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase);
        if (entryName == "TermForge" && !isDotnet)
        {
            return new CredentialHelperLaunchTarget(executable, null);
        }

        var assemblyPath = Path.Combine(AppContext.BaseDirectory, "TermForge.dll");
        if (!File.Exists(assemblyPath))
        {
            throw new InvalidOperationException("The TermForge credential helper assembly is unavailable.");
        }

        var host = isDotnet ? executable : "dotnet";
        return new CredentialHelperLaunchTarget(host, assemblyPath);
    }
}
