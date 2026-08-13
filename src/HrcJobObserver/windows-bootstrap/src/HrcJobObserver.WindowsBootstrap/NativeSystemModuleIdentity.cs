using System;
using System.Buffers.Binary;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal enum NativeStartupSystemModule
{
    Ntdll = 0,
    Kernel32 = 1,
    KernelBase = 2,
    Apphelp = 3,
}

/// <summary>
/// Retains one exact native System32 module file and compares debugger-supplied
/// LOAD_DLL file handles with that contemporaneous identity. This does not
/// establish Microsoft provenance, KnownDLL section identity, or trusted-launch
/// eligibility.
/// </summary>
internal sealed class NativeSystemModuleIdentityLease : IDisposable
{
    private readonly object gate = new();
    private readonly NativeStartupSystemModule module;
    private readonly SafeFileHandle expectedHandle;
    private readonly byte[] expectedDigest;
    private readonly TrustedArtifactFileIdentity expectedIdentity;
    private readonly uint expectedLinkCount;
    private bool disposed;

    private NativeSystemModuleIdentityLease(
        NativeStartupSystemModule module,
        SafeFileHandle expectedHandle,
        string path,
        string volumeGuidPath,
        long length,
        byte[] expectedDigest,
        TrustedArtifactFileIdentity expectedIdentity,
        uint expectedLinkCount)
    {
        this.module = module;
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

    internal NativeStartupSystemModule Module => module;

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

    internal bool HasSameFileIdentity(
        NativeSystemModuleIdentityLease other)
    {
        ArgumentNullException.ThrowIfNull(other);
        byte[] thisIdentifier = CopyFileIdentifier();
        byte[]? otherIdentifier = null;
        try
        {
            otherIdentifier = other.CopyFileIdentifier();
            return expectedIdentity.VolumeSerialNumber ==
                    other.expectedIdentity.VolumeSerialNumber &&
                CryptographicOperations.FixedTimeEquals(
                    thisIdentifier,
                    otherIdentifier);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(thisIdentifier);
            if (otherIdentifier is not null)
            {
                CryptographicOperations.ZeroMemory(otherIdentifier);
            }
        }
    }

    internal static NativeSystemModuleIdentityLease OpenKernel32(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        RequireSupportedPlatform(deadline, cancellationToken);
        return OpenExpectedModule(
            NativeStartupSystemModule.Kernel32,
            ReadNativeSystemDirectory(),
            deadline,
            cancellationToken);
    }

    internal static NativeSystemModuleIdentityLease OpenExpectedModule(
        NativeStartupSystemModule module,
        string systemDirectory,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(systemDirectory);
        RequireSupportedPlatform(deadline, cancellationToken);
        string nativeSystemDirectory = ReadNativeSystemDirectory();
        if (!string.Equals(
                systemDirectory,
                nativeSystemDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                "The expected native system-module directory was not the current native System32 directory.");
        }

        CheckOperation(deadline, cancellationToken);
        string moduleFileName = GetFileName(module);
        string path = PathJoinCanonical(systemDirectory, moduleFileName);
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
                module,
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
    /// expected module identity. An exact match produces independently owned
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
            SafeFileHandle? candidateCopy = DuplicateFileHandle(candidate);
            byte[]? candidateDigest = null;
            byte[]? evidenceDigest = null;
            try
            {
                CheckOperation(deadline, cancellationToken);
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
                        $"The loaded native system module bytes did not match {GetDisplayName(module)}.");
                }

                CheckOperation(deadline, cancellationToken);
                ValidateExpectedStillCurrent(deadline, cancellationToken);
                evidenceDigest = (byte[])expectedDigest.Clone();
                CheckOperation(deadline, cancellationToken);
                NativeSystemModuleLoadEvidence evidence = new(
                    module,
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
                    $"The native System32 {GetDisplayName(module)} bytes changed during revalidation.");
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
                $"The native system module identity did not match {GetDisplayName(module)}.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    internal static void RequireSupportedPlatform(
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
    }

