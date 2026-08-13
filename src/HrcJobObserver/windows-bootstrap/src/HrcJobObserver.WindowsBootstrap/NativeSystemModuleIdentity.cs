using System;
using System.Buffers.Binary;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Retains the native System32 KERNEL32 file and compares debugger-supplied
/// LOAD_DLL file handles with that contemporaneous identity. This does not
/// establish Microsoft provenance, KnownDLL section identity, or trusted-launch
/// eligibility.
/// </summary>
internal sealed class NativeSystemModuleIdentityLease : IDisposable
{
    private readonly object gate = new();
    private readonly SafeFileHandle expectedHandle;
    private readonly byte[] expectedDigest;
    private readonly TrustedArtifactFileIdentity expectedIdentity;
    private readonly uint expectedLinkCount;
    private bool disposed;

    private NativeSystemModuleIdentityLease(
        SafeFileHandle expectedHandle,
        string path,
        string volumeGuidPath,
        long length,
        byte[] expectedDigest,
        TrustedArtifactFileIdentity expectedIdentity,
        uint expectedLinkCount)
    {
        this.expectedHandle = expectedHandle;
        Path = path;
        VolumeGuidPath = volumeGuidPath;
        Length = length;
        this.expectedDigest = expectedDigest;
        this.expectedIdentity = expectedIdentity;
        this.expectedLinkCount = expectedLinkCount;
    }

    internal string Path { get; }

    internal string VolumeGuidPath { get; }

    internal long Length { get; }

    internal bool IsEligibleForTrustedLaunch => false;

