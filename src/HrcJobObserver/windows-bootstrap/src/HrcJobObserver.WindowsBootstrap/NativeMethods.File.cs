using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class NativeMethods
{
    internal const uint DeleteAccess = 0x00010000;
    internal const uint ReadControl = 0x00020000;
    internal const uint FileListDirectory = 0x00000001;
    internal const uint FileTraverse = 0x00000020;
    internal const uint FileReadAttributes = 0x00000080;
    internal const uint FileShareRead = 0x00000001;
    internal const uint FileShareWrite = 0x00000002;
    internal const uint FileShareDelete = 0x00000004;
    internal const uint CreateNew = 1;
    internal const uint FileAttributeDirectory = 0x00000010;
    internal const uint FileAttributeNormal = 0x00000080;
    internal const uint FileAttributeReparsePoint = 0x00000400;
    internal const uint FileFlagBackupSemantics = 0x02000000;
    internal const uint FileFlagOpenReparsePoint = 0x00200000;
    internal const uint OwnerSecurityInformation = 0x00000001;
    internal const uint SeFileObject = 1;
    internal const uint FileSupportsPosixUnlinkRename = 0x00000400;
    internal const uint FileDispositionDelete = 0x00000001;
    internal const uint FileDispositionPosixSemantics = 0x00000002;
    internal const int FileStandardInfoClass = 1;
    internal const int FileRenameInformationClass = 10;
    internal const int FileDispositionInfoExClass = 21;
    internal const int FileAttributeTagInfoClass = 9;
    internal const int FileIdInfoClass = 18;
    internal const int FileIdExtdDirectoryInfoClass = 19;
    internal const int FileIdExtdDirectoryRestartInfoClass = 20;
    internal const int ErrorFileNotFound = 2;
    internal const int ErrorPathNotFound = 3;
    internal const int ErrorNoMoreFiles = 18;
    internal const int ErrorFileExists = 80;
    internal const int ErrorAlreadyExists = 183;
    internal const uint FileBegin = 0;
    internal const uint VolumeNameGuid = 0x00000001;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static unsafe partial int ReadFile(
        SafeFileHandle file,
        byte* buffer,
        uint bytesToRead,
        out uint bytesRead,
        nint overlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static unsafe partial int WriteFile(
        SafeFileHandle file,
        byte* buffer,
        uint bytesToWrite,
        out uint bytesWritten,
        nint overlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int FlushFileBuffers(SafeFileHandle file);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int GetFileSizeEx(
        SafeFileHandle file,
        out long fileSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int SetFilePointerEx(
        SafeFileHandle file,
        long distanceToMove,
        out long newFilePointer,
        uint moveMethod);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static unsafe partial int GetFileInformationByHandleEx(
        SafeFileHandle file,
        int informationClass,
        void* information,
        uint bufferSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int SetFileInformationByHandle(
        SafeFileHandle file,
        int informationClass,
        nint information,
        uint bufferSize);

    [LibraryImport("ntdll.dll")]
    internal static partial int NtSetInformationFile(
        SafeFileHandle file,
        out IoStatusBlock ioStatus,
        nint information,
        uint bufferSize,
        int informationClass);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "GetFinalPathNameByHandleW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static unsafe partial uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        char* path,
        uint pathLength,
        uint flags);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "GetVolumeInformationByHandleW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static unsafe partial int GetVolumeInformationByHandle(
        SafeFileHandle file,
        char* volumeName,
        uint volumeNameLength,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        char* fileSystemName,
        uint fileSystemNameLength);

    [StructLayout(LayoutKind.Sequential)]
    internal struct FileAttributeTagInfo
    {
        internal uint FileAttributes;
        internal uint ReparseTag;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FileStandardInfo
    {
        internal long AllocationSize;
        internal long EndOfFile;
        internal uint NumberOfLinks;
        internal byte DeletePending;
        internal byte Directory;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct FileId128
    {
        internal fixed byte Identifier[16];
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct FileIdInfo
    {
        internal ulong VolumeSerialNumber;
        internal FileId128 FileId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FileDispositionInfoEx
    {
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct IoStatusBlock
    {
        // IO_STATUS_BLOCK starts with a native union of NTSTATUS and PVOID.
        // NtSetInformationFile's immediate return is authoritative here; this
        // pointer-sized field preserves the ABI without interpreting the union.
        internal readonly nint PointerOrStatus;
        internal readonly nuint Information;
    }
}
