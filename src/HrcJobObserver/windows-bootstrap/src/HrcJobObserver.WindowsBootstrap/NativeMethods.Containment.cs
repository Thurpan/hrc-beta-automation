using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class NativeMethods
{
    internal const uint CreateSuspended = 0x00000004;
    internal const uint CreateDefaultErrorMode = 0x04000000;
    internal const uint CreateNoWindow = 0x08000000;
    internal const uint CreateUnicodeEnvironment = 0x00000400;
    internal const uint ExtendedStartupInfoPresent = 0x00080000;
    internal const uint JobObjectLimitKillOnJobClose = 0x00002000;
    internal const int JobObjectBasicProcessIdList = 3;
    internal const int JobObjectExtendedLimitInformation = 9;
    internal const nuint ProcThreadAttributeJobList = 0x0002000D;
    internal const uint StillActive = 259;

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateJobObjectW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateJobObject(
        nint jobAttributes,
        string? name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int SetInformationJobObject(
        SafeJobHandle job,
        int informationClass,
        nint information,
        uint informationLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int QueryInformationJobObject(
        SafeJobHandle job,
        int informationClass,
        nint information,
        uint informationLength,
        out uint returnLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int IsProcessInJob(
        SafeProcessHandle process,
        SafeJobHandle job,
        out int result);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int InitializeProcThreadAttributeList(
        nint attributeList,
        uint attributeCount,
        uint flags,
        ref nuint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int UpdateProcThreadAttribute(
        nint attributeList,
        uint flags,
        nuint attribute,
        nint value,
        nuint size,
        nint previousValue,
        nint returnSize);

    [LibraryImport("kernel32.dll")]
    internal static partial void DeleteProcThreadAttributeList(
        nint attributeList);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateProcessW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static unsafe partial int CreateProcess(
        string applicationName,
        char* commandLine,
        nint processAttributes,
        nint threadAttributes,
        int inheritHandles,
        uint creationFlags,
        char* environment,
        string currentDirectory,
        ref StartupInfoEx startupInfo,
        out ProcessInformation processInformation);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint ResumeThread(SafeThreadHandle thread);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int GetExitCodeProcess(
        SafeProcessHandle process,
        out uint exitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int GetHandleInformation(
        SafeJobHandle handle,
        out uint flags);

    [StructLayout(LayoutKind.Sequential)]
    internal struct StartupInfo
    {
        internal uint Size;
        internal nint Reserved;
        internal nint Desktop;
        internal nint Title;
        internal uint X;
        internal uint Y;
        internal uint XSize;
        internal uint YSize;
        internal uint XCountChars;
        internal uint YCountChars;
        internal uint FillAttribute;
        internal uint Flags;
        internal ushort ShowWindow;
        internal ushort Reserved2;
        internal nint Reserved2Pointer;
        internal nint StandardInput;
        internal nint StandardOutput;
        internal nint StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct StartupInfoEx
    {
        internal StartupInfo StartupInfo;
        internal nint AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessInformation
    {
        internal nint Process;
        internal nint Thread;
        internal uint ProcessId;
        internal uint ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IoCounters
    {
        internal ulong ReadOperationCount;
        internal ulong WriteOperationCount;
        internal ulong OtherOperationCount;
        internal ulong ReadTransferCount;
        internal ulong WriteTransferCount;
        internal ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BasicLimitInformation
    {
        internal long PerProcessUserTimeLimit;
        internal long PerJobUserTimeLimit;
        internal uint LimitFlags;
        internal nuint MinimumWorkingSetSize;
        internal nuint MaximumWorkingSetSize;
        internal uint ActiveProcessLimit;
        internal nuint Affinity;
        internal uint PriorityClass;
        internal uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ExtendedLimitInformation
    {
        internal BasicLimitInformation BasicLimitInformation;
        internal IoCounters IoInfo;
        internal nuint ProcessMemoryLimit;
        internal nuint JobMemoryLimit;
        internal nuint PeakProcessMemoryUsed;
        internal nuint PeakJobMemoryUsed;
    }

    internal sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeJobHandle(nint handle)
            : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            return CloseKernelHandle(handle) != 0;
        }
    }

    internal sealed class SafeThreadHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeThreadHandle(nint handle)
            : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            return CloseKernelHandle(handle) != 0;
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CloseHandle", SetLastError = true)]
    private static partial int CloseKernelHandle(nint handle);

    internal static int CloseRawKernelHandle(nint handle)
    {
        return CloseKernelHandle(handle);
    }
}
