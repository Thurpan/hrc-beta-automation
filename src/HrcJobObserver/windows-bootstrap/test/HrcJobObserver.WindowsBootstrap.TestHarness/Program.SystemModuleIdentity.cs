using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using HrcJobObserver.WindowsBootstrap;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class Program
{
    private static Task TestNativeSystemModuleIdentityRoundTrip()
    {
        NativeSystemModuleIdentityLease lease =
            NativeSystemModuleIdentityLease.OpenKernel32(
                NewArtifactDeadline(),
                CancellationToken.None);
        try
        {
            Assert(string.Equals(
                    lease.Path,
                    Path.Combine(Environment.SystemDirectory, "kernel32.dll"),
                    StringComparison.OrdinalIgnoreCase),
                "the module lease must bind native System32 KERNEL32");
            Assert(lease.Length > 0, "KERNEL32 must have a positive length");
            Assert(!lease.IsEligibleForTrustedLaunch,
                "local module equality must not imply trusted launch");
            byte[] digest = lease.CopySha256Digest();
            byte[] identifier = lease.CopyFileIdentifier();
            try
            {
                AssertEqual(32, digest.Length, "system-module digest length");
                AssertEqual(16, identifier.Length, "system-module FILE_ID length");
                digest[0] ^= 0xff;
                identifier[0] ^= 0xff;
                Assert(!digest.AsSpan().SequenceEqual(lease.CopySha256Digest()),
                    "system-module digest copies must be independent");
                Assert(!identifier.AsSpan().SequenceEqual(lease.CopyFileIdentifier()),
                    "system-module identity copies must be independent");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
                CryptographicOperations.ZeroMemory(identifier);
            }

            lease.Revalidate(NewArtifactDeadline(), CancellationToken.None);
        }
        finally
        {
            lease.Dispose();
        }

        lease.Dispose();
        AssertThrows<ObjectDisposedException>(() => lease.CopySha256Digest());
        AssertThrows<ObjectDisposedException>(() => lease.Revalidate(
            NewArtifactDeadline(), CancellationToken.None));
        return Task.CompletedTask;
    }

    private static Task TestNativeSystemModuleIdentityEvidence()
    {
        using NativeSystemModuleIdentityLease lease =
            NativeSystemModuleIdentityLease.OpenKernel32(
                NewArtifactDeadline(),
                CancellationToken.None);
        NativeSystemModuleLoadEvidence ownedEvidence;
        using (SafeFileHandle current = File.OpenHandle(
                   lease.Path,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read))
        {
            NativeSystemModuleLoadEvidence? captured =
                lease.TryCaptureLoadedModuleEvidence(
                    current,
                    NewArtifactDeadline(),
                    CancellationToken.None);
            ownedEvidence = captured ?? throw new InvalidOperationException(
                "An exact KERNEL32 handle did not produce load evidence.");
            using (ownedEvidence)
            {
                Assert(!current.IsClosed && !current.IsInvalid,
                    "capturing evidence must not consume the borrowed handle");
                Span<byte> probe = stackalloc byte[1];
                AssertEqual(1, RandomAccess.Read(current, probe, 0),
                    "borrowed handle read after evidence capture");
                Assert(!ownedEvidence.IsEligibleForTrustedLaunch,
                    "file equality must not imply trusted launch");
                AssertEqual(lease.Length, ownedEvidence.Length, "evidence length");
                Assert(string.Equals(
                        lease.VolumeGuidPath,
                        ownedEvidence.VolumeGuidPath,
                        StringComparison.OrdinalIgnoreCase),
                    "evidence volume-GUID path");
                byte[] expectedDigest = lease.CopySha256Digest();
                byte[] actualDigest = ownedEvidence.CopySha256Digest();
                try
                {
                    Assert(expectedDigest.AsSpan().SequenceEqual(actualDigest),
                        "evidence digest must match the expected module");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(expectedDigest);
                    CryptographicOperations.ZeroMemory(actualDigest);
                }

                ownedEvidence.Revalidate(
                    NewArtifactDeadline(),
                    CancellationToken.None);
            }

            Assert(!current.IsClosed && !current.IsInvalid,
                "disposing evidence must not consume the borrowed handle");
            Span<byte> postDisposeProbe = stackalloc byte[1];
            AssertEqual(1, RandomAccess.Read(current, postDisposeProbe, 0),
                "borrowed handle read after evidence disposal");
        }

        AssertThrows<ObjectDisposedException>(() =>
            ownedEvidence.CopySha256Digest());
        return Task.CompletedTask;
    }

    private static Task TestNativeSystemModuleIdentityMismatchAndBounds()
    {
        using NativeSystemModuleIdentityLease lease =
            NativeSystemModuleIdentityLease.OpenKernel32(
                NewArtifactDeadline(),
                CancellationToken.None);
        string fixturePath = Path.Combine(
            FindWindowsBootstrapModuleRoot(),
            "build",
            "native",
            "HrcJobObserver.NativeFixture.exe");
        using (SafeFileHandle other = File.OpenHandle(
                   fixturePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read))
        {
            NativeSystemModuleLoadEvidence? mismatchedEvidence =
                lease.TryCaptureLoadedModuleEvidence(
                    other,
                    NewArtifactDeadline(),
                    CancellationToken.None);
            Assert(mismatchedEvidence is null,
                "a stable different file identity must classify as non-KERNEL32");
        }

        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        AssertThrows<OperationCanceledException>(() =>
            NativeSystemModuleIdentityLease.OpenKernel32(
                NewArtifactDeadline(),
                cancelled.Token));
        AssertThrows<OperationCanceledException>(() =>
            lease.Revalidate(NewArtifactDeadline(), cancelled.Token));

        ManualTimeProvider expiredClock = new(CanonicalTestUtcNow());
        MonotonicDeadline expired = MonotonicDeadline.Start(
            expiredClock,
            TestTimeout);
        expiredClock.Advance(TestTimeout);
        AssertThrows<TimeoutException>(() =>
            NativeSystemModuleIdentityLease.OpenKernel32(
                expired,
                CancellationToken.None));
        AssertThrows<TimeoutException>(() =>
            lease.Revalidate(expired, CancellationToken.None));

        using SafeFileHandle exact = File.OpenHandle(
            lease.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        AssertThrows<OperationCanceledException>(() =>
            lease.TryCaptureLoadedModuleEvidence(
                exact,
                NewArtifactDeadline(),
                cancelled.Token));
        Assert(!exact.IsClosed && !exact.IsInvalid,
            "cancelled capture must not consume the borrowed handle");
        AssertThrows<TimeoutException>(() =>
            lease.TryCaptureLoadedModuleEvidence(
                exact,
                expired,
                CancellationToken.None));
        Assert(!exact.IsClosed && !exact.IsInvalid,
            "expired capture must not consume the borrowed handle");

        NativeSystemModuleLoadEvidence evidence =
            lease.TryCaptureLoadedModuleEvidence(
                exact,
                NewArtifactDeadline(),
                CancellationToken.None) ?? throw new InvalidOperationException(
                    "An exact KERNEL32 handle did not produce load evidence.");
        using (evidence)
        {
            AssertThrows<OperationCanceledException>(() =>
                evidence.Revalidate(NewArtifactDeadline(), cancelled.Token));
            AssertThrows<TimeoutException>(() =>
                evidence.Revalidate(expired, CancellationToken.None));
            evidence.Revalidate(NewArtifactDeadline(), CancellationToken.None);
        }

        Span<byte> stillReadable = stackalloc byte[1];
        AssertEqual(1, RandomAccess.Read(exact, stillReadable, 0),
            "failed evidence operations must leave the borrowed handle usable");
        return Task.CompletedTask;
    }
}
