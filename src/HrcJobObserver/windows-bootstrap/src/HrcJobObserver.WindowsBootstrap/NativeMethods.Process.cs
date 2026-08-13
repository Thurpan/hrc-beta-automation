using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class NativeMethods
{
    internal const uint ProcessQueryLimitedInformation = 0x1000;
    internal const uint Synchronize = 0x00100000;
    internal const uint TokenQuery = 0x0008;
    internal const uint WaitObject0 = 0x00000000;
    internal const uint WaitTimeout = 0x00000102;
    internal const int TokenUser = 1;
    internal const int TokenSessionId = 12;
    internal const int TokenLogonSid = 28;
    internal const uint SeGroupLogonId = 0xC0000000;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial nint OpenProcess(
        uint desiredAccess,
        int inheritHandle,
        uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int GetProcessTimes(
        SafeProcessHandle process,
        out FileTime creation,
        out FileTime exit,
        out FileTime kernel,
        out FileTime user);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "QueryFullProcessImageNameW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static unsafe partial int QueryFullProcessImageName(
        SafeProcessHandle process,
        uint flags,
        char* imagePath,
        ref uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int ProcessIdToSessionId(
        uint processId,
        out uint sessionId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint WaitForSingleObject(
        SafeHandle handle,
        uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial int CloseHandle(nint handle);

    [LibraryImport("kernel32.dll")]
    internal static partial nint LocalFree(nint memory);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    internal static partial int OpenProcessToken(
        SafeProcessHandle process,
        uint desiredAccess,
        out nint token);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    internal static partial int GetTokenInformation(
        SafeTokenHandle token,
        int informationClass,
        nint information,
        uint informationLength,
        out uint returnLength);

    [LibraryImport(
        "advapi32.dll",
        EntryPoint = "ConvertSidToStringSidW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int ConvertSidToStringSid(
        nint sid,
        out nint stringSid);

    internal static Win32Exception Win32Failure(string operation)
    {
        return new Win32Exception(Marshal.GetLastWin32Error(), operation);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct FileTime
    {
        internal readonly uint Low;
        internal readonly uint High;

        internal ulong ToUInt64()
        {
            return ((ulong)High << 32) | Low;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct SidAndAttributes
    {
        internal readonly nint Sid;
        internal readonly uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct TokenUserValue
    {
        internal readonly SidAndAttributes User;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct TokenGroupsFirst
    {
        internal readonly uint GroupCount;
        internal readonly SidAndAttributes FirstGroup;
    }

    internal sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeProcessHandle()
            : base(true)
        {
        }

        internal SafeProcessHandle(nint handle)
            : base(true)
        {
            SetHandle(handle);
        }

        internal void Initialize(nint value)
        {
            if (!IsInvalid || IsClosed || value == 0 || value == -1)
            {
                throw new InvalidOperationException(
                    "The process handle cannot be initialized twice or from an invalid value.");
            }

            SetHandle(value);
        }

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle) != 0;
        }
    }

    internal sealed class SafeTokenHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeTokenHandle(nint handle)
            : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle) != 0;
        }
    }
}
