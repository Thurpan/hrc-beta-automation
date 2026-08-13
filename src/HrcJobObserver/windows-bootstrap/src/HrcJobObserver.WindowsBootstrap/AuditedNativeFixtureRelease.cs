using System;
using System.Security;
using System.Security.Cryptography;
using System.Threading;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Retains one caller-pinned native-fixture release set and its strict PE
/// audit. The exact embedded application manifest is supplied explicitly until
/// packaging owns it as a project resource. This composite supplies neither
/// release provenance nor launch eligibility.
/// </summary>
internal sealed class AuditedNativeFixtureReleaseLease : IDisposable
{
    private readonly object gate = new();
    private readonly PinnedReleaseArtifactSetLease pinnedRelease;
    private readonly NativeFixturePeAudit peAudit;
    private bool disposed;

    private AuditedNativeFixtureReleaseLease(
        PinnedReleaseArtifactSetLease pinnedRelease,
        NativeFixturePeAudit peAudit)
    {
        this.pinnedRelease = pinnedRelease;
        this.peAudit = peAudit;
    }

    internal string ApplicationDirectory => pinnedRelease.ApplicationDirectory;

    internal string ExecutablePath => pinnedRelease.ExecutablePath;

    internal bool IsEligibleForTrustedLaunch => false;

    internal static AuditedNativeFixtureReleaseLease Open(
        string exactApplicationDirectory,
        ReadOnlySpan<byte> canonicalReleaseManifest,
        ReadOnlySpan<byte> expectedReleaseManifestPinSha256,
        ReadOnlySpan<byte> exactEmbeddedApplicationManifest,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        return Open(
            exactApplicationDirectory,
            canonicalReleaseManifest,
            expectedReleaseManifestPinSha256,
            exactEmbeddedApplicationManifest,
            deadline,
            cancellationToken,
            testHook: null);
    }

    /// <summary>
    /// Test-only overload. The hook must not mutate the borrowed exact-image
    /// snapshot. A test may retain its reference only to assert zeroing after a
    /// forced failure; it must never use the reference after successful return.
    /// </summary>
    internal static AuditedNativeFixtureReleaseLease Open(
        string exactApplicationDirectory,
        ReadOnlySpan<byte> canonicalReleaseManifest,
        ReadOnlySpan<byte> expectedReleaseManifestPinSha256,
        ReadOnlySpan<byte> exactEmbeddedApplicationManifest,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken,
        Action<NativeFixtureReleaseOpenStage, byte[]?>? testHook)
    {
        CheckOperation(deadline, cancellationToken);
        if (exactEmbeddedApplicationManifest.Length !=
            NativeFixturePeAudit.ExactManifestLength)
        {
            throw new ArgumentException(
                "The native fixture embedded manifest must have its exact bounded length.",
                nameof(exactEmbeddedApplicationManifest));
        }

        byte[] ownedEmbeddedManifest =
            exactEmbeddedApplicationManifest.ToArray();
        PinnedReleaseArtifactSetLease? pinned = null;
        NativeFixturePeAudit? audit = null;
        byte[]? exactImage = null;
        byte[]? expectedImageSha256 = null;
        byte[]? auditedImageSha256 = null;
        try
        {
            CheckOperation(deadline, cancellationToken);
            pinned = PinnedReleaseArtifactSetLease.Open(
                exactApplicationDirectory,
                canonicalReleaseManifest,
                expectedReleaseManifestPinSha256,
                deadline,
                cancellationToken);
            RequireNativeFixtureProfile(pinned);
            expectedImageSha256 = pinned.CopyExecutableSha256Digest();
            pinned.RevalidateExactSet(deadline, cancellationToken);
            CheckOperation(deadline, cancellationToken);

            exactImage = pinned.CopyExactExecutableBytes(
                NativeFixturePeAudit.ExactImageLength,
                deadline,
                cancellationToken);
            testHook?.Invoke(
                NativeFixtureReleaseOpenStage.AfterExactImageCopy,
                exactImage);
            CheckOperation(deadline, cancellationToken);
            audit = NativeFixturePeAudit.Open(
                exactImage,
                ownedEmbeddedManifest,
                expectedImageSha256);
            if (!audit.RequiresNoDynamicIndirectControlFlow ||
                audit.HasGuardCfInstrumentation ||
                audit.ProvesMachineCodeSemantics ||
                audit.IsEligibleForTrustedLaunch)
            {
                throw new SecurityException(
                    "The native fixture PE audit crossed its narrow policy boundary.");
            }

            auditedImageSha256 = audit.CopyImageSha256();
            if (!CryptographicOperations.FixedTimeEquals(
                    auditedImageSha256,
                    expectedImageSha256))
            {
                throw new SecurityException(
                    "The audited native fixture identity differs from the authenticated release manifest.");
            }

            testHook?.Invoke(
                NativeFixtureReleaseOpenStage.BeforeFinalRevalidation,
                null);
            pinned.RevalidateExactSet(deadline, cancellationToken);
            testHook?.Invoke(
                NativeFixtureReleaseOpenStage.AfterFinalRevalidation,
                null);
            CheckOperation(deadline, cancellationToken);
            AuditedNativeFixtureReleaseLease result = new(pinned, audit);
            pinned = null;
            audit = null;
            return result;
        }
        finally
        {
            try
            {
                audit?.Dispose();
            }
            finally
            {
                try
                {
                    pinned?.Dispose();
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(ownedEmbeddedManifest);
                    if (exactImage is not null)
                    {
                        CryptographicOperations.ZeroMemory(exactImage);
                    }

                    if (expectedImageSha256 is not null)
                    {
                        CryptographicOperations.ZeroMemory(
                            expectedImageSha256);
                    }

                    if (auditedImageSha256 is not null)
                    {
                        CryptographicOperations.ZeroMemory(
                            auditedImageSha256);
                    }
                }
            }
        }
    }

