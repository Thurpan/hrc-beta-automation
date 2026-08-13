using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal enum NativeLaunchPolicyPackageFileStage
{
    DirectoryValidated,
    LeafIdentityEnumerated,
    LeafHandleOpened,
    LeafValidated,
    SnapshotRead,
    PackageAuthenticated,
    BeforeFinalRevalidation,
    BeforeReturn,
}

/// <summary>
/// Retains one authenticated package selected at a fixed leaf in a caller-
/// provisioned guarded directory. The independently supplied package pin is
/// the only package-byte trust input; this lease supplies no pin provenance,
/// freshness, rollback protection, or launch authority.
/// </summary>
internal sealed class NativeLaunchPolicyPackageFileLease : IDisposable
{
    internal const string PackageFileName = "native-launch-policy-v1.bin";

    private const int Sha256Length = 32;
    private readonly object gate = new();
    private readonly string packageFilePath;
    private readonly GuardedDescriptorDirectory directory;
    private readonly SafeFileHandle retainedFile;
    private readonly NativeLaunchPolicyPackageV1 package;
    private readonly byte[] expectedPackagePinSha256;
    private bool disposed;

    private NativeLaunchPolicyPackageFileLease(
        string packageFilePath,
        GuardedDescriptorDirectory directory,
        SafeFileHandle retainedFile,
        NativeLaunchPolicyPackageV1 package,
        byte[] expectedPackagePinSha256)
    {
        this.packageFilePath = packageFilePath;
        this.directory = directory;
        this.retainedFile = retainedFile;
        this.package = package;
        this.expectedPackagePinSha256 = expectedPackagePinSha256;
    }

