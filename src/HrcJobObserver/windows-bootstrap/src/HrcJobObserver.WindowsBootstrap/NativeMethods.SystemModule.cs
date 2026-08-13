using System.Runtime.InteropServices;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class NativeMethods
{
    internal const uint DuplicateSameAccess = 0x00000002;

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "GetSystemDirectoryW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static unsafe partial uint GetSystemDirectory(
        char* buffer,
        uint size);

    [LibraryImport("kernel32.dll", EntryPoint = "GetCurrentProcess")]
    internal static partial nint GetCurrentProcessPseudoHandle();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int DuplicateHandle(
        nint sourceProcess,
        nint sourceHandle,
        nint targetProcess,
        out nint targetHandle,
        uint desiredAccess,
        int inheritHandle,
        uint options);
}
