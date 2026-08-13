using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace HrcJobObserver.WindowsBootstrap;

internal enum NativeLaunchPolicyProfile : ushort
{
    SyntheticNativeFixture = 1,
}

/// <summary>
/// Pure authenticated binding between one synthetic native-fixture release
/// manifest and one native System32 module policy. Authentication is relative
/// only to caller-supplied pins. This package supplies no issuer, signature,
/// freshness, rollback protection, protected selection, or launch authority.
/// </summary>
internal sealed class NativeLaunchPolicyPackageV1 : IDisposable
{
    internal const int MinimumEncodedLength = 440;
    internal const int MaximumEncodedLength = 38_667;

    private const int Sha256Length = 32;
    private const int HashChunkLength = 4_096;
    private const int FixedHeaderLength = 92;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HRCNLP01");
    private static readonly byte[] PinDomain = Encoding.ASCII.GetBytes(
        "HRC-BETA-OBSERVER-NATIVE-LAUNCH-POLICY-PACKAGE-PIN-V1\0");

    private readonly object gate = new();
    private readonly byte[] canonicalPackage;
    private readonly byte[] packagePinSha256;
    private readonly ulong generation;
    private readonly AuthenticatedReleaseManifestV1 releaseManifest;
    private readonly TrustedNativeSystemModulePolicyV1 systemModulePolicy;
    private bool disposed;

    private NativeLaunchPolicyPackageV1(
        byte[] canonicalPackage,
        byte[] packagePinSha256,
        ulong generation,
        AuthenticatedReleaseManifestV1 releaseManifest,
        TrustedNativeSystemModulePolicyV1 systemModulePolicy)
    {
        this.canonicalPackage = canonicalPackage;
        this.packagePinSha256 = packagePinSha256;
        this.generation = generation;
        this.releaseManifest = releaseManifest;
        this.systemModulePolicy = systemModulePolicy;
    }

    internal NativeLaunchPolicyProfile Profile
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return NativeLaunchPolicyProfile.SyntheticNativeFixture;
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
                return generation;
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
                return releaseManifest.ArtifactRole;
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
                return releaseManifest.DeploymentKind;
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
                return releaseManifest.TargetRuntimeIdentifier;
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
                return systemModulePolicy.ConsumerProfile;
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

