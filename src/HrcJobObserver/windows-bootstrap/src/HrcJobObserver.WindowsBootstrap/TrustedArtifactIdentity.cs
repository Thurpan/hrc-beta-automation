using System;
using System.Buffers.Binary;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Opens and verifies one exact local artifact while retaining a handle that
/// denies new data-write and delete access. Attribute and extended-attribute
/// access are outside that sharing guarantee. This is an identity primitive,
/// not an atomic path-based process-launch handoff.
/// </summary>
internal static class TrustedArtifactIdentity
{
    private const int Sha256Length = 32;

    internal static TrustedArtifactLease Open(
        string exactPath,
        long expectedLength,
        ReadOnlySpan<byte> expectedSha256)
    {
        return OpenCore(
            exactPath,
            expectedLength,
            expectedSha256,
            deadline: null,
            CancellationToken.None);
    }

    internal static TrustedArtifactLease Open(
        string exactPath,
        long expectedLength,
        ReadOnlySpan<byte> expectedSha256,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        return OpenCore(
            exactPath,
            expectedLength,
            expectedSha256,
            deadline,
            cancellationToken);
    }

    private static TrustedArtifactLease OpenCore(
        string exactPath,
        long expectedLength,
        ReadOnlySpan<byte> expectedSha256,
        MonotonicDeadline? deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        if (expectedLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedLength));
        }

        if (expectedSha256.Length != Sha256Length)
        {
            throw new ArgumentException(
                "The expected SHA-256 digest must contain exactly 32 bytes.",
                nameof(expectedSha256));
        }

        string path = CanonicalFixedLocalPath(exactPath);
        SafeFileHandle? file = null;
        byte[]? actualDigest = null;
        try
        {
            CheckOperation(deadline, cancellationToken);
            RequireNoReparseAncestors(path);
            CheckOperation(deadline, cancellationToken);
            file = OpenRetained(path);
            CheckOperation(deadline, cancellationToken);
            ArtifactMetadata initial = ValidateMetadata(file, path);
            RequireExpectedLength(initial, expectedLength);

            actualDigest = HashExact(
                file,
                expectedLength,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
            ArtifactMetadata final = ValidateMetadata(file, path);
            RequireUnchanged(initial, final, expectedLength);
            CheckOperation(deadline, cancellationToken);
            RequireNoReparseAncestors(path);
            CheckOperation(deadline, cancellationToken);

            if (!CryptographicOperations.FixedTimeEquals(
                    actualDigest,
                    expectedSha256))
            {
                throw new SecurityException(
                    "The trusted artifact SHA-256 digest did not match.");
            }

            TrustedArtifactLease lease = new(
                file,
                path,
                expectedLength,
                actualDigest,
                initial.Identity);
            file = null;
            actualDigest = null;
            return lease;
        }
        finally
        {
            file?.Dispose();
            if (actualDigest is not null)
            {
                CryptographicOperations.ZeroMemory(actualDigest);
            }
        }
    }

    internal static void Revalidate(
        SafeFileHandle retained,
        string exactPath,
        long expectedLength,
        ReadOnlySpan<byte> expectedSha256,
        TrustedArtifactFileIdentity expectedIdentity)
    {
        RevalidateCore(
            retained,
            exactPath,
            expectedLength,
            expectedSha256,
            expectedIdentity,
            deadline: null,
            CancellationToken.None);
    }

    internal static void Revalidate(
        SafeFileHandle retained,
        string exactPath,
        long expectedLength,
        ReadOnlySpan<byte> expectedSha256,
        TrustedArtifactFileIdentity expectedIdentity,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        RevalidateCore(
            retained,
            exactPath,
            expectedLength,
            expectedSha256,
            expectedIdentity,
            deadline,
            cancellationToken);
    }

    private static void RevalidateCore(
        SafeFileHandle retained,
        string exactPath,
        long expectedLength,
        ReadOnlySpan<byte> expectedSha256,
        TrustedArtifactFileIdentity expectedIdentity,
        MonotonicDeadline? deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        ArgumentNullException.ThrowIfNull(retained);
        if (retained.IsClosed || retained.IsInvalid)
        {
            throw new ObjectDisposedException(nameof(TrustedArtifactLease));
        }

        ArtifactMetadata retainedBefore = ValidateMetadata(
            retained,
            exactPath);
        RequireIdentity(
            expectedIdentity,
            retainedBefore.Identity,
            "The retained trusted artifact identity changed.");
        RequireExpectedLength(retainedBefore, expectedLength);

        CheckOperation(deadline, cancellationToken);
        RequireNoReparseAncestors(exactPath);
        CheckOperation(deadline, cancellationToken);
        using SafeFileHandle current = OpenRetained(exactPath);
        CheckOperation(deadline, cancellationToken);
        ArtifactMetadata currentBefore = ValidateMetadata(current, exactPath);
        RequireIdentity(
            expectedIdentity,
            currentBefore.Identity,
            "The trusted artifact path no longer names the retained file.");
        RequireExpectedLength(currentBefore, expectedLength);

        byte[] digest = HashExact(
            current,
            expectedLength,
            deadline,
            cancellationToken);
        try
        {
            CheckOperation(deadline, cancellationToken);
            ArtifactMetadata currentAfter = ValidateMetadata(current, exactPath);
            RequireUnchanged(currentBefore, currentAfter, expectedLength);
            RequireIdentity(
                expectedIdentity,
                currentAfter.Identity,
                "The trusted artifact path identity changed during revalidation.");
            CheckOperation(deadline, cancellationToken);
            RequireNoReparseAncestors(exactPath);
            CheckOperation(deadline, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(
                    digest,
                    expectedSha256))
            {
                throw new SecurityException(
                    "The trusted artifact digest changed during revalidation.");
            }

            ArtifactMetadata retainedAfter = ValidateMetadata(
                retained,
                exactPath);
            RequireUnchanged(retainedBefore, retainedAfter, expectedLength);
            RequireIdentity(
                expectedIdentity,
                retainedAfter.Identity,
                "The retained trusted artifact identity changed during revalidation.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
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
                NativeMethods.FileFlagBackupSemantics |
                NativeMethods.FileFlagOpenReparsePoint |
                NativeMethods.FileFlagSequentialScan,
            0);
        SafeFileHandle handle = new(raw, true);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw NativeMethods.Win32Failure(
                "Opening the exact trusted artifact failed");
        }

        return handle;
    }

    private static void RequireNoReparseAncestors(string path)
    {
        string root = Path.GetPathRoot(path) ??
            throw new ArgumentException(
                "The trusted artifact path has no local root.",
                nameof(path));
        string? current = Path.GetDirectoryName(path);
        while (current is not null && current.Length > root.Length)
        {
            using SafeFileHandle directory = OpenAncestor(current);
            NativeMethods.FileAttributeTagInfo attributes =
                ReadAttributeInformation(directory);
            if ((attributes.FileAttributes &
                    NativeMethods.FileAttributeDirectory) == 0 ||
                (attributes.FileAttributes &
                    NativeMethods.FileAttributeReparsePoint) != 0)
            {
                throw new SecurityException(
                    "The trusted artifact path crosses a reparse point.");
            }

            RequireFinalPath(directory, current);
            RequireLocalVolume(directory);
            current = Path.GetDirectoryName(current);
        }
    }

    private static SafeFileHandle OpenAncestor(string path)
    {
        nint raw = NativeMethods.CreateFile(
            path,
            NativeMethods.FileReadAttributes,
            NativeMethods.FileShareRead |
                NativeMethods.FileShareWrite |
                NativeMethods.FileShareDelete,
            0,
            NativeMethods.OpenExisting,
            NativeMethods.FileFlagBackupSemantics |
                NativeMethods.FileFlagOpenReparsePoint,
            0);
        SafeFileHandle handle = new(raw, true);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw NativeMethods.Win32Failure(
                "Opening a trusted artifact path ancestor failed");
        }

        return handle;
    }

    private static string CanonicalFixedLocalPath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length < 4 ||
            !char.IsAsciiLetter(value[0]) ||
            value[1] != ':' ||
            value[2] != Path.DirectorySeparatorChar ||
            value.IndexOf(Path.AltDirectorySeparatorChar) >= 0 ||
            value.IndexOf(':', 2) >= 0 ||
            Path.EndsInDirectorySeparator(value) ||
            !Path.IsPathFullyQualified(value))
        {
            throw new ArgumentException(
                "The trusted artifact must use an exact absolute local DOS file path.",
                nameof(value));
        }

        string full = Path.GetFullPath(value);
        if (!string.Equals(full, value, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The trusted artifact path must already be canonical.",
                nameof(value));
        }

        string? root = Path.GetPathRoot(full);
        if (root is null ||
            root.Length != 3 ||
            NativeMethods.GetDriveType(root) != NativeMethods.DriveFixed)
        {
            throw new PlatformNotSupportedException(
                "The trusted artifact must be on a fixed local drive.");
        }

        return full;
    }

    private static ArtifactMetadata ValidateMetadata(
        SafeFileHandle file,
        string expectedPath)
    {
        if (NativeMethods.GetFileType(file) != NativeMethods.FileTypeDisk)
        {
            throw new SecurityException(
                "The trusted artifact is not a disk file.");
        }

        NativeMethods.FileAttributeTagInfo attributes =
            ReadAttributeInformation(file);
        NativeMethods.FileStandardInfo standard =
            ReadStandardInformation(file);
        if ((attributes.FileAttributes &
                (NativeMethods.FileAttributeDirectory |
                    NativeMethods.FileAttributeReparsePoint)) != 0 ||
            standard.Directory != 0 ||
            standard.DeletePending != 0 ||
            standard.NumberOfLinks != 1 ||
            standard.EndOfFile < 0)
        {
            throw new SecurityException(
                "The trusted artifact is not a single-link, non-reparse regular file.");
        }

        RequireFinalPath(file, expectedPath);
        RequireLocalVolume(file);
        if (NativeMethods.GetFileSizeEx(file, out long fileSize) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading the trusted artifact length failed");
        }

        if (fileSize != standard.EndOfFile)
        {
            throw new SecurityException(
                "The trusted artifact reported inconsistent lengths.");
        }

        return new ArtifactMetadata(
            fileSize,
            ReadIdentity(file));
    }

    private static void RequireExpectedLength(
        ArtifactMetadata metadata,
        long expectedLength)
    {
        if (metadata.Length != expectedLength)
        {
            throw new SecurityException(
                "The trusted artifact length did not match.");
        }
    }

    private static void RequireUnchanged(
        ArtifactMetadata before,
        ArtifactMetadata after,
        long expectedLength)
    {
        RequireExpectedLength(after, expectedLength);
        RequireIdentity(
            before.Identity,
            after.Identity,
            "The trusted artifact identity changed during validation.");
    }

    private static void RequireIdentity(
        TrustedArtifactFileIdentity expected,
        TrustedArtifactFileIdentity actual,
        string message)
    {
        if (!expected.Equals(actual))
        {
            throw new SecurityException(message);
        }
    }

    private static unsafe NativeMethods.FileAttributeTagInfo
        ReadAttributeInformation(SafeFileHandle file)
    {
        NativeMethods.FileAttributeTagInfo information = new();
        if (NativeMethods.GetFileInformationByHandleEx(
                file,
                NativeMethods.FileAttributeTagInfoClass,
                &information,
                checked((uint)sizeof(NativeMethods.FileAttributeTagInfo))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading trusted artifact attributes failed");
        }

        return information;
    }

    private static unsafe NativeMethods.FileStandardInfo
        ReadStandardInformation(SafeFileHandle file)
    {
        NativeMethods.FileStandardInfo information = new();
        if (NativeMethods.GetFileInformationByHandleEx(
                file,
                NativeMethods.FileStandardInfoClass,
                &information,
                checked((uint)sizeof(NativeMethods.FileStandardInfo))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading trusted artifact link information failed");
        }

        return information;
    }

    private static unsafe TrustedArtifactFileIdentity ReadIdentity(
        SafeFileHandle file)
    {
        NativeMethods.FileIdInfo information = new();
        if (NativeMethods.GetFileInformationByHandleEx(
                file,
                NativeMethods.FileIdInfoClass,
                &information,
                checked((uint)sizeof(NativeMethods.FileIdInfo))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading trusted artifact identity failed");
        }

        ReadOnlySpan<byte> identifier = new(
            information.FileId.Identifier,
            TrustedArtifactFileIdentity.IdentifierLength);
        return new TrustedArtifactFileIdentity(
            information.VolumeSerialNumber,
            BinaryPrimitives.ReadUInt64LittleEndian(identifier),
            BinaryPrimitives.ReadUInt64LittleEndian(identifier[sizeof(ulong)..]));
    }

    private static unsafe void RequireFinalPath(
        SafeFileHandle file,
        string expectedPath)
    {
        uint required = NativeMethods.GetFinalPathNameByHandle(
            file,
            null,
            0,
            0);
        if (required == 0 || required > 32_768)
        {
            throw NativeMethods.Win32Failure(
                "Reading the trusted artifact final path failed");
        }

        char[] buffer = new char[checked((int)required)];
        fixed (char* pointer = buffer)
        {
            uint written = NativeMethods.GetFinalPathNameByHandle(
                file,
                pointer,
                checked((uint)buffer.Length),
                0);
            if (written == 0 || written >= buffer.Length)
            {
                throw NativeMethods.Win32Failure(
                    "Reading the trusted artifact final path failed");
            }

            string actual = new(pointer, 0, checked((int)written));
            string expected = "\\\\?\\" + expectedPath;
            if (!string.Equals(
                    actual,
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException(
                    "The trusted artifact path resolved to an unexpected target.");
            }
        }
    }

    private static unsafe void RequireLocalVolume(SafeFileHandle file)
    {
        uint required = NativeMethods.GetFinalPathNameByHandle(
            file,
            null,
            0,
            NativeMethods.VolumeNameGuid);
        if (required == 0 || required > 32_768)
        {
            throw new PlatformNotSupportedException(
                "The trusted artifact is not on a local Mount Manager volume.");
        }

        char[] buffer = new char[checked((int)required)];
        fixed (char* pointer = buffer)
        {
            uint written = NativeMethods.GetFinalPathNameByHandle(
                file,
                pointer,
                checked((uint)buffer.Length),
                NativeMethods.VolumeNameGuid);
            if (written == 0 || written >= buffer.Length)
            {
                throw new PlatformNotSupportedException(
                    "The trusted artifact is not on a local Mount Manager volume.");
            }

            string volumePath = new(pointer, 0, checked((int)written));
            if (!volumePath.StartsWith(
                    "\\\\?\\Volume{",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new PlatformNotSupportedException(
                    "The trusted artifact is not on a local Mount Manager volume.");
            }
        }
    }

    private static byte[] HashExact(
        SafeFileHandle file,
        long expectedLength,
        MonotonicDeadline? deadline,
        CancellationToken cancellationToken)
    {
        const int BufferLength = 64 * 1024;
        byte[] buffer = new byte[BufferLength];
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        try
        {
            long offset = 0;
            while (offset < expectedLength)
            {
                CheckOperation(deadline, cancellationToken);
                int requested = checked((int)Math.Min(
                    buffer.Length,
                    expectedLength - offset));
                int read = RandomAccess.Read(
                    file,
                    buffer.AsSpan(0, requested),
                    offset);
                if (read <= 0)
                {
                    throw new EndOfStreamException(
                        "The trusted artifact ended before its expected length.");
                }

                hash.AppendData(buffer, 0, read);
                offset += read;
                CheckOperation(deadline, cancellationToken);
            }

            CheckOperation(deadline, cancellationToken);
            Span<byte> trailing = stackalloc byte[1];
            if (RandomAccess.Read(file, trailing, expectedLength) != 0)
            {
                throw new SecurityException(
                    "The trusted artifact exceeded its expected length.");
            }

            CheckOperation(deadline, cancellationToken);
            return hash.GetHashAndReset();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private static void CheckOperation(
        MonotonicDeadline? deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (deadline is MonotonicDeadline activeDeadline)
        {
            _ = activeDeadline.GetRemaining();
        }
    }

    private readonly record struct ArtifactMetadata(
        long Length,
        TrustedArtifactFileIdentity Identity);
}

internal readonly struct TrustedArtifactFileIdentity :
    IEquatable<TrustedArtifactFileIdentity>
{
    internal const int IdentifierLength = 16;
    private readonly ulong identifierLow;
    private readonly ulong identifierHigh;

    internal TrustedArtifactFileIdentity(
        ulong volumeSerialNumber,
        ulong identifierLow,
        ulong identifierHigh)
    {
        VolumeSerialNumber = volumeSerialNumber;
        this.identifierLow = identifierLow;
        this.identifierHigh = identifierHigh;
    }

    internal ulong VolumeSerialNumber { get; }

    internal byte[] CopyIdentifier()
    {
        byte[] result = new byte[IdentifierLength];
        BinaryPrimitives.WriteUInt64LittleEndian(result, identifierLow);
        BinaryPrimitives.WriteUInt64LittleEndian(
            result.AsSpan(sizeof(ulong)),
            identifierHigh);
        return result;
    }

    public bool Equals(TrustedArtifactFileIdentity other)
    {
        return VolumeSerialNumber == other.VolumeSerialNumber &&
            identifierLow == other.identifierLow &&
            identifierHigh == other.identifierHigh;
    }

    public override bool Equals(object? value)
    {
        return value is TrustedArtifactFileIdentity other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            VolumeSerialNumber,
            identifierLow,
            identifierHigh);
    }
}

/// <summary>
/// Owns the retained trusted-artifact handle and verified immutable identity.
/// Revalidation detects path or content drift but does not make a later
/// path-based process launch atomic.
/// </summary>
internal sealed class TrustedArtifactLease : IDisposable
{
    private readonly object gate = new();
    private readonly SafeFileHandle handle;
    private readonly byte[] sha256Digest;
    private bool disposed;

    internal TrustedArtifactLease(
        SafeFileHandle handle,
        string path,
        long length,
        byte[] sha256Digest,
        TrustedArtifactFileIdentity identity)
    {
        this.handle = handle;
        Path = path;
        Length = length;
        this.sha256Digest = sha256Digest;
        Identity = identity;
    }

    internal string Path { get; }

    internal long Length { get; }

    internal TrustedArtifactFileIdentity Identity { get; }

    internal byte[] CopySha256Digest()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return (byte[])sha256Digest.Clone();
        }
    }

    /// <summary>
    /// Copies the exact retained default-stream bytes under one caller-owned
    /// operation budget. The retained identity is revalidated before and after
    /// the read. The returned snapshot is independently hashed against the
    /// authenticated artifact digest; every failed snapshot is wiped.
    /// </summary>
    internal byte[] CopyExactBytes(
        int maximumLength,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        return CopyExactBytes(
            maximumLength,
            deadline,
            cancellationToken,
            testHook: null);
    }

    /// <summary>
    /// Test-only overload. The hook must not mutate the borrowed snapshot. A
    /// test may retain its reference only to assert zeroing after a forced
    /// failure; it must never use the reference after successful return.
    /// </summary>
    internal byte[] CopyExactBytes(
        int maximumLength,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken,
        Action<TrustedArtifactCopyStage, byte[]>? testHook)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            if (maximumLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumLength));
            }

            if (Length > maximumLength || Length > int.MaxValue)
            {
                throw new SecurityException(
                    "The trusted artifact exceeds the retained-copy bound.");
            }

            TrustedArtifactIdentity.Revalidate(
                handle,
                Path,
                Length,
                sha256Digest,
                Identity,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);

            byte[]? snapshot = new byte[checked((int)Length)];
            byte[]? snapshotDigest = null;
            try
            {
                testHook?.Invoke(
                    TrustedArtifactCopyStage.SnapshotAllocated,
                    snapshot);
                const int CopyChunkLength = 64 * 1024;
                int offset = 0;
                while (offset < snapshot.Length)
                {
                    CheckOperation(deadline, cancellationToken);
                    int requested = Math.Min(
                        CopyChunkLength,
                        snapshot.Length - offset);
                    int read = RandomAccess.Read(
                        handle,
                        snapshot.AsSpan(offset, requested),
                        offset);
                    if (read <= 0)
                    {
                        throw new EndOfStreamException(
                            "The retained trusted artifact ended before its exact length.");
                    }

                    offset = checked(offset + read);
                    CheckOperation(deadline, cancellationToken);
                }

                testHook?.Invoke(
                    TrustedArtifactCopyStage.SnapshotRead,
                    snapshot);

                CheckOperation(deadline, cancellationToken);
                Span<byte> trailing = stackalloc byte[1];
                if (RandomAccess.Read(handle, trailing, Length) != 0)
                {
                    throw new SecurityException(
                        "The retained trusted artifact exceeded its exact length.");
                }

                CheckOperation(deadline, cancellationToken);
                TrustedArtifactIdentity.Revalidate(
                    handle,
                    Path,
                    Length,
                    sha256Digest,
                    Identity,
                    deadline,
                    cancellationToken);
                CheckOperation(deadline, cancellationToken);
                snapshotDigest = SHA256.HashData(snapshot);
                CheckOperation(deadline, cancellationToken);
                if (!CryptographicOperations.FixedTimeEquals(
                        snapshotDigest,
                        sha256Digest))
                {
                    throw new SecurityException(
                        "The retained trusted-artifact snapshot digest changed.");
                }

                CheckOperation(deadline, cancellationToken);
                testHook?.Invoke(
                    TrustedArtifactCopyStage.BeforeReturn,
                    snapshot);
                ThrowIfDisposed();
                CheckOperation(deadline, cancellationToken);
                CryptographicOperations.ZeroMemory(snapshotDigest);
                snapshotDigest = SHA256.HashData(snapshot);
                if (!CryptographicOperations.FixedTimeEquals(
                        snapshotDigest,
                        sha256Digest))
                {
                    throw new SecurityException(
                        "The retained trusted-artifact snapshot changed before ownership transfer.");
                }

                CheckOperation(deadline, cancellationToken);
                byte[] result = snapshot;
                snapshot = null;
                return result;
            }
            finally
            {
                if (snapshot is not null)
                {
                    CryptographicOperations.ZeroMemory(snapshot);
                }

                if (snapshotDigest is not null)
                {
                    CryptographicOperations.ZeroMemory(snapshotDigest);
                }
            }
        }
    }

    internal void RevalidateCurrentPath()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            TrustedArtifactIdentity.Revalidate(
                handle,
                Path,
                Length,
                sha256Digest,
                Identity);
        }
    }


    internal void RevalidateCurrentPath(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            TrustedArtifactIdentity.Revalidate(
                handle,
                Path,
                Length,
                sha256Digest,
                Identity,
                deadline,
                cancellationToken);
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
            try
            {
                handle.Dispose();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(sha256Digest);
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(TrustedArtifactLease));
        }
    }

    private static void CheckOperation(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.GetRemaining();
    }
}

/// <summary>
/// Test-only stages within the retained exact-byte-copy ownership envelope.
/// </summary>
internal enum TrustedArtifactCopyStage
{
    SnapshotAllocated = 1,
    SnapshotRead = 2,
    BeforeReturn = 3,
}
