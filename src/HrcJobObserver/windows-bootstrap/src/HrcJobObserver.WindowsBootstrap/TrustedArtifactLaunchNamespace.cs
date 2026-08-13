using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Pins the complete canonical ancestor chain of one retained trusted artifact
/// against delete sharing. It supplies a handle-derived volume-GUID launch
/// path and validates a later process-reported DOS path against the retained
/// file identity. This is a namespace/path-to-file binding, not proof of the
/// kernel image section's identity.
/// </summary>
internal sealed class TrustedArtifactLaunchNamespaceLease : IDisposable
{
    private const int MaximumAncestorCount = 256;
    private const int MaximumFinalPathChars = 32_768;
    private const string VolumeGuidPrefix = "\\\\?\\Volume{";

    private readonly object gate = new();
    private readonly SafeFileHandle retainedArtifact;
    private readonly PinnedAncestor[] ancestors;
    private readonly byte[] sha256Digest;
    private bool retainedArtifactReference;
    private bool disposed;

    private TrustedArtifactLaunchNamespaceLease(
        SafeFileHandle retainedArtifact,
        string canonicalDosExecutablePath,
        long length,
        byte[] sha256Digest,
        TrustedArtifactFileIdentity identity,
        PinnedAncestor[] ancestors,
        string volumeGuidExecutablePath)
    {
        this.retainedArtifact = retainedArtifact;
        retainedArtifactReference = true;
        CanonicalDosExecutablePath = canonicalDosExecutablePath;
        CanonicalDosDirectory = Path.GetDirectoryName(
            canonicalDosExecutablePath) ??
            throw new SecurityException(
                "The trusted launch executable directory is unavailable.");
        Length = length;
        this.sha256Digest = sha256Digest;
        Identity = identity;
        this.ancestors = ancestors;
        VolumeGuidExecutablePath = volumeGuidExecutablePath;
        VolumeGuidDirectory = Path.GetDirectoryName(volumeGuidExecutablePath) ??
            throw new SecurityException(
                "The trusted launch volume-GUID directory is unavailable.");
        if (!VolumeGuidDirectory.StartsWith(
                VolumeGuidPrefix,
                StringComparison.OrdinalIgnoreCase) ||
            VolumeGuidDirectory.Contains('"'))
        {
            throw new SecurityException(
                "The trusted launch volume-GUID directory is invalid.");
        }
    }

    internal string CanonicalDosExecutablePath { get; }

    internal string CanonicalDosDirectory { get; }

    internal string VolumeGuidExecutablePath { get; }

    internal string VolumeGuidDirectory { get; }

    internal long Length { get; }

    private TrustedArtifactFileIdentity Identity { get; }

