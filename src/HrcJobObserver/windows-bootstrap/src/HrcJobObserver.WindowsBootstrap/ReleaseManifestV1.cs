using System;
using System.Collections.Generic;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Closed source/test-only roles admitted by release-manifest version 1.
/// Neither value is a production observer or controller role.
/// </summary>
internal enum ReleaseArtifactRole : byte
{
    SyntheticTestHarness = 1,
    SyntheticNativeFixture = 2,
}

/// <summary>
/// Closed deployment shapes admitted by release-manifest version 1. Neither
/// shape establishes release provenance or loader atomicity.
/// </summary>
internal enum ReleaseDeploymentKind : byte
{
    FrameworkDependentSnapshot = 1,
    NativeNoCrtSystem32Fixture = 2,
}

/// <summary>
/// Closed target-runtime policy label authenticated by the manifest pin. It is
/// not an observation of PE headers or proof of actual runtime selection.
/// </summary>
internal enum ReleaseTargetRuntimeIdentifier : ushort
{
    WinX64 = 1,
}

/// <summary>
/// Strict canonical out-of-band description of one synthetic release artifact
/// set. The complete bytes are authenticated only relative to a caller-supplied
/// SHA-256 pin. This structure supplies no signature or release provenance.
/// </summary>
internal sealed class ReleaseManifestV1 : IDisposable
{
    internal const int MaximumEncodedLength = 38_325;
    internal const string NativeFixtureExecutableRelativeFileName =
        "HrcJobObserver.NativeFixture.exe";
    internal const int MinimumEncodedLength = 98;
    private const int Sha256Length = 32;
    private const int MaximumFileNameLength = 255;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HRCREL01");

    private readonly TrustedArtifactExpectation[] artifacts;
    private readonly byte[] artifactSetManifestSha256;

    private ReleaseManifestV1(
        ReleaseArtifactRole artifactRole,
        ReleaseDeploymentKind deploymentKind,
        string executableRelativeFileName,
        TrustedArtifactExpectation[] artifacts,
        byte[] artifactSetManifestSha256)
    {
        ArtifactRole = artifactRole;
        DeploymentKind = deploymentKind;
        ExecutableRelativeFileName = executableRelativeFileName;
        this.artifacts = artifacts;
        this.artifactSetManifestSha256 = artifactSetManifestSha256;
    }

    internal ReleaseArtifactRole ArtifactRole { get; }

    internal ReleaseDeploymentKind DeploymentKind { get; }

    internal ReleaseTargetRuntimeIdentifier TargetRuntimeIdentifier =>
        ReleaseTargetRuntimeIdentifier.WinX64;

    internal bool IsEligibleForTrustedLaunch => false;

    internal string ExecutableRelativeFileName { get; }

    internal int ArtifactCount => artifacts.Length;

    internal TrustedArtifactExpectation[] CopyArtifacts()
    {
        TrustedArtifactExpectation[] copy =
            new TrustedArtifactExpectation[artifacts.Length];
        try
        {
            for (int index = 0; index < artifacts.Length; index++)
            {
                copy[index] = artifacts[index].CloneOwned();
            }

            return copy;
        }
        catch
        {
            foreach (TrustedArtifactExpectation? artifact in copy)
            {
                artifact?.Dispose();
            }

            throw;
        }
    }

    internal byte[] CopyArtifactSetManifestSha256()
    {
        return (byte[])artifactSetManifestSha256.Clone();
    }