    internal string PackageFilePath
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return packageFilePath;
            }
        }
    }

    internal NativeLaunchPolicyProfile Profile
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return package.Profile;
            }
        }
    }

    internal ulong Generation
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return package.Generation;
            }
        }
    }

    internal ReleaseArtifactRole ReleaseArtifactRole
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return package.ReleaseArtifactRole;
            }
        }
    }

    internal ReleaseDeploymentKind ReleaseDeploymentKind
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return package.ReleaseDeploymentKind;
            }
        }
    }

    internal ReleaseTargetRuntimeIdentifier TargetRuntimeIdentifier
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return package.TargetRuntimeIdentifier;
            }
        }
    }

    internal TrustedNativeSystemModuleConsumerProfile
        NativeSystemModuleConsumerProfile
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return package.NativeSystemModuleConsumerProfile;
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

    internal static NativeLaunchPolicyPackageFileLease Open(
        string exactExistingDirectoryPath,
        string expectedOwnerSid,
        ReadOnlySpan<byte> expectedPackagePinSha256,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        return Open(
            exactExistingDirectoryPath,
            expectedOwnerSid,
            expectedPackagePinSha256,
            deadline,
            cancellationToken,
            testHook: null);
    }

    /// <summary>
    /// Test-only overload. Only <c>SnapshotRead</c> carries a borrowed byte
    /// array. The hook must not mutate it and may retain it only to verify that
    /// a forced failure wiped the snapshot.
    /// </summary>
    internal static NativeLaunchPolicyPackageFileLease Open(
        string exactExistingDirectoryPath,
        string expectedOwnerSid,
        ReadOnlySpan<byte> expectedPackagePinSha256,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken,
        Action<NativeLaunchPolicyPackageFileStage, byte[]?>? testHook)
    {
        CheckOperation(deadline, cancellationToken);
        if (expectedPackagePinSha256.Length != Sha256Length)
        {
            throw new ArgumentException(
                "The expected native launch-policy package pin must contain exactly 32 bytes.",
                nameof(expectedPackagePinSha256));
        }

        byte[]? ownedExpectedPin = expectedPackagePinSha256.ToArray();
        GuardedDescriptorDirectory? directory = null;
        SafeFileHandle? retainedFile = null;
        NativeLaunchPolicyPackageV1? package = null;
        try
        {
            CheckOperation(deadline, cancellationToken);
            directory = GuardedDescriptorDirectory.OpenExact(
                exactExistingDirectoryPath,
                expectedOwnerSid);
            testHook?.Invoke(
                NativeLaunchPolicyPackageFileStage.DirectoryValidated,
                null);
            CheckOperation(deadline, cancellationToken);

            retainedFile = directory.OpenRetainedExactReadOnlyLeaf(
                PackageFileName,
                NativeLaunchPolicyPackageV1.MinimumEncodedLength,
                NativeLaunchPolicyPackageV1.MaximumEncodedLength,
                deadline,
                cancellationToken,
                testHook);
            package = AuthenticateRetainedFile(
                directory,
                retainedFile,
                ownedExpectedPin,
                deadline,
                cancellationToken,
                testHook);
            testHook?.Invoke(
                NativeLaunchPolicyPackageFileStage.PackageAuthenticated,
                null);
            CheckOperation(deadline, cancellationToken);

            testHook?.Invoke(
                NativeLaunchPolicyPackageFileStage.BeforeFinalRevalidation,
                null);
            CheckOperation(deadline, cancellationToken);
            RevalidateCore(
                directory,
                retainedFile,
                package,
                ownedExpectedPin,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);

            NativeLaunchPolicyPackageFileLease result = new(
                Path.Combine(exactExistingDirectoryPath, PackageFileName),
                directory,
                retainedFile,
                package,
                ownedExpectedPin);
            directory = null;
            retainedFile = null;
            package = null;
            ownedExpectedPin = null;
            try
            {
                testHook?.Invoke(
                    NativeLaunchPolicyPackageFileStage.BeforeReturn,
                    null);
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
            try
            {
                package?.Dispose();
            }
            finally
            {
                try
                {
                    retainedFile?.Dispose();
                }
                finally
                {
                    try
                    {
                        directory?.Dispose();
                    }
                    finally
                    {
                        if (ownedExpectedPin is not null)
                        {
                            CryptographicOperations.ZeroMemory(
                                ownedExpectedPin);
                        }
                    }
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
            CheckOperation(deadline, cancellationToken);
            RevalidateCore(
                directory,
                retainedFile,
                package,
                expectedPackagePinSha256,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
        }
    }

    internal void RequireDistinctApplicationDirectory(
        string canonicalApplicationDirectory,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            directory.RequireDistinctFromCanonicalDirectory(
                canonicalApplicationDirectory,
                deadline,
                cancellationToken);
            RevalidateCore(
                directory,
                retainedFile,
                package,
                expectedPackagePinSha256,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
        }
    }

    internal byte[] CopyCanonicalPackage()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return package.CopyCanonicalPackage();
        }
    }

    internal byte[] CopyPackagePinSha256()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return package.CopyPackagePinSha256();
        }
    }

    internal byte[] CopyCanonicalReleaseManifest()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return package.CopyCanonicalReleaseManifest();
        }
    }

    internal byte[] CopyReleaseManifestPinSha256()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return package.CopyReleaseManifestPinSha256();
        }
    }

    internal byte[] CopyCanonicalNativeSystemModulePolicy()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return package.CopyCanonicalNativeSystemModulePolicy();
        }
    }

    internal byte[] CopyNativeSystemModulePolicyPinSha256()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return package.CopyNativeSystemModulePolicyPinSha256();
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
                package.Dispose();
            }
            finally
            {
                try
                {
                    retainedFile.Dispose();
                }
                finally
                {
                    try
                    {
                        directory.Dispose();
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(
                            expectedPackagePinSha256);
                    }
                }
            }
        }
    }

    private static NativeLaunchPolicyPackageV1 AuthenticateRetainedFile(
        GuardedDescriptorDirectory directory,
        SafeFileHandle retainedFile,
        ReadOnlySpan<byte> expectedPackagePinSha256,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken,
        Action<NativeLaunchPolicyPackageFileStage, byte[]?>? testHook)
    {
        byte[]? snapshot = null;
        NativeLaunchPolicyPackageV1? result = null;
        try
        {
            snapshot = directory.CopyRetainedExactReadOnlyLeaf(
                retainedFile,
                PackageFileName,
                NativeLaunchPolicyPackageV1.MinimumEncodedLength,
                NativeLaunchPolicyPackageV1.MaximumEncodedLength,
                deadline,
                cancellationToken,
                testHook);
            result = NativeLaunchPolicyPackageV1.Authenticate(
                snapshot,
                expectedPackagePinSha256,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
            NativeLaunchPolicyPackageV1 authenticated = result;
            result = null;
            return authenticated;
        }
        finally
        {
            result?.Dispose();
            if (snapshot is not null)
            {
                CryptographicOperations.ZeroMemory(snapshot);
            }
        }
    }

    private static void RevalidateCore(
        GuardedDescriptorDirectory directory,
        SafeFileHandle retainedFile,
        NativeLaunchPolicyPackageV1 expectedPackage,
        ReadOnlySpan<byte> expectedPackagePinSha256,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        NativeLaunchPolicyPackageV1? currentPackage = null;
        byte[]? expectedBytes = null;
        byte[]? currentBytes = null;
        try
        {
            currentPackage = AuthenticateRetainedFile(
                directory,
                retainedFile,
                expectedPackagePinSha256,
                deadline,
                cancellationToken,
                testHook: null);
            expectedBytes = expectedPackage.CopyCanonicalPackage();
            currentBytes = currentPackage.CopyCanonicalPackage();
            if (!CryptographicOperations.FixedTimeEquals(
                    expectedBytes,
                    currentBytes))
            {
                throw new SecurityException(
                    "The retained native launch-policy package bytes changed.");
            }

            CheckOperation(deadline, cancellationToken);
        }
        finally
        {
            currentPackage?.Dispose();
            if (expectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(expectedBytes);
            }

            if (currentBytes is not null)
            {
                CryptographicOperations.ZeroMemory(currentBytes);
            }
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
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
