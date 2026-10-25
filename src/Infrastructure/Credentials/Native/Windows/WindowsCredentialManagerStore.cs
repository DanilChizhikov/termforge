using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using DTech.Core.Credentials;

namespace DTech.Credentials.Native.Windows;

[SupportedOSPlatform("windows")]
public sealed unsafe class WindowsCredentialManagerStore : IOperatingSystemCredentialStore
{
    private const string TargetPrefix = "termforge:";

    public string Name => "Windows Credential Manager";

    public CredentialStoreAvailability CheckPrerequisites()
    {
        return CredentialStoreAvailability.Available(Name);
    }

    public Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        var result = StoreCredential(key, secret);
        return Task.FromResult(result);
    }

    public Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        var result = ReadCredential(key);
        return Task.FromResult(result);
    }

    public Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        var result = DeleteCredential(key);
        return Task.FromResult(result);
    }

    private static string CreateTargetName(CredentialKey key)
    {
        return $"{TargetPrefix}{key.Value}";
    }

    private static CredentialResult StoreCredential(CredentialKey key, Secret secret)
    {
        if (secret.Length > AdvApi32.MaxCredentialBlobSize)
        {
            var sizeError = CredentialErrors.InvalidSecret($"Windows Credential Manager accepts at most {AdvApi32.MaxCredentialBlobSize} bytes.");
            return CredentialResult.Failure(sizeError);
        }

        var targetName = CreateTargetName(key);
        var userName = key.Value;
        var secretBytes = secret.AsSpan();
        fixed (char* targetPointer = targetName)
        fixed (char* userPointer = userName)
        fixed (byte* blobPointer = secretBytes)
        {
            var credential = new AdvApi32.NativeCredential
            {
                Type = AdvApi32.CredentialTypeGeneric,
                TargetName = targetPointer,
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = blobPointer,
                Persist = AdvApi32.CredentialPersistLocalMachine,
                UserName = userPointer,
            };
            if (AdvApi32.CredWrite(&credential, 0))
            {
                return CredentialResult.Success();
            }
        }

        var errorCode = Marshal.GetLastPInvokeError();
        var error = MapError(errorCode, key);
        return CredentialResult.Failure(error);
    }

    private static CredentialResult<Secret> ReadCredential(CredentialKey key)
    {
        var targetName = CreateTargetName(key);
        if (!AdvApi32.CredRead(targetName, AdvApi32.CredentialTypeGeneric, 0, out var credential))
        {
            var errorCode = Marshal.GetLastPInvokeError();
            var error = MapError(errorCode, key);
            return CredentialResult<Secret>.Failure(error);
        }

        try
        {
            var blobLength = (int)credential->CredentialBlobSize;
            var blob = new Span<byte>(credential->CredentialBlob, blobLength);
            var secret = Secret.FromBytes(blob);
            CryptographicOperations.ZeroMemory(blob);
            return CredentialResult<Secret>.Success(secret);
        }
        finally
        {
            AdvApi32.CredFree(credential);
        }
    }

    private static CredentialResult DeleteCredential(CredentialKey key)
    {
        var targetName = CreateTargetName(key);
        if (AdvApi32.CredDelete(targetName, AdvApi32.CredentialTypeGeneric, 0))
        {
            return CredentialResult.Success();
        }

        var errorCode = Marshal.GetLastPInvokeError();
        var error = MapError(errorCode, key);
        return CredentialResult.Failure(error);
    }

    private static CredentialError MapError(int errorCode, CredentialKey key)
    {
        if (errorCode == AdvApi32.ErrorNotFound)
        {
            return CredentialErrors.NotFound(key);
        }

        var systemMessage = Marshal.GetPInvokeErrorMessage(errorCode);
        if (errorCode == AdvApi32.ErrorNoSuchLogonSession)
        {
            return CredentialErrors.StoreUnavailable($"Windows Credential Manager is not available for this logon session (error {errorCode}: {systemMessage}). Run the application from an interactive user session.");
        }

        return CredentialErrors.StoreFailure($"Windows Credential Manager error {errorCode}: {systemMessage}");
    }
}
