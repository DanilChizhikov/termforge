using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DTech.Core.Credentials;

namespace DTech.Credentials.Native.MacOs;

[SupportedOSPlatform("macos")]
public sealed unsafe class MacOsKeychainStore : IOperatingSystemCredentialStore
{
    private const string ServiceName = "termforge";

    private readonly Lazy<MacOsSecurityConstants> _constants = new(MacOsSecurityConstants.Load);

    public string Name => "macOS Keychain";

    public CredentialStoreAvailability CheckPrerequisites()
    {
        if (!NativeLibrary.TryLoad(SecurityFramework.LibraryPath, out _))
        {
            return CredentialStoreAvailability.Unavailable(Name, $"Security.framework could not be loaded from {SecurityFramework.LibraryPath}.");
        }

        return CredentialStoreAvailability.Available(Name);
    }

    public Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        var result = StoreItem(key, secret);
        return Task.FromResult(result);
    }

    public Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        var result = ReadItem(key);
        return Task.FromResult(result);
    }

    public Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        var result = DeleteItem(key);
        return Task.FromResult(result);
    }

    private CredentialResult StoreItem(CredentialKey key, Secret secret)
    {
        var constants = _constants.Value;
        var secretBytes = secret.AsSpan();
        using var attributes = CreateItemQuery(key);
        attributes.AddString(constants.AttributeLabel, $"{ServiceName}: {key.Value}");
        attributes.AddData(constants.ValueData, secretBytes);
        var attributesDictionary = attributes.Build();
        var addStatus = SecurityFramework.SecItemAdd(attributesDictionary, IntPtr.Zero);
        if (addStatus == SecurityFramework.StatusSuccess)
        {
            return CredentialResult.Success();
        }

        if (addStatus != SecurityFramework.StatusDuplicateItem)
        {
            var addError = MapStatus(addStatus, key);
            return CredentialResult.Failure(addError);
        }

        using var query = CreateItemQuery(key);
        var queryDictionary = query.Build();
        using var update = new CoreFoundationDictionaryBuilder(constants);
        update.AddData(constants.ValueData, secretBytes);
        var updateDictionary = update.Build();
        var updateStatus = SecurityFramework.SecItemUpdate(queryDictionary, updateDictionary);
        if (updateStatus == SecurityFramework.StatusSuccess)
        {
            return CredentialResult.Success();
        }

        var updateError = MapStatus(updateStatus, key);
        return CredentialResult.Failure(updateError);
    }

    private CredentialResult<Secret> ReadItem(CredentialKey key)
    {
        var constants = _constants.Value;
        using var query = CreateItemQuery(key);
        query.Add(constants.ReturnData, constants.BooleanTrue);
        query.Add(constants.MatchLimit, constants.MatchLimitOne);
        query.Add(constants.UseAuthenticationUi, constants.UseAuthenticationUiFail);
        var queryDictionary = query.Build();
        var status = SecurityFramework.SecItemCopyMatching(queryDictionary, out var data);
        if (status != SecurityFramework.StatusSuccess)
        {
            var error = MapStatus(status, key);
            return CredentialResult<Secret>.Failure(error);
        }

        try
        {
            var bytes = CoreFoundation.CFDataGetBytePtr(data);
            var length = (int)CoreFoundation.CFDataGetLength(data);
            var secretBytes = new ReadOnlySpan<byte>(bytes, length);
            var secret = Secret.FromBytes(secretBytes);
            return CredentialResult<Secret>.Success(secret);
        }
        finally
        {
            CoreFoundation.CFRelease(data);
        }
    }

    private CredentialResult DeleteItem(CredentialKey key)
    {
        using var query = CreateItemQuery(key);
        var queryDictionary = query.Build();
        var status = SecurityFramework.SecItemDelete(queryDictionary);
        if (status == SecurityFramework.StatusSuccess)
        {
            return CredentialResult.Success();
        }

        var error = MapStatus(status, key);
        return CredentialResult.Failure(error);
    }

    private CoreFoundationDictionaryBuilder CreateItemQuery(CredentialKey key)
    {
        var constants = _constants.Value;
        var builder = new CoreFoundationDictionaryBuilder(constants);
        builder.Add(constants.Class, constants.ClassGenericPassword);
        builder.AddString(constants.AttributeService, ServiceName);
        builder.AddString(constants.AttributeAccount, key.Value);
        return builder;
    }

    private static CredentialError MapStatus(int status, CredentialKey key)
    {
        if (status == SecurityFramework.StatusItemNotFound)
        {
            return CredentialErrors.NotFound(key);
        }

        var statusMessage = DescribeStatus(status);
        if (status is SecurityFramework.StatusInteractionNotAllowed or SecurityFramework.StatusAuthFailed or SecurityFramework.StatusUserCanceled)
        {
            return CredentialErrors.StoreUnavailable($"The macOS Keychain refused access ({status}: {statusMessage}). Unlock the login keychain (for example with 'security unlock-keychain') and allow access for this application.");
        }

        if (status is SecurityFramework.StatusNoSuchKeychain or SecurityFramework.StatusNotAvailable)
        {
            return CredentialErrors.StoreUnavailable($"No usable macOS keychain was found ({status}: {statusMessage}). Create or restore the login keychain in Keychain Access.");
        }

        return CredentialErrors.StoreFailure($"macOS Keychain error {status}: {statusMessage}");
    }

    private static string DescribeStatus(int status)
    {
        var message = SecurityFramework.SecCopyErrorMessageString(status, IntPtr.Zero);
        if (message == IntPtr.Zero)
        {
            return "unknown error";
        }

        try
        {
            var length = CoreFoundation.CFStringGetLength(message);
            var bufferSize = CoreFoundation.CFStringGetMaximumSizeForEncoding(length, CoreFoundation.StringEncodingUtf8) + 1;
            var buffer = new byte[bufferSize];
            fixed (byte* bufferPointer = buffer)
            {
                if (!CoreFoundation.CFStringGetCString(message, bufferPointer, bufferSize, CoreFoundation.StringEncodingUtf8))
                {
                    return "unknown error";
                }

                return Marshal.PtrToStringUTF8((IntPtr)bufferPointer) ?? "unknown error";
            }
        }
        finally
        {
            CoreFoundation.CFRelease(message);
        }
    }
}