    internal byte[] CopyImageSha256()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return peAudit.CopyImageSha256();
        }
    }

    internal byte[] CopyReproducibleBuildId()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return peAudit.CopyReproducibleBuildId();
        }
    }

    internal TrustedArtifactLaunchNamespaceLease OpenLaunchNamespaceLease(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            if (!peAudit.RequiresNoDynamicIndirectControlFlow ||
                peAudit.HasGuardCfInstrumentation ||
                peAudit.ProvesMachineCodeSemantics ||
                peAudit.IsEligibleForTrustedLaunch ||
                IsEligibleForTrustedLaunch)
            {
                throw new SecurityException(
                    "The native fixture PE audit crossed its narrow launch-namespace boundary.");
            }

            pinnedRelease.RevalidateExactSet(deadline, cancellationToken);
            TrustedArtifactLaunchNamespaceLease launchNamespace =
                pinnedRelease.OpenExecutableLaunchNamespaceLease(
                    deadline,
                    cancellationToken);
            try
            {
                pinnedRelease.RevalidateExactSet(deadline, cancellationToken);
                CheckOperation(deadline, cancellationToken);
                TrustedArtifactLaunchNamespaceLease result = launchNamespace;
                launchNamespace = null!;
                return result;
            }
            finally
            {
                launchNamespace?.Dispose();
            }
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
            pinnedRelease.RevalidateExactSet(deadline, cancellationToken);
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
                peAudit.Dispose();
            }
            finally
            {
                pinnedRelease.Dispose();
            }
        }
    }

    private static void RequireNativeFixtureProfile(
        PinnedReleaseArtifactSetLease release)
    {
        if (release.ArtifactRole !=
                ReleaseArtifactRole.SyntheticNativeFixture ||
            release.DeploymentKind !=
                ReleaseDeploymentKind.NativeNoCrtSystem32Fixture ||
            release.TargetRuntimeIdentifier !=
                ReleaseTargetRuntimeIdentifier.WinX64 ||
            release.Count != 1 ||
            !string.Equals(
                release.ExecutableRelativeFileName,
                ReleaseManifestV1.NativeFixtureExecutableRelativeFileName,
                StringComparison.Ordinal) ||
            release.IsEligibleForTrustedLaunch)
        {
            throw new SecurityException(
                "The pinned release is not the closed synthetic native-fixture profile.");
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
                nameof(AuditedNativeFixtureReleaseLease));
        }
    }
}

/// <summary>
/// Test-only stages within native-release composite open ownership.
/// </summary>
internal enum NativeFixtureReleaseOpenStage
{
    AfterExactImageCopy = 1,
    BeforeFinalRevalidation = 2,
    AfterFinalRevalidation = 3,
}
