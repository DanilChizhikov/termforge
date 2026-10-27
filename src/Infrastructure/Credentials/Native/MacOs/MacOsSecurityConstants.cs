using System.Runtime.InteropServices;

namespace DTech.Credentials.Native.MacOs;

internal sealed class MacOsSecurityConstants
{
    private MacOsSecurityConstants(IntPtr security, IntPtr coreFoundation)
    {
        Class = ReadPointer(security, "kSecClass");
        ClassGenericPassword = ReadPointer(security, "kSecClassGenericPassword");
        AttributeService = ReadPointer(security, "kSecAttrService");
        AttributeAccount = ReadPointer(security, "kSecAttrAccount");
        AttributeLabel = ReadPointer(security, "kSecAttrLabel");
        ValueData = ReadPointer(security, "kSecValueData");
        ReturnData = ReadPointer(security, "kSecReturnData");
        MatchLimit = ReadPointer(security, "kSecMatchLimit");
        MatchLimitOne = ReadPointer(security, "kSecMatchLimitOne");
        UseAuthenticationUi = ReadPointer(security, "kSecUseAuthenticationUI");
        UseAuthenticationUiFail = ReadPointer(security, "kSecUseAuthenticationUIFail");
        BooleanTrue = ReadPointer(coreFoundation, "kCFBooleanTrue");
        DictionaryKeyCallBacks = NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryKeyCallBacks");
        DictionaryValueCallBacks = NativeLibrary.GetExport(coreFoundation, "kCFTypeDictionaryValueCallBacks");
    }

    public IntPtr Class { get; }

    public IntPtr ClassGenericPassword { get; }

    public IntPtr AttributeService { get; }

    public IntPtr AttributeAccount { get; }

    public IntPtr AttributeLabel { get; }

    public IntPtr ValueData { get; }

    public IntPtr ReturnData { get; }

    public IntPtr MatchLimit { get; }

    public IntPtr MatchLimitOne { get; }

    public IntPtr UseAuthenticationUi { get; }

    public IntPtr UseAuthenticationUiFail { get; }

    public IntPtr BooleanTrue { get; }

    public IntPtr DictionaryKeyCallBacks { get; }

    public IntPtr DictionaryValueCallBacks { get; }

    public static MacOsSecurityConstants Load()
    {
        var security = NativeLibrary.Load(SecurityFramework.LibraryPath);
        var coreFoundation = NativeLibrary.Load(CoreFoundation.LibraryPath);
        return new MacOsSecurityConstants(security, coreFoundation);
    }

    private static IntPtr ReadPointer(IntPtr library, string symbol)
    {
        var address = NativeLibrary.GetExport(library, symbol);
        return Marshal.ReadIntPtr(address);
    }
}
