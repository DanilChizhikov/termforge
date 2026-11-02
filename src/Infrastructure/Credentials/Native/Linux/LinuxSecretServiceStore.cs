using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Unicode;
using DTech.Core.Credentials;

namespace DTech.Credentials.Native.Linux;

[SupportedOSPlatform("linux")]
public sealed unsafe class LinuxSecretServiceStore(TimeSpan? operationTimeout = null) : IOperatingSystemCredentialStore
{
    private const string SessionBusAddressVariable = "DBUS_SESSION_BUS_ADDRESS";
    private const string RuntimeDirectoryVariable = "XDG_RUNTIME_DIR";
    private const string ProviderHint = "Make sure a Secret Service provider (GNOME Keyring, KWallet with Secret Service support or KeePassXC) is running in this session and its default collection is unlocked.";

    public string Name => "Secret Service (libsecret)";

    public CredentialStoreAvailability CheckPrerequisites()
    {
        if (!NativeLibrary.TryLoad(LibSecret.LibraryName, out var handle))
        {
            return CredentialStoreAvailability.Unavailable(Name, $"{LibSecret.LibraryName} is not installed. Install libsecret (Debian/Ubuntu: 'sudo apt install libsecret-1-0', Fedora: 'sudo dnf install libsecret'). {ProviderHint}");
        }

        NativeLibrary.Free(handle);
        if (!HasSessionBus())
        {
            return CredentialStoreAvailability.Unavailable(Name, $"No D-Bus session bus was found ({SessionBusAddressVariable} is not set and ${RuntimeDirectoryVariable}/bus does not exist). Run the application inside a desktop session or 'dbus-run-session'. {ProviderHint}");
        }

        return CredentialStoreAvailability.Available(Name);
    }

    public Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = StorePassword(key, secret, cancellationToken);
        return Task.FromResult(result);
    }

    public Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = LookupPassword(key, cancellationToken);
        return Task.FromResult(result);
    }

    public Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = ClearPassword(key, cancellationToken);
        return Task.FromResult(result);
    }

    private static bool HasSessionBus()
    {
        var address = Environment.GetEnvironmentVariable(SessionBusAddressVariable);
        if (!string.IsNullOrEmpty(address))
        {
            return true;
        }

        var runtimeDirectory = Environment.GetEnvironmentVariable(RuntimeDirectoryVariable);
        if (string.IsNullOrEmpty(runtimeDirectory))
        {
            return false;
        }

        var busPath = Path.Combine(runtimeDirectory, "bus");
        return File.Exists(busPath);
    }

    private CredentialResult StorePassword(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        var secretBytes = secret.AsSpan();
        if (secretBytes.Contains((byte)0) || !Utf8.IsValid(secretBytes))
        {
            var textError = CredentialErrors.InvalidSecret("Secret Service stores text secrets only: the value must be valid UTF-8 without NUL characters.");
            return CredentialResult.Failure(textError);
        }

        var schema = LinuxSecretSchema.Instance;
        var timeout = operationTimeout ?? TimeSpan.FromSeconds(10);
        using var cancellation = new GLibCancellation(cancellationToken, timeout);
        using var attributes = new GLibAttributeTable(LinuxSecretSchema.KeyAttribute, key.Value);
        var label = Marshal.StringToCoTaskMemUTF8($"termforge: {key.Value}");
        var passwordLength = (nuint)secretBytes.Length + 1;
        var password = (byte*)NativeMemory.AllocZeroed(passwordLength);
        try
        {
            var passwordSpan = new Span<byte>(password, secretBytes.Length);
            secretBytes.CopyTo(passwordSpan);
            GLib.Error* error = null;
            var stored = LibSecret.StorePassword(schema.Pointer, attributes.Handle, null, (byte*)label, password, cancellation.Handle, &error);
            cancellation.ThrowIfCancellationRequested(error);
            if (stored != 0)
            {
                return CredentialResult.Success();
            }

            var failure = TakeError(error);
            return CredentialResult.Failure(failure);
        }
        finally
        {
            NativeMemory.Clear(password, passwordLength);
            NativeMemory.Free(password);
            Marshal.FreeCoTaskMem(label);
        }
    }

    private CredentialResult<Secret> LookupPassword(CredentialKey key, CancellationToken cancellationToken)
    {
        var schema = LinuxSecretSchema.Instance;
        var timeout = operationTimeout ?? TimeSpan.FromSeconds(10);
        using var cancellation = new GLibCancellation(cancellationToken, timeout);
        using var attributes = new GLibAttributeTable(LinuxSecretSchema.KeyAttribute, key.Value);
        GLib.Error* error = null;
        var password = LibSecret.LookupPassword(schema.Pointer, attributes.Handle, cancellation.Handle, &error);
        try
        {
            cancellation.ThrowIfCancellationRequested(error);
            if (error is not null)
            {
                var failure = TakeError(error);
                return CredentialResult<Secret>.Failure(failure);
            }

            if (password is null)
            {
                var notFound = CredentialErrors.NotFound(key);
                return CredentialResult<Secret>.Failure(notFound);
            }

            var passwordBytes = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(password);
            var secret = Secret.FromBytes(passwordBytes);
            return CredentialResult<Secret>.Success(secret);
        }
        finally
        {
            if (password is not null)
            {
                LibSecret.FreePassword(password);
            }
        }
    }

    private CredentialResult ClearPassword(CredentialKey key, CancellationToken cancellationToken)
    {
        var schema = LinuxSecretSchema.Instance;
        var timeout = operationTimeout ?? TimeSpan.FromSeconds(10);
        using var cancellation = new GLibCancellation(cancellationToken, timeout);
        using var attributes = new GLibAttributeTable(LinuxSecretSchema.KeyAttribute, key.Value);
        GLib.Error* error = null;
        var cleared = LibSecret.ClearPassword(schema.Pointer, attributes.Handle, cancellation.Handle, &error);
        cancellation.ThrowIfCancellationRequested(error);
        if (error is not null)
        {
            var failure = TakeError(error);
            return CredentialResult.Failure(failure);
        }

        if (cleared == 0)
        {
            var notFound = CredentialErrors.NotFound(key);
            return CredentialResult.Failure(notFound);
        }

        return CredentialResult.Success();
    }

    private static CredentialError TakeError(GLib.Error* error)
    {
        if (error is null)
        {
            return CredentialErrors.StoreFailure($"Secret Service reported an unknown error. {ProviderHint}");
        }

        try
        {
            var message = Marshal.PtrToStringUTF8((IntPtr)error->Message) ?? "unknown error";
            return CredentialErrors.StoreFailure($"Secret Service error {error->Domain}/{error->Code}: {message}. {ProviderHint}");
        }
        finally
        {
            GLib.ErrorFree(error);
        }
    }
}
