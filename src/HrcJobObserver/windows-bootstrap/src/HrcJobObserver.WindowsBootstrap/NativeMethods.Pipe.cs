using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class NativeMethods
{
    internal const uint GenericRead = 0x80000000;
    internal const uint GenericWrite = 0x40000000;
    internal const uint OpenExisting = 3;
    internal const uint PipeAccessDuplex = 0x00000003;
    internal const uint FileFlagFirstPipeInstance = 0x00080000;
    internal const uint FileFlagOverlapped = 0x40000000;
    internal const uint SecurityIdentification = 0x00010000;
    internal const uint SecuritySqosPresent = 0x00100000;
    internal const uint PipeTypeByte = 0x00000000;
    internal const uint PipeReadmodeByte = 0x00000000;
    internal const uint PipeWait = 0x00000000;
    internal const uint PipeRejectRemoteClients = 0x00000008;
    internal const int ErrorPipeConnected = 535;
    internal const uint SddlRevision1 = 1;
    internal const uint ErrorSuccess = 0;
    internal const uint SeKernelObject = 6;
    internal const uint DaclSecurityInformation = 0x00000004;

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateNamedPipeW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static unsafe partial nint CreateNamedPipe(
        string name,
        uint openMode,
        uint pipeMode,
        uint maximumInstances,
        uint outputBufferSize,
        uint inputBufferSize,
        uint defaultTimeout,
        SecurityAttributes* securityAttributes);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateFileW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int GetNamedPipeClientProcessId(
        nint pipe,
        out uint clientProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int GetNamedPipeServerProcessId(
        nint pipe,
        out uint serverProcessId);

    [LibraryImport(
        "advapi32.dll",
        EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int ConvertStringSecurityDescriptor(
        string stringSecurityDescriptor,
        uint stringSdRevision,
        out nint securityDescriptor,
        out uint securityDescriptorSize);

    [LibraryImport(
        "advapi32.dll",
        EntryPoint = "GetSecurityInfo")]
    internal static partial uint GetSecurityInfo(
        nint handle,
        uint objectType,
        uint securityInformation,
        out nint owner,
        out nint group,
        out nint dacl,
        out nint sacl,
        out nint securityDescriptor);

    [LibraryImport(
        "advapi32.dll",
        EntryPoint = "ConvertSecurityDescriptorToStringSecurityDescriptorW",
        SetLastError = true)]
    internal static partial int ConvertSecurityDescriptorToString(
        nint securityDescriptor,
        uint requestedStringSdRevision,
        uint securityInformation,
        out nint stringSecurityDescriptor,
        out uint stringSecurityDescriptorLength);

    [StructLayout(LayoutKind.Sequential)]
    internal struct SecurityAttributes
    {
        internal uint Length;
        internal nint SecurityDescriptor;
        internal int InheritHandle;
    }

}
