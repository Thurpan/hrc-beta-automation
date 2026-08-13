using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Owns one canonical release manifest authenticated relative to an
/// independently supplied SHA-256 pin. It performs no filesystem access and
/// supplies no signature, issuer, freshness, rollback, or launch authority.
/// </summary>
internal sealed class AuthenticatedReleaseManifestV1 : IDisposable
{
    private const int Sha256Length = 32;
    private const int HashChunkLength = 4_096;
    private static readonly byte[] PinDomain = Encoding.ASCII.GetBytes(
        "HRC-BETA-OBSERVER-RELEASE-MANIFEST-PIN-V1\0");

    private readonly object gate = new();
    private readonly byte[] canonicalManifest;
    private readonly byte[] manifestPinSha256;
    private readonly ReleaseManifestV1 manifest;
    private bool disposed;

    private AuthenticatedReleaseManifestV1(
        byte[] canonicalManifest,
        byte[] manifestPinSha256,
        ReleaseManifestV1 manifest)
    {
        this.canonicalManifest = canonicalManifest;
        this.manifestPinSha256 = manifestPinSha256;
        this.manifest = manifest;
    }

    internal ReleaseArtifactRole ArtifactRole
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return manifest.ArtifactRole;
            }
        }
    }

    internal ReleaseDeploymentKind DeploymentKind
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return manifest.DeploymentKind;
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
                return manifest.TargetRuntimeIdentifier;
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

    internal string ExecutableRelativeFileName
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return manifest.ExecutableRelativeFileName;
            }
        }
    }

    internal int ArtifactCount
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return manifest.ArtifactCount;
            }
        }
    }

    internal static AuthenticatedReleaseManifestV1 Authenticate(
        ReadOnlySpan<byte> canonicalManifest,
        ReadOnlySpan<byte> expectedManifestPinSha256,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        if (canonicalManifest.Length is < 1 or >
            ReleaseManifestV1.MaximumEncodedLength)
        {
            throw new ArgumentException(
                "The release manifest byte length is invalid.",
                nameof(canonicalManifest));
        }

        if (expectedManifestPinSha256.Length != Sha256Length)
        {
            throw new ArgumentException(
                "The expected release-manifest pin must contain exactly 32 bytes.",
                nameof(expectedManifestPinSha256));
        }

        byte[]? ownedManifest = null;
        byte[]? ownedExpectedPin = null;
        byte[]? actualPin = null;
        ReleaseManifestV1? parsedManifest = null;
        bool transferred = false;
        try
        {
            ownedManifest = canonicalManifest.ToArray();
            ownedExpectedPin = expectedManifestPinSha256.ToArray();
            CheckOperation(deadline, cancellationToken);
            actualPin = ComputePinSha256(
                ownedManifest,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(
                    actualPin,
                    ownedExpectedPin))
            {
                throw new SecurityException(
                    "The release manifest does not match the independently supplied pin.");
            }

            CheckOperation(deadline, cancellationToken);
            parsedManifest = ReleaseManifestV1.ParseStructuralCanonical(
                ownedManifest,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
            AuthenticatedReleaseManifestV1 result = new(
                ownedManifest,
                actualPin,
                parsedManifest);
            ownedManifest = null;
            actualPin = null;
            parsedManifest = null;
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
                if (ownedManifest is not null)
                {
                    CryptographicOperations.ZeroMemory(ownedManifest);
                }

                if (actualPin is not null)
                {
                    CryptographicOperations.ZeroMemory(actualPin);
                }

                parsedManifest?.Dispose();
            }
        }
    }

    internal byte[] CopyCanonicalManifest()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return (byte[])canonicalManifest.Clone();
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

    internal TrustedArtifactExpectation[] CopyArtifacts()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return manifest.CopyArtifacts();
        }
    }

    internal byte[] CopyArtifactSetManifestSha256()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return manifest.CopyArtifactSetManifestSha256();
        }
    }

    internal byte[] CopyExecutableSha256Digest()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return manifest.CopyExecutableSha256Digest();
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
                manifest.Dispose();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(canonicalManifest);
                CryptographicOperations.ZeroMemory(manifestPinSha256);
            }
        }
    }

    private static byte[] ComputePinSha256(
        ReadOnlySpan<byte> canonicalManifest,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        hash.AppendData(PinDomain);
        for (int offset = 0; offset < canonicalManifest.Length;
            offset += HashChunkLength)
        {
            CheckOperation(deadline, cancellationToken);
            int length = Math.Min(
                HashChunkLength,
                canonicalManifest.Length - offset);
            hash.AppendData(canonicalManifest.Slice(offset, length));
        }

        CheckOperation(deadline, cancellationToken);
        byte[] result = hash.GetHashAndReset();
        if (result.Length != Sha256Length)
        {
            CryptographicOperations.ZeroMemory(result);
            throw new CryptographicException(
                "The release-manifest pin digest length was invalid.");
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
}
