using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class Program
{
    private const string NativeFixtureFileName =
        "HrcJobObserver.NativeFixture.exe";

    private static Task TestNativeReleaseManifestProfiles()
    {
        byte[] content = { 0x4d, 0x5a };
        byte[] artifactSetManifest = Convert.FromHexString(
            "aabbccddeeff00112233445566778899" +
            "aabbccddeeff00112233445566778899");
        byte[]? legacy = null;
        byte[]? native = null;
        try
        {
            ReleaseArtifactContent[] legacyArtifacts =
            {
                new("role.exe", content),
            };
            legacy = EncodeReleaseManifest(
                "role.exe",
                legacyArtifacts,
                artifactSetManifest);
            using ReleaseManifestV1 legacyParsed =
                ReleaseManifestV1.ParseStructuralCanonical(
                    legacy,
                    NewArtifactDeadline(),
                    CancellationToken.None);
            AssertEqual(
                ReleaseArtifactRole.SyntheticTestHarness,
                legacyParsed.ArtifactRole,
                "legacy release role");
            AssertEqual(
                ReleaseDeploymentKind.FrameworkDependentSnapshot,
                legacyParsed.DeploymentKind,
                "legacy release deployment");

            ReleaseArtifactContent[] nativeArtifacts =
            {
                new(NativeFixtureFileName, content),
            };
            native = EncodeReleaseManifest(
                NativeFixtureFileName,
                nativeArtifacts,
                artifactSetManifest,
                artifactRole: ReleaseArtifactRole.SyntheticNativeFixture,
                deploymentKind:
                    ReleaseDeploymentKind.NativeNoCrtSystem32Fixture);
            using ReleaseManifestV1 nativeParsed =
                ReleaseManifestV1.ParseStructuralCanonical(
                    native,
                    NewArtifactDeadline(),
                    CancellationToken.None);
            AssertEqual(
                ReleaseArtifactRole.SyntheticNativeFixture,
                nativeParsed.ArtifactRole,
                "native release role");
            AssertEqual(
                ReleaseDeploymentKind.NativeNoCrtSystem32Fixture,
                nativeParsed.DeploymentKind,
                "native release deployment");
            AssertEqual(1, nativeParsed.ArtifactCount,
                "native release artifact count");
            AssertEqual(NativeFixtureFileName,
                nativeParsed.ExecutableRelativeFileName,
                "native release executable filename");

            AssertPinnedManifestFormatFailure(
                EncodeReleaseManifest(
                    NativeFixtureFileName,
                    nativeArtifacts,
                    artifactSetManifest,
                    artifactRole:
                        ReleaseArtifactRole.SyntheticNativeFixture,
                    deploymentKind:
                        ReleaseDeploymentKind.FrameworkDependentSnapshot));
            AssertPinnedManifestFormatFailure(
                EncodeReleaseManifest(
                    "role.exe",
                    legacyArtifacts,
                    artifactSetManifest,
                    artifactRole:
                        ReleaseArtifactRole.SyntheticTestHarness,
                    deploymentKind:
                        ReleaseDeploymentKind.NativeNoCrtSystem32Fixture));
            AssertPinnedManifestFormatFailure(
                EncodeReleaseManifest(
                    NativeFixtureFileName,
                    new[]
                    {
                        new ReleaseArtifactContent(
                            "HrcJobObserver.Nativefixture.exe",
                            content),
                    },
                    artifactSetManifest,
                    artifactRole:
                        ReleaseArtifactRole.SyntheticNativeFixture,
                    deploymentKind:
                        ReleaseDeploymentKind.NativeNoCrtSystem32Fixture));
            AssertPinnedManifestFormatFailure(
                EncodeReleaseManifest(
                    NativeFixtureFileName,
                    new[]
                    {
                        new ReleaseArtifactContent(
                            NativeFixtureFileName,
                            content),
                        new ReleaseArtifactContent("sibling.dll", content),
                    },
                    artifactSetManifest,
                    artifactRole:
                        ReleaseArtifactRole.SyntheticNativeFixture,
                    deploymentKind:
                        ReleaseDeploymentKind.NativeNoCrtSystem32Fixture));
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(content);
            CryptographicOperations.ZeroMemory(artifactSetManifest);
            WipeNativeTestBytes(legacy);
            WipeNativeTestBytes(native);
        }
    }

    private static Task TestNativeReleaseManifestGoldenIdentity()
    {
        byte[] legacyWire = Convert.FromHexString(
            "48524352454c303101010001000000000008726f6c652e657865" +
            "000000010008726f6c652e6578650000000000000002" +
            "112233445566778899aabbccddeeff00112233445566778899aabbccddeeff00" +
            "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899");
        byte[] expectedLegacyPin = Convert.FromHexString(
            "b6d5aaceed16f2860b237ffd94dfe1a44d46e5c19932a5365219930159f789cc");
        byte[] nativeWire = Convert.FromHexString(
            "48524352454c303102020001000000000020" +
            "4872634a6f624f627365727665722e4e6174697665466978747572652e657865" +
            "000000010020" +
            "4872634a6f624f627365727665722e4e6174697665466978747572652e657865" +
            "0000000000001000" +
            "112233445566778899aabbccddeeff00112233445566778899aabbccddeeff00" +
            "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899");
        byte[] expectedNativePin = Convert.FromHexString(
            "a6cfb28209acbeaacc702c14ef29513383b028dd102c63486bb9ca95f13f7b45");
        byte[]? actualLegacyPin = null;
        byte[]? actualNativePin = null;
        try
        {
            actualLegacyPin = ComputeReleaseManifestPin(legacyWire);
            actualNativePin = ComputeReleaseManifestPin(nativeWire);
            Assert(actualLegacyPin.AsSpan().SequenceEqual(expectedLegacyPin),
                "the legacy V1 release wire identity changed");
            Assert(actualNativePin.AsSpan().SequenceEqual(expectedNativePin),
                "the native V1 release wire identity changed");
            using ReleaseManifestV1 parsed =
                ReleaseManifestV1.ParseStructuralCanonical(
                    nativeWire,
                    NewArtifactDeadline(),
                    CancellationToken.None);
            AssertEqual(
                ReleaseArtifactRole.SyntheticNativeFixture,
                parsed.ArtifactRole,
                "golden native release role");
            AssertEqual(NativeFixtureFileName,
                parsed.ExecutableRelativeFileName,
                "golden native executable filename");
            AssertEqual(1, parsed.ArtifactCount,
                "golden native artifact count");
            Assert(!parsed.IsEligibleForTrustedLaunch,
                "golden native structural parsing must not establish launch eligibility");
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(legacyWire);
            CryptographicOperations.ZeroMemory(expectedLegacyPin);
            CryptographicOperations.ZeroMemory(nativeWire);
            CryptographicOperations.ZeroMemory(expectedNativePin);
            WipeNativeTestBytes(actualLegacyPin);
            WipeNativeTestBytes(actualNativePin);
        }
    }

    private static Task TestRetainedArtifactExactByteCopy()
    {
        using FilePublicationTestDirectory directory = new();
        using NativeFixtureInputs inputs = ReadNativeFixtureInputs();
        string path = Path.Combine(directory.Path, NativeFixtureFileName);
        string replacementPath = Path.Combine(
            directory.Path,
            "native-replacement.exe");
        byte[] digest = SHA256.HashData(inputs.Image);
        TrustedArtifactLease? lease = null;
        byte[]? first = null;
        byte[]? second = null;
        try
        {
            File.WriteAllBytes(path, inputs.Image);
            File.WriteAllBytes(replacementPath, inputs.Image);
            lease = TrustedArtifactIdentity.Open(
                path,
                inputs.Image.Length,
                digest,
                NewArtifactDeadline(),
                CancellationToken.None);
            first = lease.CopyExactBytes(
                NativeFixturePeAudit.ExactImageLength,
                NewArtifactDeadline(),
                CancellationToken.None);
            Assert(first.AsSpan().SequenceEqual(inputs.Image),
                "the retained exact-byte copy changed fixture content");
            first[0] ^= 0xff;
            second = lease.CopyExactBytes(
                NativeFixturePeAudit.ExactImageLength,
                NewArtifactDeadline(),
                CancellationToken.None);
            Assert(second.AsSpan().SequenceEqual(inputs.Image),
                "retained exact-byte copies must be independently owned");

            AssertThrows<SecurityException>(() => lease.CopyExactBytes(
                NativeFixturePeAudit.ExactImageLength - 1,
                NewArtifactDeadline(),
                CancellationToken.None));
            AssertThrows<ArgumentOutOfRangeException>(() =>
                lease.CopyExactBytes(
                    -1,
                    NewArtifactDeadline(),
                    CancellationToken.None));
            using CancellationTokenSource cancelled = new();
            cancelled.Cancel();
            AssertThrows<OperationCanceledException>(() =>
                lease.CopyExactBytes(
                    NativeFixturePeAudit.ExactImageLength,
                    NewArtifactDeadline(),
                    cancelled.Token));
            ManualTimeProvider clock = new(CanonicalTestUtcNow());
            MonotonicDeadline expired = MonotonicDeadline.Start(
                clock,
                TestTimeout);
            clock.Advance(TestTimeout);
            AssertThrows<TimeoutException>(() => lease.CopyExactBytes(
                NativeFixturePeAudit.ExactImageLength,
                expired,
                CancellationToken.None));
            AssertThrowsAny(
                () => File.WriteAllBytes(path, inputs.Image),
                typeof(IOException),
                typeof(UnauthorizedAccessException));
            AssertThrowsAny(
                () => File.Delete(path),
                typeof(IOException),
                typeof(UnauthorizedAccessException));
            AssertThrowsAny(
                () => File.Move(replacementPath, path, overwrite: true),
                typeof(IOException),
                typeof(UnauthorizedAccessException));
            Assert(File.Exists(path) && File.Exists(replacementPath),
                "a retained exact-byte lease must preserve both replacement paths");

            lease.Dispose();
            lease = null;
            File.Move(replacementPath, path, overwrite: true);
            Assert(!File.Exists(replacementPath) &&
                    File.ReadAllBytes(path).AsSpan().SequenceEqual(inputs.Image),
                "replacement must become possible after exact-byte lease disposal");
            return Task.CompletedTask;
        }
        finally
        {
            lease?.Dispose();
            CryptographicOperations.ZeroMemory(digest);
            WipeNativeTestBytes(first);
            WipeNativeTestBytes(second);
        }
    }

    private static Task TestRetainedArtifactCopyLateFailureCleanup()
    {
        using FilePublicationTestDirectory directory = new();
        using NativeFixtureInputs inputs = ReadNativeFixtureInputs();
        string path = Path.Combine(directory.Path, NativeFixtureFileName);
        byte[] digest = SHA256.HashData(inputs.Image);
        byte[]? allocatedCancelledBorrow = null;
        byte[]? allocatedFaultBorrow = null;
        byte[]? cancelledBorrow = null;
        byte[]? expiredBorrow = null;
        byte[]? mutatedBorrow = null;
        byte[]? disposedBorrow = null;
        try
        {
            File.WriteAllBytes(path, inputs.Image);
            using TrustedArtifactLease lease = TrustedArtifactIdentity.Open(
                path,
                inputs.Image.Length,
                digest,
                NewArtifactDeadline(),
                CancellationToken.None);

            using CancellationTokenSource allocationCancelled = new();
            AssertThrows<OperationCanceledException>(() =>
                lease.CopyExactBytes(
                    NativeFixturePeAudit.ExactImageLength,
                    NewArtifactDeadline(),
                    allocationCancelled.Token,
                    (stage, borrowed) =>
                    {
                        if (stage ==
                            TrustedArtifactCopyStage.SnapshotAllocated)
                        {
                            allocatedCancelledBorrow = borrowed;
                            allocationCancelled.Cancel();
                        }
                    }));
            Assert(allocatedCancelledBorrow is not null &&
                    AllZero(allocatedCancelledBorrow),
                "an allocation-stage cancelled snapshot must be wiped");

            AssertThrows<TestNativeReleaseHookException>(() =>
                lease.CopyExactBytes(
                    NativeFixturePeAudit.ExactImageLength,
                    NewArtifactDeadline(),
                    CancellationToken.None,
                    (stage, borrowed) =>
                    {
                        if (stage ==
                            TrustedArtifactCopyStage.SnapshotAllocated)
                        {
                            allocatedFaultBorrow = borrowed;
                            throw new TestNativeReleaseHookException();
                        }
                    }));
            Assert(allocatedFaultBorrow is not null &&
                    AllZero(allocatedFaultBorrow),
                "an allocation-stage hook-fault snapshot must be wiped");

            using CancellationTokenSource cancelled = new();
            AssertThrows<OperationCanceledException>(() =>
                lease.CopyExactBytes(
                    NativeFixturePeAudit.ExactImageLength,
                    NewArtifactDeadline(),
                    cancelled.Token,
                    (stage, borrowed) =>
                    {
                        if (stage == TrustedArtifactCopyStage.SnapshotRead)
                        {
                            cancelledBorrow = borrowed;
                            cancelled.Cancel();
                        }
                    }));
            Assert(cancelledBorrow is not null && AllZero(cancelledBorrow),
                "a cancelled retained-byte snapshot must be wiped");

            ManualTimeProvider clock = new(CanonicalTestUtcNow());
            MonotonicDeadline deadline = MonotonicDeadline.Start(
                clock,
                TestTimeout);
            AssertThrows<TimeoutException>(() => lease.CopyExactBytes(
                NativeFixturePeAudit.ExactImageLength,
                deadline,
                CancellationToken.None,
                (stage, borrowed) =>
                {
                    if (stage == TrustedArtifactCopyStage.BeforeReturn)
                    {
                        expiredBorrow = borrowed;
                        clock.Advance(TestTimeout);
                    }
                }));
            Assert(expiredBorrow is not null && AllZero(expiredBorrow),
                "a late retained-byte snapshot must be wiped");

            AssertThrows<SecurityException>(() => lease.CopyExactBytes(
                NativeFixturePeAudit.ExactImageLength,
                NewArtifactDeadline(),
                CancellationToken.None,
                (stage, borrowed) =>
                {
                    if (stage == TrustedArtifactCopyStage.BeforeReturn)
                    {
                        mutatedBorrow = borrowed;
                        borrowed[0] ^= 0xff;
                    }
                }));
            Assert(mutatedBorrow is not null && AllZero(mutatedBorrow),
                "a mutated pre-transfer snapshot must be rejected and wiped");

            byte[] recovered = lease.CopyExactBytes(
                NativeFixturePeAudit.ExactImageLength,
                NewArtifactDeadline(),
                CancellationToken.None);
            try
            {
                Assert(recovered.AsSpan().SequenceEqual(inputs.Image),
                    "a failed retained copy must leave its lease usable");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(recovered);
            }

            AssertThrows<ObjectDisposedException>(() => lease.CopyExactBytes(
                NativeFixturePeAudit.ExactImageLength,
                NewArtifactDeadline(),
                CancellationToken.None,
                (stage, borrowed) =>
                {
                    if (stage == TrustedArtifactCopyStage.BeforeReturn)
                    {
                        disposedBorrow = borrowed;
                        lease.Dispose();
                    }
                }));
            Assert(disposedBorrow is not null && AllZero(disposedBorrow),
                "a reentrant-dispose snapshot must be rejected and wiped");
            AssertThrows<ObjectDisposedException>(() => lease.CopyExactBytes(
                NativeFixturePeAudit.ExactImageLength,
                NewArtifactDeadline(),
                CancellationToken.None));
            File.WriteAllBytes(path, inputs.Image);
            Assert(File.ReadAllBytes(path).AsSpan().SequenceEqual(inputs.Image),
                "reentrant disposal must release the retained artifact file");

            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
            allocatedCancelledBorrow = null;
            allocatedFaultBorrow = null;
            cancelledBorrow = null;
            expiredBorrow = null;
            mutatedBorrow = null;
            disposedBorrow = null;
        }
    }

    private static Task TestAuditedNativeReleaseAuthenticationAndRoundTrip()
    {
        byte[] malformed = new byte[ReleaseManifestV1.MaximumEncodedLength];
        byte[] wrongPin = new byte[32];
        byte[] correctPin = ComputeReleaseManifestPin(malformed);
        using NativeFixtureInputs authenticationInputs = ReadNativeFixtureInputs();
        try
        {
            wrongPin[0] = 1;
            AssertThrows<SecurityException>(() =>
            {
                using AuditedNativeFixtureReleaseLease ignored =
                    AuditedNativeFixtureReleaseLease.Open(
                        @"C:\manifest-path-is-not-consulted",
                        malformed,
                        wrongPin,
                        authenticationInputs.Manifest,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            AssertThrows<FormatException>(() =>
            {
                using AuditedNativeFixtureReleaseLease ignored =
                    AuditedNativeFixtureReleaseLease.Open(
                        @"C:\manifest-path-is-not-consulted",
                        malformed,
                        correctPin,
                        authenticationInputs.Manifest,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(malformed);
            CryptographicOperations.ZeroMemory(wrongPin);
            CryptographicOperations.ZeroMemory(correctPin);
        }

        using NativeReleaseFixture fixture = NativeReleaseFixture.Create();
        byte[] releaseManifest = (byte[])fixture.ReleaseManifest.Clone();
        byte[] releasePin = (byte[])fixture.ReleasePin.Clone();
        byte[] embeddedManifest = (byte[])fixture.EmbeddedManifest.Clone();
        AuditedNativeFixtureReleaseLease? lease = null;
        byte[]? firstDigest = null;
        byte[]? secondDigest = null;
        byte[]? reproId = null;
        try
        {
            lease = AuditedNativeFixtureReleaseLease.Open(
                fixture.Directory.Path,
                releaseManifest,
                releasePin,
                embeddedManifest,
                NewArtifactDeadline(),
                CancellationToken.None);
            AssertEqual(fixture.Directory.Path, lease.ApplicationDirectory,
                "audited native release directory");
            AssertEqual(fixture.ExecutablePath, lease.ExecutablePath,
                "audited native release executable path");
            Assert(!lease.IsEligibleForTrustedLaunch,
                "the audited native fixture must remain ineligible for trusted launch");
            firstDigest = lease.CopyImageSha256();
            reproId = lease.CopyReproducibleBuildId();
            Assert(firstDigest.AsSpan().SequenceEqual(
                    ExactNativeFixtureSha256),
                "the audited native release image identity changed");
            Assert(reproId.AsSpan().SequenceEqual(
                    ExactNativeFixtureReproId),
                "the audited native release REPRO identity changed");

            CryptographicOperations.ZeroMemory(releaseManifest);
            CryptographicOperations.ZeroMemory(releasePin);
            CryptographicOperations.ZeroMemory(embeddedManifest);
            CryptographicOperations.ZeroMemory(firstDigest);
            secondDigest = lease.CopyImageSha256();
            Assert(secondDigest.AsSpan().SequenceEqual(
                    ExactNativeFixtureSha256),
                "the audited native release retained caller-owned input backing");
            lease.RevalidateExactSet(
                NewArtifactDeadline(),
                CancellationToken.None);

            string unexpectedPath = Path.Combine(
                fixture.Directory.Path,
                "unexpected.dll");
            File.WriteAllBytes(unexpectedPath, new byte[] { 0x01 });
            AssertThrows<SecurityException>(() => lease.RevalidateExactSet(
                NewArtifactDeadline(),
                CancellationToken.None));
            File.Delete(unexpectedPath);
            lease.RevalidateExactSet(
                NewArtifactDeadline(),
                CancellationToken.None);
            AssertThrowsAny(
                () => File.WriteAllBytes(
                    fixture.ExecutablePath,
                    fixture.Image),
                typeof(IOException),
                typeof(UnauthorizedAccessException));

            AuditedNativeFixtureReleaseLease disposedLease = lease;
            disposedLease.Dispose();
            disposedLease.Dispose();
            lease = null;
            AssertThrows<ObjectDisposedException>(() =>
                disposedLease.CopyImageSha256());
            AssertThrows<ObjectDisposedException>(() =>
                disposedLease.RevalidateExactSet(
                    NewArtifactDeadline(),
                    CancellationToken.None));
            File.WriteAllBytes(fixture.ExecutablePath, fixture.Image);
            return Task.CompletedTask;
        }
        finally
        {
            lease?.Dispose();
            CryptographicOperations.ZeroMemory(releaseManifest);
            CryptographicOperations.ZeroMemory(releasePin);
            CryptographicOperations.ZeroMemory(embeddedManifest);
            WipeNativeTestBytes(firstDigest);
            WipeNativeTestBytes(secondDigest);
            WipeNativeTestBytes(reproId);
        }
    }

    private static Task TestAuditedNativeReleaseFailureCleanup()
    {
        using NativeReleaseFixture legacyProfile = NativeReleaseFixture.Create(
            ReleaseArtifactRole.SyntheticTestHarness,
            ReleaseDeploymentKind.FrameworkDependentSnapshot,
            "role.exe");
        AssertAuditedNativeFailureReleasesFile<SecurityException>(legacyProfile);

        using NativeReleaseFixture shortImage = NativeReleaseFixture.Create(
            imageTransform: static image => image[..^1]);
        AssertAuditedNativeFailureReleasesFile<ArgumentException>(shortImage);

        using NativeReleaseFixture longImage = NativeReleaseFixture.Create(
            imageTransform: static image =>
            {
                byte[] result = new byte[image.Length + 1];
                image.CopyTo(result, 0);
                return result;
            });
        AssertAuditedNativeFailureReleasesFile<SecurityException>(longImage);

        using NativeReleaseFixture changedPe = NativeReleaseFixture.Create(
            imageTransform: static image =>
            {
                byte[] result = (byte[])image.Clone();
                result[0x9c0] = (byte)'U';
                RewriteNativePeChecksum(result);
                return result;
            });
        AssertAuditedNativeFailureReleasesFile<FormatException>(changedPe);

        using NativeReleaseFixture wrongEmbedded = NativeReleaseFixture.Create();
        wrongEmbedded.EmbeddedManifest[0] ^= 1;
        AssertAuditedNativeFailureReleasesFile<FormatException>(wrongEmbedded);
        return Task.CompletedTask;
    }

    private static Task TestAuditedNativeReleaseLateFailureOwnership()
    {
        using (NativeReleaseFixture copied = NativeReleaseFixture.Create())
        using (CancellationTokenSource cancelled = new())
        {
            byte[]? borrowedImage = null;
            AssertThrows<OperationCanceledException>(() =>
                {
                    using AuditedNativeFixtureReleaseLease ignored =
                        AuditedNativeFixtureReleaseLease.Open(
                            copied.Directory.Path,
                            copied.ReleaseManifest,
                            copied.ReleasePin,
                            copied.EmbeddedManifest,
                            NewArtifactDeadline(),
                            cancelled.Token,
                            (stage, image) =>
                            {
                                if (stage ==
                                    NativeFixtureReleaseOpenStage
                                        .AfterExactImageCopy)
                                {
                                    borrowedImage = image;
                                    cancelled.Cancel();
                                }
                            });
                });
            Assert(borrowedImage is not null && AllZero(borrowedImage),
                "a composite late-failure image snapshot must be wiped");
            RequireNativeFixtureWritable(copied);
            borrowedImage = null;
        }

        using (NativeReleaseFixture sibling = NativeReleaseFixture.Create())
        {
            string unexpectedPath = Path.Combine(
                sibling.Directory.Path,
                "late.dll");
            AssertThrows<SecurityException>(() =>
                {
                    using AuditedNativeFixtureReleaseLease ignored =
                        AuditedNativeFixtureReleaseLease.Open(
                            sibling.Directory.Path,
                            sibling.ReleaseManifest,
                            sibling.ReleasePin,
                            sibling.EmbeddedManifest,
                            NewArtifactDeadline(),
                            CancellationToken.None,
                            (stage, _) =>
                            {
                                if (stage ==
                                    NativeFixtureReleaseOpenStage
                                        .BeforeFinalRevalidation)
                                {
                                    File.WriteAllBytes(
                                        unexpectedPath,
                                        new byte[] { 0x01 });
                                }
                            });
                });
            File.Delete(unexpectedPath);
            RequireNativeFixtureWritable(sibling);
        }

        using (NativeReleaseFixture late = NativeReleaseFixture.Create())
        {
            ManualTimeProvider clock = new(CanonicalTestUtcNow());
            MonotonicDeadline deadline = MonotonicDeadline.Start(
                clock,
                TestTimeout);
            AssertThrows<TimeoutException>(() =>
                {
                    using AuditedNativeFixtureReleaseLease ignored =
                        AuditedNativeFixtureReleaseLease.Open(
                            late.Directory.Path,
                            late.ReleaseManifest,
                            late.ReleasePin,
                            late.EmbeddedManifest,
                            deadline,
                            CancellationToken.None,
                            (stage, _) =>
                            {
                                if (stage ==
                                    NativeFixtureReleaseOpenStage
                                        .AfterFinalRevalidation)
                                {
                                    clock.Advance(TestTimeout);
                                }
                            });
                });
            RequireNativeFixtureWritable(late);
        }

        return Task.CompletedTask;
    }

    private static void AssertAuditedNativeFailureReleasesFile<TException>(
        NativeReleaseFixture fixture)
        where TException : Exception
    {
        AssertThrows<TException>(() =>
        {
            using AuditedNativeFixtureReleaseLease ignored =
                AuditedNativeFixtureReleaseLease.Open(
                    fixture.Directory.Path,
                    fixture.ReleaseManifest,
                    fixture.ReleasePin,
                    fixture.EmbeddedManifest,
                    NewArtifactDeadline(),
                    CancellationToken.None);
        });
        RequireNativeFixtureWritable(fixture);
    }

    private static void RequireNativeFixtureWritable(
        NativeReleaseFixture fixture)
    {
        File.WriteAllBytes(fixture.ExecutablePath, fixture.Image);
        Assert(File.ReadAllBytes(fixture.ExecutablePath)
                .AsSpan()
                .SequenceEqual(fixture.Image),
            "a failed native-release composite retained its executable handle");
    }

    private sealed class NativeReleaseFixture : IDisposable
    {
        private NativeReleaseFixture(
            FilePublicationTestDirectory directory,
            string executableFileName,
            byte[] image,
            byte[] embeddedManifest,
            byte[] releaseManifest,
            byte[] releasePin)
        {
            Directory = directory;
            ExecutablePath = Path.Combine(directory.Path, executableFileName);
            Image = image;
            EmbeddedManifest = embeddedManifest;
            ReleaseManifest = releaseManifest;
            ReleasePin = releasePin;
        }

        internal FilePublicationTestDirectory Directory { get; }

        internal string ExecutablePath { get; }

        internal byte[] Image { get; }

        internal byte[] EmbeddedManifest { get; }

        internal byte[] ReleaseManifest { get; }

        internal byte[] ReleasePin { get; }

        internal static NativeReleaseFixture Create(
            ReleaseArtifactRole artifactRole =
                ReleaseArtifactRole.SyntheticNativeFixture,
            ReleaseDeploymentKind deploymentKind =
                ReleaseDeploymentKind.NativeNoCrtSystem32Fixture,
            string executableFileName = NativeFixtureFileName,
            Func<byte[], byte[]>? imageTransform = null)
        {
            FilePublicationTestDirectory? directory = null;
            NativeFixtureInputs? inputs = null;
            byte[]? image = null;
            byte[]? embeddedManifest = null;
            byte[]? releaseManifest = null;
            byte[]? releasePin = null;
            try
            {
                directory = new FilePublicationTestDirectory();
                inputs = ReadNativeFixtureInputs();
                image = imageTransform is null
                    ? (byte[])inputs.Image.Clone()
                    : imageTransform(inputs.Image);
                embeddedManifest = (byte[])inputs.Manifest.Clone();
                File.WriteAllBytes(
                    Path.Combine(directory.Path, executableFileName),
                    image);
                ReleaseArtifactContent[] artifacts =
                {
                    new(executableFileName, image),
                };
                byte[]? artifactSetManifest = null;
                try
                {
                    TrustedArtifactExpectation[] expectations =
                    {
                        ArtifactExpectation(executableFileName, image),
                    };
                    using TrustedArtifactSetLease set = OpenArtifactSet(
                        directory.Path,
                        executableFileName,
                        expectations);
                    artifactSetManifest = set.CopyManifestSha256();
                    releaseManifest = EncodeReleaseManifest(
                        executableFileName,
                        artifacts,
                        artifactSetManifest,
                        artifactRole: artifactRole,
                        deploymentKind: deploymentKind);
                }
                finally
                {
                    WipeNativeTestBytes(artifactSetManifest);
                }

                releasePin = ComputeReleaseManifestPin(releaseManifest);
                NativeReleaseFixture result = new(
                    directory,
                    executableFileName,
                    image,
                    embeddedManifest,
                    releaseManifest,
                    releasePin);
                directory = null;
                image = null;
                embeddedManifest = null;
                releaseManifest = null;
                releasePin = null;
                return result;
            }
            finally
            {
                inputs?.Dispose();
                directory?.Dispose();
                WipeNativeTestBytes(image);
                WipeNativeTestBytes(embeddedManifest);
                WipeNativeTestBytes(releaseManifest);
                WipeNativeTestBytes(releasePin);
            }
        }

        public void Dispose()
        {
            try
            {
                Directory.Dispose();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(Image);
                CryptographicOperations.ZeroMemory(EmbeddedManifest);
                CryptographicOperations.ZeroMemory(ReleaseManifest);
                CryptographicOperations.ZeroMemory(ReleasePin);
            }
        }
    }

    private sealed class TestNativeReleaseHookException : Exception
    {
    }
}
