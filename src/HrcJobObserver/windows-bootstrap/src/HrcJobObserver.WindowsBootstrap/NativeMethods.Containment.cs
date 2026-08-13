using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class NativeMethods
{
    internal const uint CreateSuspended = 0x00000004;
    internal const uint DebugOnlyThisProcess = 0x00000002;
    internal const uint CreateDefaultErrorMode = 0x04000000;
    internal const uint CreateNoWindow = 0x08000000;
    internal const uint CreateUnicodeEnvironment = 0x00000400;
    internal const uint ExtendedStartupInfoPresent = 0x00080000;
    internal const uint JobObjectLimitKillOnJobClose = 0x00002000;
    internal const int JobObjectBasicProcessIdList = 3;
    internal const int JobObjectExtendedLimitInformation = 9;
    internal const nuint ProcThreadAttributeJobList = 0x0002000D;
    internal const uint StillActive = 259;
    internal const uint ExceptionDebugEvent = 1;
    internal const uint CreateThreadDebugEvent = 2;
    internal const uint CreateProcessDebugEvent = 3;
    internal const uint ExitThreadDebugEvent = 4;
    internal const uint ExitProcessDebugEvent = 5;
    internal const uint LoadDllDebugEvent = 6;
    internal const uint UnloadDllDebugEvent = 7;
    internal const uint OutputDebugStringEvent = 8;
    internal const uint RipEvent = 9;
    internal const uint DbgContinue = 0x00010002;
    internal const int ErrorSemTimeout = 121;
    internal const ushort ImageFileMachineUnknown = 0;
    internal const ushort ImageFileMachineAmd64 = 0x8664;

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
    internal static partial uint SuspendThread(SafeThreadHandle thread);

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

    [LibraryImport("ntdll.dll")]
    internal static unsafe partial int RtlGetVersion(
        OsVersionInfoEx* versionInformation);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static unsafe partial int WaitForDebugEvent(
        DebugEvent* debugEvent,
        uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int ContinueDebugEvent(
        uint processId,
        uint threadId,
        uint continueStatus);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int DebugActiveProcessStop(uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int CheckRemoteDebuggerPresent(
        SafeProcessHandle process,
        out int debuggerPresent);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int IsWow64Process2(
        SafeProcessHandle process,
        out ushort processMachine,
        out ushort nativeMachine);

    [LibraryImport("kernelbase.dll")]
    internal static partial int CompareObjectHandles(
        nint firstObjectHandle,
        nint secondObjectHandle);

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
    internal unsafe struct OsVersionInfoEx
    {
        internal uint Size;
        internal uint MajorVersion;
        internal uint MinorVersion;
        internal uint BuildNumber;
        internal uint PlatformId;
        internal fixed char ServicePack[128];
        internal ushort ServicePackMajor;
        internal ushort ServicePackMinor;
        internal ushort SuiteMask;
        internal byte ProductType;
        internal byte Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CreateProcessDebugInfo
    {
        internal nint File;
        internal nint Process;
        internal nint Thread;
        internal nint BaseOfImage;
        internal uint DebugInfoFileOffset;
        internal uint DebugInfoSize;
        internal nint ThreadLocalBase;
        internal nint StartAddress;
        internal nint ImageName;
        internal ushort Unicode;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ExitProcessDebugInfo
    {
        internal uint ExitCode;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LoadDllDebugInfo
    {
        internal nint File;
        internal nint BaseOfDll;
        internal uint DebugInfoFileOffset;
        internal uint DebugInfoSize;
        internal nint ImageName;
        internal ushort Unicode;
    }

    [StructLayout(LayoutKind.Explicit, Size = 160)]
    internal struct DebugEventUnion
    {
        [FieldOffset(0)]
        internal CreateProcessDebugInfo CreateProcess;

        [FieldOffset(0)]
        internal ExitProcessDebugInfo ExitProcess;

        [FieldOffset(0)]
        internal LoadDllDebugInfo LoadDll;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DebugEvent
    {
        internal uint Code;
        internal uint ProcessId;
        internal uint ThreadId;
        internal DebugEventUnion Union;
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
        internal SafeThreadHandle()
            : base(true)
        {
        }

        internal SafeThreadHandle(nint handle)
            : base(true)
        {
            SetHandle(handle);
        }

        internal void Initialize(nint value)
        {
            if (!IsInvalid || IsClosed || value == 0 || value == -1)
            {
                throw new InvalidOperationException(
                    "The thread handle cannot be initialized twice or from an invalid value.");
            }

            SetHandle(value);
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
