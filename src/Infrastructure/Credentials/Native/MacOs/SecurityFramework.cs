using System.Runtime.InteropServices;

namespace DTech.Credentials.Native.MacOs;

internal static partial class SecurityFramework
{
    public const string LibraryPath = "/System/Library/Frameworks/Security.framework/Security";
    public const int StatusSuccess = 0;
    public const int StatusItemNotFound = -25300;
    public const int StatusDuplicateItem = -25299;
    public const int StatusInteractionNotAllowed = -25308;
    public const int StatusNoSuchKeychain = -25294;
    public const int StatusNotAvailable = -25291;
    public const int StatusAuthFailed = -25293;
    public const int StatusUserCanceled = -128;

    [LibraryImport(LibraryPath)]
    public static partial int SecItemAdd(IntPtr attributes, IntPtr result);

    [LibraryImport(LibraryPath)]
    public static partial int SecItemCopyMatching(IntPtr query, out IntPtr result);

    [LibraryImport(LibraryPath)]
    public static partial int SecItemUpdate(IntPtr query, IntPtr attributesToUpdate);

    [LibraryImport(LibraryPath)]
    public static partial int SecItemDelete(IntPtr query);

    [LibraryImport(LibraryPath)]
    public static partial IntPtr SecCopyErrorMessageString(int status, IntPtr reserved);
}