    internal static TrustedArtifactLaunchNamespaceLease Open(
        SafeFileHandle retainedArtifact,
        string canonicalDosExecutablePath,
        long length,
        ReadOnlySpan<byte> sha256Digest,
        TrustedArtifactFileIdentity identity,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(retainedArtifact);
        CheckOperation(deadline, cancellationToken);
        bool retainedReference = false;
        byte[]? ownedDigest = null;
        List<PinnedAncestor> pinned = new();
        try
        {
            retainedArtifact.DangerousAddRef(ref retainedReference);
            if (!retainedReference || retainedArtifact.IsInvalid ||
                retainedArtifact.IsClosed)
            {
                throw new ObjectDisposedException(nameof(TrustedArtifactLease));
            }

            ownedDigest = sha256Digest.ToArray();
            foreach (string ancestorPath in BuildAncestorPaths(
                         canonicalDosExecutablePath))
            {
                CheckOperation(deadline, cancellationToken);
                SafeFileHandle handle = OpenPinnedAncestor(ancestorPath);
                try
                {
                    TrustedArtifactFileIdentity ancestorIdentity =
                        ValidatePinnedAncestor(handle, ancestorPath);
                    pinned.Add(new PinnedAncestor(
                        ancestorPath,
                        handle,
                        ancestorIdentity));
                    handle = null!;
                }
                finally
                {
                    handle?.Dispose();
                }
            }

            CheckOperation(deadline, cancellationToken);
            RevalidateAncestors(pinned, deadline, cancellationToken);
            TrustedArtifactIdentity.Revalidate(
                retainedArtifact,
                canonicalDosExecutablePath,
                length,
                ownedDigest,
                identity,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
            string volumeGuidPath = ReadVolumeGuidPath(
                retainedArtifact,
                canonicalDosExecutablePath);
            RevalidateAncestors(pinned, deadline, cancellationToken);
            CheckOperation(deadline, cancellationToken);

            TrustedArtifactLaunchNamespaceLease result = new(
                retainedArtifact,
                canonicalDosExecutablePath,
                length,
                ownedDigest,
                identity,
                pinned.ToArray(),
                volumeGuidPath);
            retainedReference = false;
            ownedDigest = null;
            pinned.Clear();
            return result;
        }
        finally
        {
            for (int index = pinned.Count - 1; index >= 0; index--)
            {
                pinned[index].Handle.Dispose();
            }

            if (ownedDigest is not null)
            {
                CryptographicOperations.ZeroMemory(ownedDigest);
            }

            if (retainedReference)
            {
                retainedArtifact.DangerousRelease();
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
            RevalidateCore(deadline, cancellationToken);
        }
    }

    internal void ValidateDebugImageFileHandle(
        SafeFileHandle debugImageFile,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            RevalidateCore(deadline, cancellationToken);
            TrustedArtifactIdentity.ValidateExactHandle(
                debugImageFile,
                Length,
                sha256Digest,
                Identity,
                deadline,
                cancellationToken);
            string debugVolumeGuidPath = ReadVolumeGuidPath(
                debugImageFile,
                CanonicalDosExecutablePath);
            if (!string.Equals(
                    debugVolumeGuidPath,
                    VolumeGuidExecutablePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException(
                    "The debug-event image handle did not name the retained volume-GUID path.");
            }

            CheckOperation(deadline, cancellationToken);
            RevalidateCore(deadline, cancellationToken);
        }
    }

    internal void ValidateReportedImagePath(
        string reportedImagePath,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reportedImagePath);
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            RevalidateCore(deadline, cancellationToken);
            if (!string.Equals(
                    reportedImagePath,
                    CanonicalDosExecutablePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException(
                    "The process-reported image path did not corroborate the retained executable path.");
            }

            RevalidateCore(deadline, cancellationToken);
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
            for (int index = ancestors.Length - 1; index >= 0; index--)
            {
                ancestors[index].Handle.Dispose();
            }

            CryptographicOperations.ZeroMemory(sha256Digest);
            if (retainedArtifactReference)
            {
                retainedArtifactReference = false;
                retainedArtifact.DangerousRelease();
            }
        }
    }

    private void RevalidateCore(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        RevalidateAncestors(ancestors, deadline, cancellationToken);
        TrustedArtifactIdentity.Revalidate(
            retainedArtifact,
            CanonicalDosExecutablePath,
            Length,
            sha256Digest,
            Identity,
            deadline,
            cancellationToken);
        CheckOperation(deadline, cancellationToken);
        string currentVolumeGuidPath = ReadVolumeGuidPath(
            retainedArtifact,
            CanonicalDosExecutablePath);
        if (!string.Equals(
                currentVolumeGuidPath,
                VolumeGuidExecutablePath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                "The retained executable volume-GUID path changed.");
        }

        RevalidateAncestors(ancestors, deadline, cancellationToken);
        CheckOperation(deadline, cancellationToken);
    }

    private static string[] BuildAncestorPaths(string executablePath)
    {
        string root = Path.GetPathRoot(executablePath) ??
            throw new ArgumentException(
                "The trusted launch executable has no local root.",
                nameof(executablePath));
        string? current = Path.GetDirectoryName(executablePath);
        List<string> reversed = new();
        while (current is not null)
        {
            if (reversed.Count == MaximumAncestorCount)
            {
                throw new SecurityException(
                    "The trusted launch namespace has too many ancestors.");
            }

            reversed.Add(current);
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = Path.GetDirectoryName(current);
        }

        if (reversed.Count == 0 ||
            !string.Equals(
                reversed[^1],
                root,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                "The trusted launch namespace did not terminate at its fixed-drive root.");
        }

        reversed.Reverse();
        return reversed.ToArray();
    }

    private static SafeFileHandle OpenPinnedAncestor(string path)
    {
        nint raw = NativeMethods.CreateFile(
            path,
            NativeMethods.FileReadAttributes,
            NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
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
                "Opening a pinned trusted-launch ancestor failed");
        }

        return handle;
    }