    internal static NativeLaunchPolicyPackageV1 Authenticate(
        ReadOnlySpan<byte> canonicalPackage,
        ReadOnlySpan<byte> expectedPackagePinSha256,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        if (canonicalPackage.Length is < MinimumEncodedLength or >
            MaximumEncodedLength)
        {
            throw new ArgumentException(
                "The native launch-policy package byte length is invalid.",
                nameof(canonicalPackage));
        }

        if (expectedPackagePinSha256.Length != Sha256Length)
        {
            throw new ArgumentException(
                "The expected native launch-policy package pin must contain exactly 32 bytes.",
                nameof(expectedPackagePinSha256));
        }

        byte[]? ownedPackage = null;
        byte[]? ownedExpectedPin = null;
        byte[]? actualPin = null;
        AuthenticatedReleaseManifestV1? release = null;
        TrustedNativeSystemModulePolicyV1? modulePolicy = null;
        bool transferred = false;
        try
        {
            ownedPackage = canonicalPackage.ToArray();
            ownedExpectedPin = expectedPackagePinSha256.ToArray();
            CheckOperation(deadline, cancellationToken);
            actualPin = ComputePinSha256(
                ownedPackage,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(
                    actualPin,
                    ownedExpectedPin))
            {
                throw new SecurityException(
                    "The native launch-policy package does not match the independently supplied pin.");
            }

            CheckOperation(deadline, cancellationToken);
            ParsedPackage parsed = ParseStructuralCanonical(
                ownedPackage,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
            release = AuthenticatedReleaseManifestV1.Authenticate(
                parsed.ReleaseManifest,
                parsed.ReleaseManifestPinSha256,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
            modulePolicy = TrustedNativeSystemModulePolicyV1.Authenticate(
                parsed.NativeSystemModulePolicy,
                parsed.NativeSystemModulePolicyPinSha256,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
            RequireClosedNativeFixtureProfiles(release, modulePolicy);
            CheckOperation(deadline, cancellationToken);
            NativeLaunchPolicyPackageV1 result = new(
                ownedPackage,
                actualPin,
                parsed.Generation,
                release,
                modulePolicy);
            ownedPackage = null;
            actualPin = null;
            release = null;
            modulePolicy = null;
            transferred = true;
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
            if (ownedExpectedPin is not null)
            {
                CryptographicOperations.ZeroMemory(ownedExpectedPin);
            }

            if (!transferred)
            {
                modulePolicy?.Dispose();
                release?.Dispose();
                if (ownedPackage is not null)
                {
                    CryptographicOperations.ZeroMemory(ownedPackage);
                }

                if (actualPin is not null)
                {
                    CryptographicOperations.ZeroMemory(actualPin);
                }
            }
        }
    }

    internal byte[] CopyCanonicalPackage()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return (byte[])canonicalPackage.Clone();
        }
    }

    internal byte[] CopyPackagePinSha256()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return (byte[])packagePinSha256.Clone();
        }
    }

    internal byte[] CopyCanonicalReleaseManifest()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return releaseManifest.CopyCanonicalManifest();
        }
    }

    internal byte[] CopyReleaseManifestPinSha256()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return releaseManifest.CopyManifestPinSha256();
        }
    }

    internal byte[] CopyCanonicalNativeSystemModulePolicy()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return SliceOwnedPackage(
                GetNativeSystemModulePolicyOffset(canonicalPackage),
                TrustedNativeSystemModulePolicyV1.EncodedLength);
        }
    }

    internal byte[] CopyNativeSystemModulePolicyPinSha256()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return systemModulePolicy.CopyPolicyPinSha256();
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
                systemModulePolicy.Dispose();
            }
            finally
            {
                try
                {
                    releaseManifest.Dispose();
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(canonicalPackage);
                    CryptographicOperations.ZeroMemory(packagePinSha256);
                }
            }
        }
    }

    private static ParsedPackage ParseStructuralCanonical(
        ReadOnlySpan<byte> encoded,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        BootstrapBinaryReader reader = new(encoded);
        if (!reader.ReadBytes(Magic.Length, "launch-policy package magic")
                .SequenceEqual(Magic))
        {
            throw new FormatException(
                "The native launch-policy package magic or version is invalid.");
        }

        if (reader.ReadUInt16("launch-policy profile") !=
            (ushort)NativeLaunchPolicyProfile.SyntheticNativeFixture)
        {
            throw new FormatException(
                "The native launch-policy package profile is not admitted.");
        }

        reader.RequireZero(sizeof(ushort), "launch-policy reserved field");
        ulong generation = reader.ReadUInt64("launch-policy generation");
        if (generation == 0)
        {
            throw new FormatException(
                "The native launch-policy package generation must be nonzero.");
        }

        uint releaseLength = reader.ReadUInt32("release manifest length");
        if (releaseLength is < ReleaseManifestV1.MinimumEncodedLength or >
            ReleaseManifestV1.MaximumEncodedLength)
        {
            throw new FormatException(
                "The nested release manifest length is invalid.");
        }

        uint moduleLength = reader.ReadUInt32(
            "native system-module policy length");
        if (moduleLength != TrustedNativeSystemModulePolicyV1.EncodedLength)
        {
            throw new FormatException(
                "The nested native system-module policy length is invalid.");
        }

        ReadOnlySpan<byte> releasePin = reader.ReadBytes(
            Sha256Length,
            "release manifest pin");
        ReadOnlySpan<byte> modulePin = reader.ReadBytes(
            Sha256Length,
            "native system-module policy pin");
        ReadOnlySpan<byte> releaseManifest = reader.ReadBytes(
            checked((int)releaseLength),
            "release manifest");
        ReadOnlySpan<byte> modulePolicy = reader.ReadBytes(
            checked((int)moduleLength),
            "native system-module policy");
        reader.RequireEnd();
        CheckOperation(deadline, cancellationToken);
        return new ParsedPackage(
            generation,
            releasePin,
            modulePin,
            releaseManifest,
            modulePolicy);
    }

    private static void RequireClosedNativeFixtureProfiles(
        AuthenticatedReleaseManifestV1 release,
        TrustedNativeSystemModulePolicyV1 modulePolicy)
    {
        if (release.ArtifactRole !=
                ReleaseArtifactRole.SyntheticNativeFixture ||
            release.DeploymentKind !=
                ReleaseDeploymentKind.NativeNoCrtSystem32Fixture ||
            release.TargetRuntimeIdentifier !=
                ReleaseTargetRuntimeIdentifier.WinX64 ||
            modulePolicy.ConsumerProfile !=
                TrustedNativeSystemModuleConsumerProfile
                    .SyntheticNativeFixture ||
            release.IsEligibleForTrustedLaunch ||
            modulePolicy.IsEligibleForTrustedLaunch)
        {
            throw new SecurityException(
                "The nested policies do not form the closed synthetic native-fixture profile.");
        }
    }

    private static int GetNativeSystemModulePolicyOffset(
        ReadOnlySpan<byte> canonicalPackage)
    {
        BootstrapBinaryReader reader = new(canonicalPackage);
        _ = reader.ReadBytes(Magic.Length, "launch-policy package magic");
        _ = reader.ReadUInt16("launch-policy profile");
        _ = reader.ReadUInt16("launch-policy reserved field");
        _ = reader.ReadUInt64("launch-policy generation");
        uint releaseLength = reader.ReadUInt32("release manifest length");
        _ = reader.ReadUInt32("native system-module policy length");
        return checked(FixedHeaderLength + (int)releaseLength);
    }

    private byte[] SliceOwnedPackage(int offset, int length)
    {
        byte[] result = new byte[length];
        canonicalPackage.AsSpan(offset, length).CopyTo(result);
        return result;
    }

    private static byte[] ComputePinSha256(
        ReadOnlySpan<byte> canonicalPackage,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        hash.AppendData(PinDomain);
        for (int offset = 0; offset < canonicalPackage.Length;
            offset += HashChunkLength)
        {
            CheckOperation(deadline, cancellationToken);
            int length = Math.Min(
                HashChunkLength,
                canonicalPackage.Length - offset);
            hash.AppendData(canonicalPackage.Slice(offset, length));
        }

        CheckOperation(deadline, cancellationToken);
        byte[] result = hash.GetHashAndReset();
        if (result.Length != Sha256Length)
        {
            CryptographicOperations.ZeroMemory(result);
            throw new CryptographicException(
                "The native launch-policy package pin length was invalid.");
        }

        return result;
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

    private readonly ref struct ParsedPackage
    {
        internal ParsedPackage(
            ulong generation,
            ReadOnlySpan<byte> releaseManifestPinSha256,
            ReadOnlySpan<byte> nativeSystemModulePolicyPinSha256,
            ReadOnlySpan<byte> releaseManifest,
            ReadOnlySpan<byte> nativeSystemModulePolicy)
        {
            Generation = generation;
            ReleaseManifestPinSha256 = releaseManifestPinSha256;
            NativeSystemModulePolicyPinSha256 =
                nativeSystemModulePolicyPinSha256;
            ReleaseManifest = releaseManifest;
            NativeSystemModulePolicy = nativeSystemModulePolicy;
        }

        internal ulong Generation { get; }

        internal ReadOnlySpan<byte> ReleaseManifestPinSha256 { get; }

        internal ReadOnlySpan<byte> NativeSystemModulePolicyPinSha256 { get; }

        internal ReadOnlySpan<byte> ReleaseManifest { get; }

        internal ReadOnlySpan<byte> NativeSystemModulePolicy { get; }
    }
}