    internal static string GetDisplayName(NativeStartupSystemModule module) =>
        module switch
        {
            NativeStartupSystemModule.Ntdll => "NTDLL",
            NativeStartupSystemModule.Kernel32 => "KERNEL32",
            NativeStartupSystemModule.KernelBase => "KernelBase",
            NativeStartupSystemModule.Apphelp => "Apphelp",
            _ => throw new ArgumentOutOfRangeException(
                nameof(module),
                module,
                "The native startup system module is not supported."),
        };

    private static string GetFileName(NativeStartupSystemModule module) =>
        module switch
        {
            NativeStartupSystemModule.Ntdll => "ntdll.dll",
            NativeStartupSystemModule.Kernel32 => "kernel32.dll",
            NativeStartupSystemModule.KernelBase => "KernelBase.dll",
            NativeStartupSystemModule.Apphelp => "apphelp.dll",
            _ => throw new ArgumentOutOfRangeException(
                nameof(module),
                module,
                "The native startup system module is not supported."),
        };

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
                "The native System32 module is not on a fixed local drive.");
        }

        return full;
    }

    internal static unsafe string ReadNativeSystemDirectory()
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
                "Opening the native System32 module file failed");
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

    internal static ModuleMetadata ValidateExpectedPath(
        SafeFileHandle handle,
        string expectedPath)
    {
        ModuleMetadata metadata = ValidateHandle(handle);
        string dosPath = ReadFinalPath(handle, flags: 0);
        string expected = "\\\\?\\" + expectedPath;
        if (!string.Equals(dosPath, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                "The native System32 module path resolved unexpectedly.");
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
/// Retains the exact native System32 NTDLL, KERNEL32, KernelBase, and Apphelp
/// files and owns debugger-supplied LOAD_DLL evidence for that exact observed
/// order. Apphelp is host/build/fixture appcompat-loader policy, not a static
/// fixture import. The aggregate is a closed host-compatibility policy for the
/// synthetic fixture; it does not establish KnownDLL, signer, section, general
/// loader closure, or trusted-launch identity.
/// Its retained read-only, non-delete-sharing file handles can defer replacement
/// or Windows servicing of these four System32 files for the lease lifetime.
/// </summary>
internal sealed class NativeStartupSystemModuleSetLease : IDisposable
{
    internal const int RequiredModuleCount = 4;

    private readonly object gate = new();
    private readonly NativeSystemModuleIdentityLease?[] expectedModules;
    private readonly NativeSystemModuleLoadEvidence?[] loadedModules =
        new NativeSystemModuleLoadEvidence?[RequiredModuleCount];
    private readonly nint[] loadedBaseAddresses = new nint[RequiredModuleCount];
    private nint mainImageBaseAddress;
    private int capturedCount;
    private bool sealedAtInitialBreakpoint;
    private bool faulted;
    private bool disposed;

    private NativeStartupSystemModuleSetLease(
        NativeSystemModuleIdentityLease?[] expectedModules)
    {
        this.expectedModules = expectedModules;
    }

    internal int CapturedCount
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return capturedCount;
            }
        }
    }

    internal bool IsSealed
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return sealedAtInitialBreakpoint;
            }
        }
    }

    internal bool IsEligibleForTrustedLaunch
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return false;
            }
        }
    }

    internal static NativeStartupSystemModuleSetLease OpenExpected(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        NativeSystemModuleIdentityLease.RequireSupportedPlatform(
            deadline,
            cancellationToken);
        string systemDirectory =
            NativeSystemModuleIdentityLease.ReadNativeSystemDirectory();
        NativeSystemModuleIdentityLease?[] expected =
            new NativeSystemModuleIdentityLease?[RequiredModuleCount];
        bool transferred = false;
        try
        {
            for (int ordinal = 0; ordinal < RequiredModuleCount; ordinal++)
            {
                NativeSystemModuleIdentityLease.CheckOperation(
                    deadline,
                    cancellationToken);
                expected[ordinal] =
                    NativeSystemModuleIdentityLease.OpenExpectedModule(
                        GetModuleAtOrdinal(ordinal),
                        systemDirectory,
                        deadline,
                        cancellationToken);

                NativeSystemModuleIdentityLease current = expected[ordinal] ??
                    throw new InvalidOperationException(
                        "Opening a native startup system module returned no lease.");
                for (int prior = 0; prior < ordinal; prior++)
                {
                    NativeSystemModuleIdentityLease.CheckOperation(
                        deadline,
                        cancellationToken);
                    NativeSystemModuleIdentityLease priorLease =
                        expected[prior] ?? throw new InvalidOperationException(
                            "The expected native startup system-module set became incomplete.");
                    if (current.HasSameFileIdentity(priorLease))
                    {
                        throw new SecurityException(
                            "Expected native startup system modules shared one file identity.");
                    }
                }
            }

            for (int ordinal = 0; ordinal < RequiredModuleCount; ordinal++)
            {
                expected[ordinal]!.Revalidate(deadline, cancellationToken);
            }

            NativeSystemModuleIdentityLease.CheckOperation(
                deadline,
                cancellationToken);
            NativeStartupSystemModuleSetLease result = new(expected);
            transferred = true;
            try
            {
                NativeSystemModuleIdentityLease.CheckOperation(
                    deadline,
                    cancellationToken);
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
            if (!transferred)
            {
                for (int ordinal = expected.Length - 1; ordinal >= 0; ordinal--)
                {
                    expected[ordinal]?.Dispose();
                    expected[ordinal] = null;
                }
            }
        }
    }

    internal NativeStartupSystemModule GetExpectedModule(int ordinal)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            _ = GetExpectedAtOrdinal(ordinal);
            return GetModuleAtOrdinal(ordinal);
        }
    }

    internal string GetExpectedPath(NativeStartupSystemModule module)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return GetExpected(module).Path;
        }
    }

    internal long GetExpectedLength(NativeStartupSystemModule module)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return GetExpected(module).Length;
        }
    }

    internal byte[] CopyExpectedSha256Digest(
        NativeStartupSystemModule module)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return GetExpected(module).CopySha256Digest();
        }
    }

    internal byte[] CopyExpectedFileIdentifier(
        NativeStartupSystemModule module)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return GetExpected(module).CopyFileIdentifier();
        }
    }

    internal nint GetLoadedBaseAddress(NativeStartupSystemModule module)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            int ordinal = GetOrdinal(module);
            if (loadedModules[ordinal] is null)
            {
                throw new InvalidOperationException(
                    $"No {NativeSystemModuleIdentityLease.GetDisplayName(module)} load evidence has been captured.");
            }

            return loadedBaseAddresses[ordinal];
        }
    }

    internal void RevalidateExpectedSet(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            ThrowIfFaulted();
            for (int ordinal = 0; ordinal < RequiredModuleCount; ordinal++)
            {
                NativeSystemModuleIdentityLease.CheckOperation(
                    deadline,
                    cancellationToken);
                GetExpectedAtOrdinal(ordinal).Revalidate(
                    deadline,
                    cancellationToken);
            }

            NativeSystemModuleIdentityLease.CheckOperation(
                deadline,
                cancellationToken);
        }
    }

    /// <summary>
    /// Duplicates the borrowed debug-event file handle before validating the
    /// event's addresses. The candidate must match the next exact member in the
    /// fixed NTDLL, KERNEL32, KernelBase, Apphelp order.
    /// </summary>
    internal NativeStartupSystemModule CaptureNextLoadedModule(
        SafeFileHandle borrowedEventFile,
        nint moduleBaseAddress,
        nint mainImageBaseAddress,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (sealedAtInitialBreakpoint)
            {
                throw new InvalidOperationException(
                    "The native startup system-module set is already sealed.");
            }

            ThrowIfFaulted();

            if (capturedCount >= RequiredModuleCount)
            {
                faulted = true;
                throw new SecurityException(
                    "The native startup sequence supplied an extra system-module load event.");
            }

            int ordinal = capturedCount;
            NativeStartupSystemModule module = GetModuleAtOrdinal(ordinal);
            NativeSystemModuleLoadEvidence? captured = null;
            try
            {
                // TryCaptureLoadedModuleEvidence duplicates the borrowed handle
                // before performing file-identity, metadata, or digest work.
                captured = GetExpectedAtOrdinal(ordinal)
                    .TryCaptureLoadedModuleEvidence(
                        borrowedEventFile,
                        deadline,
                        cancellationToken);
                if (captured is null || captured.Module != module)
                {
                    throw new SecurityException(
                        $"Startup module ordinal {ordinal} did not match {NativeSystemModuleIdentityLease.GetDisplayName(module)}.");
                }

                RequireDistinctBaseAddress(
                    moduleBaseAddress,
                    mainImageBaseAddress,
                    ordinal);
                NativeSystemModuleIdentityLease.CheckOperation(
                    deadline,
                    cancellationToken);

                loadedModules[ordinal] = captured;
                loadedBaseAddresses[ordinal] = moduleBaseAddress;
                if (ordinal == 0)
                {
                    this.mainImageBaseAddress = mainImageBaseAddress;
                }

                capturedCount = ordinal + 1;
                captured = null;
                try
                {
                    NativeSystemModuleIdentityLease.CheckOperation(
                        deadline,
                        cancellationToken);
                    return module;
                }
                catch
                {
                    capturedCount = ordinal;
                    if (ordinal == 0)
                    {
                        this.mainImageBaseAddress = 0;
                    }

                    loadedBaseAddresses[ordinal] = 0;
                    captured = loadedModules[ordinal];
                    loadedModules[ordinal] = null;
                    throw;
                }
            }
            catch
            {
                faulted = true;
                throw;
            }
            finally
            {
                captured?.Dispose();
            }
        }
    }

    internal void SealAtInitialBreakpoint(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (sealedAtInitialBreakpoint)
            {
                throw new InvalidOperationException(
                    "The native startup system-module set is already sealed.");
            }

            ThrowIfFaulted();

            if (capturedCount != RequiredModuleCount)
            {
                faulted = true;
                throw new SecurityException(
                    "The initial breakpoint arrived before the exact native startup system-module set was captured.");
            }

            try
            {
                RevalidateCompleteSet(deadline, cancellationToken);
                NativeSystemModuleIdentityLease.CheckOperation(
                    deadline,
                    cancellationToken);
                sealedAtInitialBreakpoint = true;
                NativeSystemModuleIdentityLease.CheckOperation(
                    deadline,
                    cancellationToken);
            }
            catch
            {
                sealedAtInitialBreakpoint = false;
                faulted = true;
                throw;
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
            ThrowIfFaulted();
            if (!sealedAtInitialBreakpoint ||
                capturedCount != RequiredModuleCount)
            {
                throw new InvalidOperationException(
                    "The native startup system-module set is not sealed and complete.");
            }

            RevalidateCompleteSet(deadline, cancellationToken);
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
            for (int ordinal = RequiredModuleCount - 1; ordinal >= 0; ordinal--)
            {
                loadedModules[ordinal]?.Dispose();
                loadedModules[ordinal] = null;
                loadedBaseAddresses[ordinal] = 0;
            }

            for (int ordinal = RequiredModuleCount - 1; ordinal >= 0; ordinal--)
            {
                expectedModules[ordinal]?.Dispose();
                expectedModules[ordinal] = null;
            }

            mainImageBaseAddress = 0;
            capturedCount = 0;
            sealedAtInitialBreakpoint = false;
            faulted = false;
        }
    }

    private void RevalidateCompleteSet(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        for (int ordinal = 0; ordinal < RequiredModuleCount; ordinal++)
        {
            NativeSystemModuleIdentityLease.CheckOperation(
                deadline,
                cancellationToken);
            GetExpectedAtOrdinal(ordinal).Revalidate(
                deadline,
                cancellationToken);
            NativeSystemModuleLoadEvidence evidence =
                loadedModules[ordinal] ?? throw new SecurityException(
                    "The native startup system-module evidence set is incomplete.");
            if (evidence.Module != GetModuleAtOrdinal(ordinal))
            {
                throw new SecurityException(
                    "The native startup system-module evidence order changed.");
            }

            evidence.Revalidate(deadline, cancellationToken);
        }

        RequireStoredBaseAddresses();
        NativeSystemModuleIdentityLease.CheckOperation(
            deadline,
            cancellationToken);
    }

    private void RequireDistinctBaseAddress(
        nint moduleBaseAddress,
        nint suppliedMainImageBaseAddress,
        int ordinal)
    {
        if (moduleBaseAddress == 0 || suppliedMainImageBaseAddress == 0)
        {
            throw new SecurityException(
                "A native startup image or module base address was zero.");
        }

        if (moduleBaseAddress == suppliedMainImageBaseAddress)
        {
            throw new SecurityException(
                "A native startup system module reused the main-image base address.");
        }

        if (ordinal > 0 &&
            suppliedMainImageBaseAddress != mainImageBaseAddress)
        {
            throw new SecurityException(
                "The native startup sequence changed its main-image base address.");
        }

        for (int prior = 0; prior < ordinal; prior++)
        {
            if (moduleBaseAddress == loadedBaseAddresses[prior])
            {
                throw new SecurityException(
                    "Native startup system modules reused one load base address.");
            }
        }
    }

    private void RequireStoredBaseAddresses()
    {
        if (mainImageBaseAddress == 0)
        {
            throw new SecurityException(
                "The retained native main-image base address was zero.");
        }

        for (int ordinal = 0; ordinal < RequiredModuleCount; ordinal++)
        {
            nint moduleBaseAddress = loadedBaseAddresses[ordinal];
            if (moduleBaseAddress == 0 ||
                moduleBaseAddress == mainImageBaseAddress)
            {
                throw new SecurityException(
                    "A retained native startup module base address was invalid.");
            }

            for (int prior = 0; prior < ordinal; prior++)
            {
                if (moduleBaseAddress == loadedBaseAddresses[prior])
                {
                    throw new SecurityException(
                        "Retained native startup modules shared a load base address.");
                }
            }
        }
    }

    private NativeSystemModuleIdentityLease GetExpected(
        NativeStartupSystemModule module) =>
        GetExpectedAtOrdinal(GetOrdinal(module));

    private NativeSystemModuleIdentityLease GetExpectedAtOrdinal(int ordinal)
    {
        if ((uint)ordinal >= RequiredModuleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        return expectedModules[ordinal] ?? throw new ObjectDisposedException(
            nameof(NativeStartupSystemModuleSetLease));
    }

    private static NativeStartupSystemModule GetModuleAtOrdinal(int ordinal) =>
        ordinal switch
        {
            0 => NativeStartupSystemModule.Ntdll,
            1 => NativeStartupSystemModule.Kernel32,
            2 => NativeStartupSystemModule.KernelBase,
            3 => NativeStartupSystemModule.Apphelp,
            _ => throw new ArgumentOutOfRangeException(nameof(ordinal)),
        };

    private static int GetOrdinal(NativeStartupSystemModule module) =>
        module switch
        {
            NativeStartupSystemModule.Ntdll => 0,
            NativeStartupSystemModule.Kernel32 => 1,
            NativeStartupSystemModule.KernelBase => 2,
            NativeStartupSystemModule.Apphelp => 3,
            _ => throw new ArgumentOutOfRangeException(
                nameof(module),
                module,
                "The native startup system module is not supported."),
        };

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private void ThrowIfFaulted()
    {
        if (faulted)
        {
            throw new InvalidOperationException(
                "The native startup system-module set is terminal after an earlier sequence failure.");
        }
    }
}

/// <summary>
/// Owns a duplicate of a debugger-supplied LOAD_DLL file handle that matched
/// one contemporaneously retained native System32 module identity.
/// </summary>
internal sealed class NativeSystemModuleLoadEvidence : IDisposable
{
    private readonly object gate = new();
    private readonly NativeStartupSystemModule module;
    private readonly SafeFileHandle loadedHandle;
    private readonly byte[] digest;
    private readonly TrustedArtifactFileIdentity identity;
    private readonly uint linkCount;
    private bool disposed;

    internal NativeSystemModuleLoadEvidence(
        NativeStartupSystemModule module,
        SafeFileHandle loadedHandle,
        string path,
        string volumeGuidPath,
        long length,
        byte[] digest,
        TrustedArtifactFileIdentity identity,
        uint linkCount)
    {
        this.module = module;
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

    internal NativeStartupSystemModule Module => module;

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
