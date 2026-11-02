using System.Runtime.InteropServices;
using DTech.Credentials.Native.Linux;
using DTech.Credentials.Native.MacOs;
using DTech.Credentials.Tests.Support;
using FluentAssertions;

namespace DTech.Credentials.Tests.Native;

[TestClass]
public sealed class NativeCancellationTests
{
    [TestMethod]
    public void MacOsSharedQuery_ForbidsAuthenticationUiForEveryOperation()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Inconclusive("Keychain query verification requires macOS.");
            return;
        }

        var store = new MacOsKeychainStore();
        var key = TestKeys.Create("test-query");
        using var query = store.CreateItemQuery(key);
        var dictionary = query.Build();
        var constants = MacOsSecurityConstants.Load();

        var value = CoreFoundation.CFDictionaryGetValue(dictionary, constants.UseAuthenticationUi);

        value.Should().Be(constants.UseAuthenticationUiFail);
    }

    [TestMethod]
    public void LinuxCancellation_ReachesNativeCancellable()
    {
        if (!OperatingSystem.IsLinux() || !NativeLibrary.TryLoad(Gio.LibraryName, out var library))
        {
            Assert.Inconclusive("Native GIO cancellation verification requires Linux with libgio.");
            return;
        }

        NativeLibrary.Free(library);
        using var cancellation = new CancellationTokenSource();
        using var native = new GLibCancellation(cancellation.Token, TimeSpan.FromSeconds(10));
        Gio.CancellableIsCancelled(native.Handle).Should().Be(0);

        cancellation.Cancel();

        Gio.CancellableIsCancelled(native.Handle).Should().NotBe(0);
    }

    [TestMethod]
    public void LinuxDeadline_CancelsNativeOperation()
    {
        if (!OperatingSystem.IsLinux() || !NativeLibrary.TryLoad(Gio.LibraryName, out var library))
        {
            Assert.Inconclusive("Native GIO deadline verification requires Linux with libgio.");
            return;
        }

        NativeLibrary.Free(library);
        using var native = new GLibCancellation(CancellationToken.None, TimeSpan.FromMilliseconds(20));

        var cancelled = SpinWait.SpinUntil(() => Gio.CancellableIsCancelled(native.Handle) != 0, TimeSpan.FromSeconds(5));

        cancelled.Should().BeTrue();
    }
}
