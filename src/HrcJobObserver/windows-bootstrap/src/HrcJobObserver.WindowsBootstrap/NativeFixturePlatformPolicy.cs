using System;
using System.Security;

namespace HrcJobObserver.WindowsBootstrap;

internal readonly record struct NativeFixtureWindowsVersion(
    uint Major,
    uint Minor,
    uint Build,
    uint PlatformId);

internal static class NativeFixturePlatformPolicy
{
    private const uint WindowsNtPlatformId = 2;
    private const uint MinimumMajor = 10;
    private const uint MinimumMinor = 0;
    private const uint MinimumBuild = 16_299;

    internal static void RequireWindows10Version1709OrLater()
    {
        RequireWindows10Version1709OrLater(ReadProductionVersion());
    }

    /// <summary>
    /// Pure policy seam for boundary tests. Runtime launch never supplies a
    /// substituted version and always calls the parameterless RtlGetVersion
    /// route.
    /// </summary>
    internal static void RequireWindows10Version1709OrLater(
        NativeFixtureWindowsVersion version)
    {
        bool supported = version.PlatformId == WindowsNtPlatformId &&
            (version.Major > MinimumMajor ||
                (version.Major == MinimumMajor &&
                    (version.Minor > MinimumMinor ||
                        (version.Minor == MinimumMinor &&
                            version.Build >= MinimumBuild))));
        if (!supported)
        {
            throw new PlatformNotSupportedException(
                "The native fixture requires Windows 10 version 1709 build 16299 or later.");
        }
    }

    internal static unsafe NativeFixtureWindowsVersion ReadProductionVersion()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        NativeMethods.OsVersionInfoEx value = default;
        value.Size = checked((uint)sizeof(NativeMethods.OsVersionInfoEx));
        int status = NativeMethods.RtlGetVersion(&value);
        if (status != 0 || value.Size != sizeof(NativeMethods.OsVersionInfoEx))
        {
            throw new SecurityException(
                $"RtlGetVersion failed closed with NTSTATUS 0x{status:X8}.");
        }

        return new NativeFixtureWindowsVersion(
            value.MajorVersion,
            value.MinorVersion,
            value.BuildNumber,
            value.PlatformId);
    }
}