    internal byte[] CopySha256Digest()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return (byte[])expectedDigest.Clone();
        }
    }

    internal byte[] CopyFileIdentifier()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return expectedIdentity.CopyIdentifier();
        }
    }

    internal static NativeSystemModuleIdentityLease OpenKernel32(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        if (IntPtr.Size != 8 ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64 ||
            RuntimeInformation.OSArchitecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException(
                "The native System32 module identity requires an x64 process.");
        }

        string systemDirectory = ReadNativeSystemDirectory();
        string path = PathJoinCanonical(systemDirectory, "kernel32.dll");
        CheckOperation(deadline, cancellationToken);
        SafeFileHandle? handle = null;
        byte[]? digest = null;
        try
        {
            handle = OpenRetained(path);
            ModuleMetadata before = ValidateExpectedPath(handle, path);
            digest = HashExact(handle, before.Length, deadline, cancellationToken);
            ModuleMetadata after = ValidateExpectedPath(handle, path);
            RequireUnchanged(before, after);
            CheckOperation(deadline, cancellationToken);
            NativeSystemModuleIdentityLease result = new(
                handle,
                path,
                before.VolumeGuidPath,
                before.Length,
                digest,
                before.Identity,
                before.LinkCount);
            handle = null;
            digest = null;
            try
            {
                CheckOperation(deadline, cancellationToken);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }
        finally
        {
            handle?.Dispose();
            if (digest is not null)
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }
    }

    /// <summary>
    /// Returns null only after a stable candidate identity differs from the
    /// expected KERNEL32 identity. An exact match produces independently owned
    /// evidence by duplicating the debugger-owned file handle.
    /// </summary>
    internal NativeSystemModuleLoadEvidence? TryCaptureLoadedModuleEvidence(
        SafeFileHandle candidate,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            SafeFileHandle? candidateCopy = DuplicateFileHandle(candidate);
            byte[]? candidateDigest = null;
            byte[]? evidenceDigest = null;
            try
            {
                ValidateExpectedStillCurrent(deadline, cancellationToken);
                ModuleMetadata candidateBefore = ValidateHandle(candidateCopy);
                if (!candidateBefore.Identity.Equals(expectedIdentity))
                {
                    ModuleMetadata differentAfter = ValidateHandle(candidateCopy);
                    RequireUnchanged(candidateBefore, differentAfter);
                    CheckOperation(deadline, cancellationToken);
                    return null;
                }

                RequireExactExpectedMetadata(candidateBefore);
                candidateDigest = HashExact(
                    candidateCopy,
                    Length,
                    deadline,
                    cancellationToken);
                ModuleMetadata candidateAfter = ValidateHandle(candidateCopy);
                RequireUnchanged(candidateBefore, candidateAfter);
                RequireExactExpectedMetadata(candidateAfter);
                if (!CryptographicOperations.FixedTimeEquals(
                        candidateDigest,
                        expectedDigest))
                {
                    throw new SecurityException(
                        "The loaded native system module bytes did not match KERNEL32.");
                }

                CheckOperation(deadline, cancellationToken);
                ValidateExpectedStillCurrent(deadline, cancellationToken);
                evidenceDigest = (byte[])expectedDigest.Clone();
                CheckOperation(deadline, cancellationToken);
                NativeSystemModuleLoadEvidence evidence = new(
                    candidateCopy,
                    Path,
                    VolumeGuidPath,
                    Length,
                    evidenceDigest,
                    expectedIdentity,
                    expectedLinkCount);
                candidateCopy = null;
                evidenceDigest = null;
                try
                {
                    CheckOperation(deadline, cancellationToken);
                    return evidence;
                }
                catch
                {
                    evidence.Dispose();
                    throw;
                }
            }
            finally
            {
                candidateCopy?.Dispose();
                if (candidateDigest is not null)
                {
                    CryptographicOperations.ZeroMemory(candidateDigest);
                }

                if (evidenceDigest is not null)
                {
                    CryptographicOperations.ZeroMemory(evidenceDigest);
                }
            }
        }
    }

    internal void Revalidate(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            ValidateExpectedStillCurrent(deadline, cancellationToken);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            expectedHandle.Dispose();
            CryptographicOperations.ZeroMemory(expectedDigest);
        }
    }

    private void ValidateExpectedStillCurrent(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        ModuleMetadata retainedBefore = ValidateExpectedPath(expectedHandle, Path);
        RequireExactExpectedMetadata(retainedBefore);
        using SafeFileHandle current = OpenRetained(Path);
        ModuleMetadata currentBefore = ValidateExpectedPath(current, Path);
        RequireExactExpectedMetadata(currentBefore);
        byte[] digest = HashExact(current, Length, deadline, cancellationToken);
        try
        {
            ModuleMetadata currentAfter = ValidateExpectedPath(current, Path);
            RequireUnchanged(currentBefore, currentAfter);
            RequireExactExpectedMetadata(currentAfter);
            if (!CryptographicOperations.FixedTimeEquals(digest, expectedDigest))
            {
                throw new SecurityException(
                    "The native System32 KERNEL32 bytes changed during revalidation.");
            }

            ModuleMetadata retainedAfter = ValidateExpectedPath(expectedHandle, Path);
            RequireUnchanged(retainedBefore, retainedAfter);
            RequireExactExpectedMetadata(retainedAfter);
            CheckOperation(deadline, cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private void RequireExactExpectedMetadata(ModuleMetadata actual)
    {
        if (actual.Length != Length ||
            actual.LinkCount != expectedLinkCount ||
            !actual.Identity.Equals(expectedIdentity) ||
            !string.Equals(
                actual.VolumeGuidPath,
                VolumeGuidPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                "The native system module identity did not match KERNEL32.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    internal static SafeFileHandle DuplicateFileHandle(SafeFileHandle source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.IsClosed || source.IsInvalid)
        {
            throw new ArgumentException(
                "The native system module source handle is invalid.",
                nameof(source));
        }

        bool added = false;
        nint duplicate = 0;
        try
        {
            source.DangerousAddRef(ref added);
            nint process = NativeMethods.GetCurrentProcessPseudoHandle();
            if (NativeMethods.DuplicateHandle(
                    process,
                    source.DangerousGetHandle(),
                    process,
                    out duplicate,
                    0,
                    0,
                    NativeMethods.DuplicateSameAccess) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "Duplicating a native system module file handle failed");
            }

            SafeFileHandle result = new(duplicate, ownsHandle: true);
            duplicate = 0;
            return result;
        }
        finally
        {
            if (duplicate != 0)
            {
                _ = NativeMethods.CloseRawKernelHandle(duplicate);
            }

            if (added)
            {
                source.DangerousRelease();
            }
        }
    }

    private static string PathJoinCanonical(string directory, string fileName)
    {
        string path = System.IO.Path.Combine(directory, fileName);
        string full = System.IO.Path.GetFullPath(path);
        if (!string.Equals(path, full, StringComparison.OrdinalIgnoreCase) ||
            !System.IO.Path.IsPathFullyQualified(full) ||
            full.Length < 4 ||
            !char.IsAsciiLetter(full[0]) ||
            full[1] != ':' ||
            full[2] != System.IO.Path.DirectorySeparatorChar ||
            full.IndexOf(System.IO.Path.AltDirectorySeparatorChar) >= 0 ||
            full.IndexOf(':', 2) >= 0)
        {
            throw new PlatformNotSupportedException(
                "The native System32 directory did not produce a canonical DOS path.");
        }

        string root = System.IO.Path.GetPathRoot(full) ?? string.Empty;
        if (root.Length != 3 ||
            NativeMethods.GetDriveType(root) != NativeMethods.DriveFixed)
        {
            throw new PlatformNotSupportedException(
                "The native System32 KERNEL32 is not on a fixed local drive.");
        }

        return full;
    }

    private static unsafe string ReadNativeSystemDirectory()
    {
        Span<char> buffer = stackalloc char[32_768];
        fixed (char* pointer = buffer)
        {
            uint written = NativeMethods.GetSystemDirectory(
                pointer,
                checked((uint)buffer.Length));
            if (written == 0 || written >= buffer.Length)
            {
                throw NativeMethods.Win32Failure(
                    "Reading the native System32 directory failed");
            }

            return new string(pointer, 0, checked((int)written));
        }
    }

    private static SafeFileHandle OpenRetained(string path)
    {
        nint raw = NativeMethods.CreateFile(
            path,
            NativeMethods.GenericRead,
            NativeMethods.FileShareRead,
            0,
            NativeMethods.OpenExisting,
            NativeMethods.FileAttributeNormal |
                NativeMethods.FileFlagOpenReparsePoint |
                NativeMethods.FileFlagSequentialScan,
            0);
        if (raw == 0 || raw == -1)
        {
            throw NativeMethods.Win32Failure(
                "Opening the native System32 KERNEL32 file failed");
        }

        try
        {
            SafeFileHandle handle = new(raw, ownsHandle: true);
            raw = 0;
            return handle;
        }
        finally
        {
            if (raw != 0 && raw != -1)
            {
                _ = NativeMethods.CloseRawKernelHandle(raw);
            }
        }
    }

    private static ModuleMetadata ValidateExpectedPath(
        SafeFileHandle handle,
        string expectedPath)
    {
        ModuleMetadata metadata = ValidateHandle(handle);
        string dosPath = ReadFinalPath(handle, flags: 0);
        string expected = "\\\\?\\" + expectedPath;
        if (!string.Equals(dosPath, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                "The native System32 KERNEL32 path resolved unexpectedly.");
        }

        return metadata;
    }

    internal static ModuleMetadata ValidateHandle(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.IsClosed || handle.IsInvalid)
        {
            throw new ArgumentException(
                "The native system module handle is invalid.",
                nameof(handle));
        }

        if (NativeMethods.GetFileType(handle) != NativeMethods.FileTypeDisk)
        {
            throw new SecurityException(
                "The native system module handle is not a disk file.");
        }

        NativeMethods.FileAttributeTagInfo attributes = ReadAttributes(handle);
        NativeMethods.FileStandardInfo standard = ReadStandard(handle);
        if ((attributes.FileAttributes &
                (NativeMethods.FileAttributeDirectory |
                    NativeMethods.FileAttributeReparsePoint)) != 0 ||
            standard.Directory != 0 ||
            standard.DeletePending != 0 ||
            standard.NumberOfLinks == 0 ||
            standard.EndOfFile < 0)
        {
            throw new SecurityException(
                "The native system module is not a stable non-reparse regular file.");
        }

        if (NativeMethods.GetFileSizeEx(handle, out long length) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading the native system module length failed");
        }

        if (length != standard.EndOfFile)
        {
            throw new SecurityException(
                "The native system module reported inconsistent lengths.");
        }

        return new ModuleMetadata(
            length,
            standard.NumberOfLinks,
            ReadIdentity(handle),
            ReadFinalPath(handle, NativeMethods.VolumeNameGuid));
    }

    internal static void RequireUnchanged(
        ModuleMetadata before,
        ModuleMetadata after)
    {
        if (before.Length != after.Length ||
            before.LinkCount != after.LinkCount ||
            !before.Identity.Equals(after.Identity) ||
            !string.Equals(
                before.VolumeGuidPath,
                after.VolumeGuidPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                "The native system module identity changed during validation.");
        }
    }

    private static unsafe NativeMethods.FileAttributeTagInfo ReadAttributes(
        SafeFileHandle handle)
    {
        NativeMethods.FileAttributeTagInfo information = default;
        if (NativeMethods.GetFileInformationByHandleEx(
                handle,
                NativeMethods.FileAttributeTagInfoClass,
                &information,
                checked((uint)sizeof(NativeMethods.FileAttributeTagInfo))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading native system module attributes failed");
        }

        return information;
    }

    private static unsafe NativeMethods.FileStandardInfo ReadStandard(
        SafeFileHandle handle)
    {
        NativeMethods.FileStandardInfo information = default;
        if (NativeMethods.GetFileInformationByHandleEx(
                handle,
                NativeMethods.FileStandardInfoClass,
                &information,
                checked((uint)sizeof(NativeMethods.FileStandardInfo))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading native system module link information failed");
        }

        return information;
    }

    private static unsafe TrustedArtifactFileIdentity ReadIdentity(
        SafeFileHandle handle)
    {
        NativeMethods.FileIdInfo information = default;
        if (NativeMethods.GetFileInformationByHandleEx(
                handle,
                NativeMethods.FileIdInfoClass,
                &information,
                checked((uint)sizeof(NativeMethods.FileIdInfo))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading native system module file identity failed");
        }

        ReadOnlySpan<byte> identifier = new(
            information.FileId.Identifier,
            TrustedArtifactFileIdentity.IdentifierLength);
        return new TrustedArtifactFileIdentity(
            information.VolumeSerialNumber,
            BinaryPrimitives.ReadUInt64LittleEndian(identifier),
            BinaryPrimitives.ReadUInt64LittleEndian(identifier[sizeof(ulong)..]));
    }

    private static unsafe string ReadFinalPath(
        SafeFileHandle handle,
        uint flags)
    {
        uint required = NativeMethods.GetFinalPathNameByHandle(
            handle,
            null,
            0,
            flags);
        if (required == 0 || required > 32_768)
        {
            throw new PlatformNotSupportedException(
                "Reading the native system module final path failed.");
        }

        char[] buffer = new char[checked((int)required)];
        fixed (char* pointer = buffer)
        {
            uint written = NativeMethods.GetFinalPathNameByHandle(
                handle,
                pointer,
                checked((uint)buffer.Length),
                flags);
            if (written == 0 || written >= buffer.Length)
            {
                throw new PlatformNotSupportedException(
                    "Reading the native system module final path failed.");
            }

            string result = new(pointer, 0, checked((int)written));
            if (flags == NativeMethods.VolumeNameGuid &&
                !result.StartsWith("\\\\?\\Volume{", StringComparison.OrdinalIgnoreCase))
            {
                throw new PlatformNotSupportedException(
                    "The native system module is not on a Mount Manager volume.");
            }

            return result;
        }
    }

    internal static byte[] HashExact(
        SafeFileHandle handle,
        long length,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        const int BufferLength = 64 * 1024;
        byte[] buffer = new byte[BufferLength];
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        try
        {
            long offset = 0;
            while (offset < length)
            {
                CheckOperation(deadline, cancellationToken);
                int requested = checked((int)Math.Min(buffer.Length, length - offset));
                int read = RandomAccess.Read(
                    handle,
                    buffer.AsSpan(0, requested),
                    offset);
                if (read <= 0)
                {
                    throw new EndOfStreamException(
                        "The native system module ended before its reported length.");
                }

                hash.AppendData(buffer, 0, read);
                offset += read;
                CheckOperation(deadline, cancellationToken);
            }

            Span<byte> trailing = stackalloc byte[1];
            if (RandomAccess.Read(handle, trailing, length) != 0)
            {
                throw new SecurityException(
                    "The native system module exceeded its reported length.");
            }

            CheckOperation(deadline, cancellationToken);
            return hash.GetHashAndReset();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    internal static void CheckOperation(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.GetRemaining();
    }

    internal readonly record struct ModuleMetadata(
        long Length,
        uint LinkCount,
        TrustedArtifactFileIdentity Identity,
        string VolumeGuidPath);
}

/// <summary>
/// Owns a duplicate of the debugger-supplied LOAD_DLL file handle that matched
/// the contemporaneously retained System32 KERNEL32 identity.
/// </summary>
internal sealed class NativeSystemModuleLoadEvidence : IDisposable
{
    private readonly object gate = new();
    private readonly SafeFileHandle loadedHandle;
    private readonly byte[] digest;
    private readonly TrustedArtifactFileIdentity identity;
    private readonly uint linkCount;
    private bool disposed;

    internal NativeSystemModuleLoadEvidence(
        SafeFileHandle loadedHandle,
        string path,
        string volumeGuidPath,
        long length,
        byte[] digest,
        TrustedArtifactFileIdentity identity,
        uint linkCount)
    {
        this.loadedHandle = loadedHandle;
        Path = path;
        VolumeGuidPath = volumeGuidPath;
        Length = length;
        this.digest = digest;
        this.identity = identity;
        this.linkCount = linkCount;
    }

    internal string Path { get; }

    internal string VolumeGuidPath { get; }

    internal long Length { get; }

    internal bool IsEligibleForTrustedLaunch => false;

    internal byte[] CopySha256Digest()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return (byte[])digest.Clone();
        }
    }

    internal byte[] CopyFileIdentifier()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return identity.CopyIdentifier();
        }
    }

    internal void Revalidate(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            NativeSystemModuleIdentityLease.CheckOperation(
                deadline,
                cancellationToken);
            using SafeFileHandle duplicate =
                NativeSystemModuleIdentityLease.DuplicateFileHandle(loadedHandle);
            NativeSystemModuleIdentityLease.ModuleMetadata before =
                NativeSystemModuleIdentityLease.ValidateHandle(duplicate);
            RequireExpectedMetadata(before);
            byte[] actualDigest = NativeSystemModuleIdentityLease.HashExact(
                duplicate,
                Length,
                deadline,
                cancellationToken);
            try
            {
                NativeSystemModuleIdentityLease.ModuleMetadata after =
                    NativeSystemModuleIdentityLease.ValidateHandle(duplicate);
                NativeSystemModuleIdentityLease.RequireUnchanged(before, after);
                RequireExpectedMetadata(after);
                if (!CryptographicOperations.FixedTimeEquals(actualDigest, digest))
                {
                    throw new SecurityException(
                        "The retained loaded native module bytes changed.");
                }

                NativeSystemModuleIdentityLease.CheckOperation(
                    deadline,
                    cancellationToken);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actualDigest);
            }
        }
    }

    private void RequireExpectedMetadata(
        NativeSystemModuleIdentityLease.ModuleMetadata actual)
    {
        if (actual.Length != Length ||
            actual.LinkCount != linkCount ||
            !actual.Identity.Equals(identity) ||
            !string.Equals(
                actual.VolumeGuidPath,
                VolumeGuidPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                "The retained loaded native module identity changed.");
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            loadedHandle.Dispose();
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

}
