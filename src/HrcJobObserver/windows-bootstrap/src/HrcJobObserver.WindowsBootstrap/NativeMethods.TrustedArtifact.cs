using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class NativeMethods
{
    internal const uint DriveFixed = 3;
    internal const uint FileTypeDisk = 1;
    internal const uint FileFlagSequentialScan = 0x08000000;

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "GetDriveTypeW",
        StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint GetDriveType(string rootPathName);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint GetFileType(SafeFileHandle file);
}
