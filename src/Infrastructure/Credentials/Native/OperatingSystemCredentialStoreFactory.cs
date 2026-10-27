using DTech.Credentials.Native.Linux;
using DTech.Credentials.Native.MacOs;
using DTech.Credentials.Native.Windows;

namespace DTech.Credentials.Native;

public static class OperatingSystemCredentialStoreFactory
{
    public static IOperatingSystemCredentialStore Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsCredentialManagerStore();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOsKeychainStore();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxSecretServiceStore();
        }

        return new UnsupportedPlatformCredentialStore();
    }
}