    /// <summary>
    /// Performs structural canonical parsing only. The composite lease calls
    /// this only after it authenticates its one owned byte snapshot.
    /// </summary>
    internal static ReleaseManifestV1 ParseStructuralCanonical(
        ReadOnlySpan<byte> encoded,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        if (encoded.Length is < MinimumEncodedLength or > MaximumEncodedLength)
        {
            throw new FormatException(
                "The release manifest encoded length is invalid.");
        }

        BootstrapBinaryReader reader = new(encoded);
        if (!reader.ReadBytes(Magic.Length, "release manifest magic")
                .SequenceEqual(Magic))
        {
            throw new FormatException(
                "The release manifest magic or version is invalid.");
        }

        ReleaseArtifactRole artifactRole = reader.ReadByte(
            "release artifact role") switch
        {
            (byte)ReleaseArtifactRole.SyntheticTestHarness =>
                ReleaseArtifactRole.SyntheticTestHarness,
            (byte)ReleaseArtifactRole.SyntheticNativeFixture =>
                ReleaseArtifactRole.SyntheticNativeFixture,
            _ => throw new FormatException(
                "The release artifact role is not admitted by this schema."),
        };

        ReleaseDeploymentKind deploymentKind = reader.ReadByte(
            "release deployment kind") switch
        {
            (byte)ReleaseDeploymentKind.FrameworkDependentSnapshot =>
                ReleaseDeploymentKind.FrameworkDependentSnapshot,
            (byte)ReleaseDeploymentKind.NativeNoCrtSystem32Fixture =>
                ReleaseDeploymentKind.NativeNoCrtSystem32Fixture,
            _ => throw new FormatException(
                "The release deployment kind is not admitted by this schema."),
        };

        bool profileIsAdmitted =
            artifactRole == ReleaseArtifactRole.SyntheticTestHarness &&
            deploymentKind ==
                ReleaseDeploymentKind.FrameworkDependentSnapshot ||
            artifactRole == ReleaseArtifactRole.SyntheticNativeFixture &&
            deploymentKind ==
                ReleaseDeploymentKind.NativeNoCrtSystem32Fixture;
        if (!profileIsAdmitted)
        {
            throw new FormatException(
                "The release role and deployment-kind pair is not admitted by this schema.");
        }

        if (reader.ReadUInt16("target runtime identifier") !=
            (ushort)ReleaseTargetRuntimeIdentifier.WinX64)
        {
            throw new FormatException(
                "The target runtime identifier is not the fixed win-x64 policy label.");
        }

        reader.RequireZero(sizeof(uint), "release manifest reserved field");
        string executableName = ReadFileName(
            ref reader,
            "designated executable filename");
        uint count = reader.ReadUInt32("release artifact count");
        if (count is 0 or > TrustedArtifactSetLease.MaximumArtifactCount)
        {
            throw new FormatException(
                "The release artifact count is outside the admitted range.");
        }

        if (artifactRole == ReleaseArtifactRole.SyntheticNativeFixture &&
            (!string.Equals(
                executableName,
                NativeFixtureExecutableRelativeFileName,
                StringComparison.Ordinal) ||
                count != 1))
        {
            throw new FormatException(
                "The synthetic native-fixture profile requires its one exact executable artifact.");
        }

        TrustedArtifactExpectation[] artifacts = new TrustedArtifactExpectation[
            checked((int)count)];
        byte[]? artifactSetManifest = null;
        bool transferred = false;
        try
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            string? previousName = null;
            bool containsExecutable = false;
            for (int index = 0; index < artifacts.Length; index++)
            {
                CheckOperation(deadline, cancellationToken);
                string name = ReadFileName(
                    ref reader,
                    $"artifact {index} filename");
                if (previousName is not null &&
                    string.CompareOrdinal(previousName, name) >= 0)
                {
                    throw new FormatException(
                        "Release artifact filenames must be strictly ordinally sorted.");
                }

                if (!names.Add(name))
                {
                    throw new FormatException(
                        "Release artifact filenames must not have case collisions.");
                }

                ulong encodedLength = reader.ReadUInt64(
                    $"artifact {index} length");
                if (encodedLength > long.MaxValue)
                {
                    throw new FormatException(
                        "A release artifact length exceeds the admitted range.");
                }

                ReadOnlySpan<byte> digest = reader.ReadBytes(
                    Sha256Length,
                    $"artifact {index} SHA-256");
                try
                {
                    artifacts[index] = new TrustedArtifactExpectation(
                        name,
                        checked((long)encodedLength),
                        digest);
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException(
                        "A release artifact entry is not canonical.",
                        exception);
                }

                containsExecutable |= string.Equals(
                    name,
                    executableName,
                    StringComparison.Ordinal);
                previousName = name;
                CheckOperation(deadline, cancellationToken);
            }

            if (!containsExecutable)
            {
                throw new FormatException(
                    "The designated executable is not an exact release artifact.");
            }

            if (artifactRole == ReleaseArtifactRole.SyntheticNativeFixture &&
                !string.Equals(
                    artifacts[0].RelativeFileName,
                    NativeFixtureExecutableRelativeFileName,
                    StringComparison.Ordinal))
            {
                throw new FormatException(
                    "The synthetic native-fixture member filename is not exact.");
            }

            artifactSetManifest = reader.ReadBytes(
                Sha256Length,
                "protected artifact-set manifest SHA-256").ToArray();
            reader.RequireEnd();
            CheckOperation(deadline, cancellationToken);
            ReleaseManifestV1 result = new(
                artifactRole,
                deploymentKind,
                executableName,
                artifacts,
                artifactSetManifest);
            artifactSetManifest = null;
            transferred = true;
            return result;
        }
        finally
        {
            if (!transferred)
            {
                foreach (TrustedArtifactExpectation? artifact in artifacts)
                {
                    artifact?.Dispose();
                }

                if (artifactSetManifest is not null)
                {
                    CryptographicOperations.ZeroMemory(artifactSetManifest);
                }
            }
        }
    }

    private static string ReadFileName(
        ref BootstrapBinaryReader reader,
        string fieldName)
    {
        ushort length = reader.ReadUInt16(fieldName + " length");
        ReadOnlySpan<byte> encoded = reader.ReadBytes(length, fieldName);
        if (encoded.Length is 0 or > MaximumFileNameLength)
        {
            throw new FormatException(
                $"The {fieldName} length is invalid.");
        }

        foreach (byte value in encoded)
        {
            if (value is < 0x20 or > 0x7e)
            {
                throw new FormatException(
                    $"The {fieldName} is not canonical printable ASCII.");
            }
        }

        string name = Encoding.ASCII.GetString(encoded);
        try
        {
            return TrustedArtifactSetLease.ValidateRelativeFileName(
                name,
                fieldName);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException(
                $"The {fieldName} is not a canonical Windows filename.",
                exception);
        }
    }

    internal byte[] CopyExecutableSha256Digest()
    {
        TrustedArtifactExpectation executable = Array.Find(
            artifacts,
            artifact => string.Equals(
                artifact.RelativeFileName,
                ExecutableRelativeFileName,
                StringComparison.Ordinal)) ??
            throw new InvalidOperationException(
                "The parsed release manifest has no exact executable artifact.");
        return executable.CopySha256Digest();
    }

    public void Dispose()
    {
        foreach (TrustedArtifactExpectation artifact in artifacts)
        {
            artifact.Dispose();
        }

        CryptographicOperations.ZeroMemory(artifactSetManifestSha256);
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
/// Retains the exact protected artifact set described by one canonical,
/// caller-pinned out-of-band manifest. Pin provenance belongs to the caller.
/// This snapshot/detection boundary does not provide signatures, loader
/// atomicity, shared-runtime trust, launch eligibility, or production roles.
/// </summary>
internal sealed class PinnedReleaseArtifactSetLease : IDisposable
{
    private readonly object gate = new();
    private readonly TrustedArtifactSetLease artifactSet;
    private readonly byte[] manifestPinSha256;
    private readonly byte[] artifactSetManifestSha256;
    private readonly byte[] executableSha256;
    private bool disposed;

    private PinnedReleaseArtifactSetLease(
        TrustedArtifactSetLease artifactSet,
        AuthenticatedReleaseManifestV1 manifest,
        byte[] manifestPinSha256,
        byte[] artifactSetManifestSha256)
    {
        this.artifactSet = artifactSet;
        this.manifestPinSha256 = manifestPinSha256;
        this.artifactSetManifestSha256 = artifactSetManifestSha256;
        executableSha256 = manifest.CopyExecutableSha256Digest();
        ArtifactRole = manifest.ArtifactRole;
        DeploymentKind = manifest.DeploymentKind;
        TargetRuntimeIdentifier = manifest.TargetRuntimeIdentifier;
    }

    internal ReleaseArtifactRole ArtifactRole { get; }

    internal ReleaseDeploymentKind DeploymentKind { get; }

    internal ReleaseTargetRuntimeIdentifier TargetRuntimeIdentifier { get; }

    internal bool IsEligibleForTrustedLaunch => false;

    internal string ApplicationDirectory => artifactSet.ApplicationDirectory;

    internal string ExecutableRelativeFileName =>
        artifactSet.ExecutableRelativeFileName;

    internal string ExecutablePath => artifactSet.ExecutablePath;

    internal int Count => artifactSet.Count;

    internal byte[] CopyExactExecutableBytes(
        int maximumLength,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            return artifactSet.CopyExactExecutableBytes(
                maximumLength,
                deadline,
                cancellationToken);
        }
    }

    internal TrustedArtifactLaunchNamespaceLease
        OpenExecutableLaunchNamespaceLease(
            MonotonicDeadline deadline,
            CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            return artifactSet.OpenExecutableLaunchNamespaceLease(
                deadline,
                cancellationToken);
        }
    }

    internal static PinnedReleaseArtifactSetLease Open(
        string exactApplicationDirectory,
        ReadOnlySpan<byte> canonicalManifest,
        ReadOnlySpan<byte> expectedManifestPinSha256,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        AuthenticatedReleaseManifestV1? manifest = null;
        byte[]? retainedManifestPin = null;
        byte[]? artifactSetManifestIdentity = null;
        TrustedArtifactSetLease? artifactSet = null;
        try
        {
            manifest = AuthenticatedReleaseManifestV1.Authenticate(
                canonicalManifest,
                expectedManifestPinSha256,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
            TrustedArtifactExpectation[] artifacts = manifest.CopyArtifacts();
            try
            {
                artifactSet = TrustedArtifactSetLease.Open(
                    exactApplicationDirectory,
                    manifest.ExecutableRelativeFileName,
                    artifacts,
                    deadline,
                    cancellationToken);
            }
            finally
            {
                foreach (TrustedArtifactExpectation artifact in artifacts)
                {
                    artifact.Dispose();
                }
            }

            artifactSetManifestIdentity =
                manifest.CopyArtifactSetManifestSha256();
            byte[] actualArtifactSetManifest =
                artifactSet.CopyManifestSha256();
            try
            {
                CheckOperation(deadline, cancellationToken);
                if (!CryptographicOperations.FixedTimeEquals(
                        actualArtifactSetManifest,
                        artifactSetManifestIdentity))
                {
                    throw new SecurityException(
                        "The release manifest does not bind the opened protected artifact set.");
                }

                CheckOperation(deadline, cancellationToken);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actualArtifactSetManifest);
            }

            artifactSet.RevalidateExactSet(deadline, cancellationToken);
            CheckOperation(deadline, cancellationToken);
            retainedManifestPin = manifest.CopyManifestPinSha256();
            PinnedReleaseArtifactSetLease result = new(
                artifactSet,
                manifest,
                retainedManifestPin,
                artifactSetManifestIdentity);
            artifactSet = null;
            retainedManifestPin = null;
            artifactSetManifestIdentity = null;
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
            try
            {
                artifactSet?.Dispose();
            }
            finally
            {
                manifest?.Dispose();
                if (retainedManifestPin is not null)
                {
                    CryptographicOperations.ZeroMemory(retainedManifestPin);
                }

                if (artifactSetManifestIdentity is not null)
                {
                    CryptographicOperations.ZeroMemory(
                        artifactSetManifestIdentity);
                }
            }
        }
    }

    internal byte[] CopyManifestPinSha256()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return (byte[])manifestPinSha256.Clone();
        }
    }

    internal byte[] CopyArtifactSetManifestSha256()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return (byte[])artifactSetManifestSha256.Clone();
        }
    }

    internal byte[] CopyExecutableSha256Digest()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return (byte[])executableSha256.Clone();
        }
    }

    internal void RevalidateExactSet(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            artifactSet.RevalidateExactSet(deadline, cancellationToken);
            CheckOperation(deadline, cancellationToken);
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
                artifactSet.Dispose();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(manifestPinSha256);
                CryptographicOperations.ZeroMemory(
                    artifactSetManifestSha256);
                CryptographicOperations.ZeroMemory(executableSha256);
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
        if (disposed)
        {
            throw new ObjectDisposedException(
                nameof(PinnedReleaseArtifactSetLease));
        }
    }
}