    private static void RevalidateAncestors(
        IReadOnlyList<PinnedAncestor> pinned,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        for (int index = 0; index < pinned.Count; index++)
        {
            CheckOperation(deadline, cancellationToken);
            PinnedAncestor ancestor = pinned[index];
            TrustedArtifactFileIdentity current = ValidatePinnedAncestor(
                ancestor.Handle,
                ancestor.Path);
            if (!current.Equals(ancestor.Identity))
            {
                throw new SecurityException(
                    "A retained trusted-launch ancestor identity changed.");
            }
        }
    }

    private static unsafe TrustedArtifactFileIdentity ValidatePinnedAncestor(
        SafeFileHandle handle,
        string expectedDosPath)
    {
        if (NativeMethods.GetFileType(handle) != NativeMethods.FileTypeDisk)
        {
            throw new SecurityException(
                "A trusted-launch ancestor is not a disk directory.");
        }

        NativeMethods.FileAttributeTagInfo attributes = default;
        if (NativeMethods.GetFileInformationByHandleEx(
                handle,
                NativeMethods.FileAttributeTagInfoClass,
                &attributes,
                checked((uint)sizeof(NativeMethods.FileAttributeTagInfo))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading trusted-launch ancestor attributes failed");
        }

        NativeMethods.FileStandardInfo standard = default;
        if (NativeMethods.GetFileInformationByHandleEx(
                handle,
                NativeMethods.FileStandardInfoClass,
                &standard,
                checked((uint)sizeof(NativeMethods.FileStandardInfo))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading trusted-launch ancestor link information failed");
        }

        if ((attributes.FileAttributes & NativeMethods.FileAttributeDirectory) == 0 ||
            (attributes.FileAttributes &
                NativeMethods.FileAttributeReparsePoint) != 0 ||
            standard.Directory == 0 ||
            standard.DeletePending != 0)
        {
            throw new SecurityException(
                "A trusted-launch ancestor is not a stable non-reparse directory.");
        }

        RequireDosFinalPath(handle, expectedDosPath);
        _ = ReadVolumeGuidPath(handle, expectedDosPath);

        NativeMethods.FileIdInfo identity = default;
        if (NativeMethods.GetFileInformationByHandleEx(
                handle,
                NativeMethods.FileIdInfoClass,
                &identity,
                checked((uint)sizeof(NativeMethods.FileIdInfo))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading trusted-launch ancestor identity failed");
        }

        ReadOnlySpan<byte> identifier = new(
            identity.FileId.Identifier,
            TrustedArtifactFileIdentity.IdentifierLength);
        return new TrustedArtifactFileIdentity(
            identity.VolumeSerialNumber,
            BinaryPrimitives.ReadUInt64LittleEndian(identifier),
            BinaryPrimitives.ReadUInt64LittleEndian(
                identifier[sizeof(ulong)..]));
    }

    private static unsafe void RequireDosFinalPath(
        SafeFileHandle handle,
        string expectedDosPath)
    {
        string actual = ReadFinalPath(handle, 0);
        string expected = "\\\\?\\" + expectedDosPath;
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                "A trusted-launch ancestor resolved to an unexpected DOS path.");
        }
    }

    private static string ReadVolumeGuidPath(
        SafeFileHandle handle,
        string expectedDosPath)
    {
        string path = ReadFinalPath(handle, NativeMethods.VolumeNameGuid);
        if (path.Length < 49 ||
            !path.StartsWith(VolumeGuidPrefix, StringComparison.OrdinalIgnoreCase) ||
            path[47] != '}' ||
            path[48] != Path.DirectorySeparatorChar ||
            !Guid.TryParseExact(path.Substring(11, 36), "D", out _) ||
            !path.AsSpan(48).Equals(
                expectedDosPath.AsSpan(2),
                StringComparison.OrdinalIgnoreCase) ||
            path.Contains('"'))
        {
            throw new PlatformNotSupportedException(
                "The trusted launch path is not an exact local volume-GUID path.");
        }

        return path;
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
        if (required == 0 || required > MaximumFinalPathChars)
        {
            throw NativeMethods.Win32Failure(
                "Reading a trusted-launch final path failed");
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
                throw NativeMethods.Win32Failure(
                    "Reading a trusted-launch final path failed");
            }

            return new string(pointer, 0, checked((int)written));
        }
    }

    private static void CheckOperation(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.GetRemaining();
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(
                nameof(TrustedArtifactLaunchNamespaceLease));
        }
    }

    private readonly record struct PinnedAncestor(
        string Path,
        SafeFileHandle Handle,
        TrustedArtifactFileIdentity Identity);
}
