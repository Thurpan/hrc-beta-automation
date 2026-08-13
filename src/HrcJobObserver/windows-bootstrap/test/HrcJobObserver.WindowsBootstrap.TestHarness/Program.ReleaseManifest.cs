using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class Program
{
    private static readonly byte[] ReleaseManifestPinDomain =
        Encoding.ASCII.GetBytes(
            "HRC-BETA-OBSERVER-RELEASE-MANIFEST-PIN-V1\0");

    private static Task TestPinnedReleaseManifestRoundTrip()
    {
        using FilePublicationTestDirectory directory = new();
        byte[] executable = { 0x4d, 0x5a, 0x51 };
        byte[] assembly = { 0x61, 0x73, 0x6d, 0x51 };
        byte[] runtimeConfig = Encoding.ASCII.GetBytes("{\"runtimeOptions\":{}}");
        byte[]? manifest = null;
        byte[]? pin = null;
        byte[]? retainedPin = null;
        byte[]? retainedArtifactSetManifest = null;
        PinnedReleaseArtifactSetLease? lease = null;
        try
        {
            ReleaseArtifactContent[] artifacts =
            {
                new("harness.exe", executable),
                new("harness.dll", assembly),
                new("harness.runtimeconfig.json", runtimeConfig),
            };
            WriteReleaseArtifacts(directory.Path, artifacts);
            manifest = CreateReleaseManifest(
                directory.Path,
                "harness.exe",
                artifacts);
            pin = ComputeReleaseManifestPin(manifest);

            lease = PinnedReleaseArtifactSetLease.Open(
                directory.Path,
                manifest,
                pin,
                NewArtifactDeadline(),
                CancellationToken.None);
            AssertEqual(
                ReleaseArtifactRole.SyntheticTestHarness,
                lease.ArtifactRole,
                "release artifact role");
            AssertEqual(
                ReleaseDeploymentKind.FrameworkDependentSnapshot,
                lease.DeploymentKind,
                "release deployment kind");
            AssertEqual(
                ReleaseTargetRuntimeIdentifier.WinX64,
                lease.TargetRuntimeIdentifier,
                "release target-runtime policy label");
            AssertEqual(directory.Path, lease.ApplicationDirectory,
                "release application directory");
            AssertEqual("harness.exe", lease.ExecutableRelativeFileName,
                "release executable filename");
            AssertEqual(3, lease.Count, "release artifact count");
            Assert(!lease.IsEligibleForTrustedLaunch,
                "a framework-dependent snapshot must remain ineligible for trusted launch");
            retainedPin = lease.CopyManifestPinSha256();
            retainedArtifactSetManifest =
                lease.CopyArtifactSetManifestSha256();
            Assert(retainedPin.AsSpan().SequenceEqual(pin),
                "the composite lease must retain the exact validated manifest pin");
            retainedPin[0] ^= 0xff;
            byte[] secondPin = lease.CopyManifestPinSha256();
            byte[] secondArtifactSetManifest =
                lease.CopyArtifactSetManifestSha256();
            try
            {
                Assert(secondPin.AsSpan().SequenceEqual(pin),
                    "manifest pin copies must be independent");
                Assert(secondArtifactSetManifest.AsSpan().SequenceEqual(
                        retainedArtifactSetManifest),
                    "artifact-set manifest copies must be independent");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secondPin);
                CryptographicOperations.ZeroMemory(secondArtifactSetManifest);
            }

            CryptographicOperations.ZeroMemory(manifest);
            CryptographicOperations.ZeroMemory(pin);
            lease.RevalidateExactSet(
                NewArtifactDeadline(),
                CancellationToken.None);
            lease.Dispose();
            AssertThrows<ObjectDisposedException>(() =>
                lease.RevalidateExactSet(
                    NewArtifactDeadline(),
                    CancellationToken.None));
            AssertThrows<ObjectDisposedException>(() =>
            {
                _ = lease.CopyManifestPinSha256();
            });
            AssertThrows<ObjectDisposedException>(() =>
            {
                _ = lease.CopyArtifactSetManifestSha256();
            });
            lease = null;

            File.WriteAllBytes(
                Path.Combine(directory.Path, "harness.dll"),
                new byte[] { 0x72, 0x65, 0x6c, 0x65, 0x61, 0x73, 0x65 });
            return Task.CompletedTask;
        }
        finally
        {
            lease?.Dispose();
            CryptographicOperations.ZeroMemory(executable);
            CryptographicOperations.ZeroMemory(assembly);
            CryptographicOperations.ZeroMemory(runtimeConfig);
            if (manifest is not null)
            {
                CryptographicOperations.ZeroMemory(manifest);
            }

            if (pin is not null)
            {
                CryptographicOperations.ZeroMemory(pin);
            }

            if (retainedPin is not null)
            {
                CryptographicOperations.ZeroMemory(retainedPin);
            }

            if (retainedArtifactSetManifest is not null)
            {
                CryptographicOperations.ZeroMemory(
                    retainedArtifactSetManifest);
            }
        }
    }

    private static Task TestPinnedReleaseManifestAuthenticatesBeforeParse()
    {
        byte[] malformed = new byte[ReleaseManifestV1.MaximumEncodedLength];
        byte[] wrongPin = new byte[32];
        byte[] correctPin = ComputeReleaseManifestPin(malformed);
        byte[] canonical = CreateGoldenLegacyReleaseManifest();
        byte[] canonicalPin = ComputeReleaseManifestPin(canonical);
        try
        {
            wrongPin[0] = 1;
            AssertThrows<SecurityException>(() =>
            {
                using PinnedReleaseArtifactSetLease ignored =
                    PinnedReleaseArtifactSetLease.Open(
                        @"C:\manifest-path-is-not-consulted",
                        malformed,
                        wrongPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            AssertThrows<FormatException>(() =>
            {
                using PinnedReleaseArtifactSetLease ignored =
                    PinnedReleaseArtifactSetLease.Open(
                        @"C:\manifest-path-is-not-consulted",
                        malformed,
                        correctPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using PinnedReleaseArtifactSetLease ignored =
                    PinnedReleaseArtifactSetLease.Open(
                        @"C:\manifest-path-is-not-consulted",
                        malformed,
                        wrongPin.AsSpan(0, 31),
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });

            using AuthenticatedReleaseManifestV1 authenticated =
                AuthenticatedReleaseManifestV1.Authenticate(
                    canonical,
                    canonicalPin,
                    NewArtifactDeadline(),
                    CancellationToken.None);
            AssertEqual(ReleaseArtifactRole.SyntheticTestHarness,
                authenticated.ArtifactRole,
                "standalone authenticated release role after failed parses");
            AssertEqual(1, authenticated.ArtifactCount,
                "standalone authenticated release artifact count");
            Assert(!authenticated.IsEligibleForTrustedLaunch,
                "standalone release authentication must not establish launch eligibility");
            AssertAuthenticatedReleaseManifestArtifactOwnership(
                authenticated,
                canonical,
                canonicalPin);
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(malformed);
            CryptographicOperations.ZeroMemory(wrongPin);
            CryptographicOperations.ZeroMemory(correctPin);
            CryptographicOperations.ZeroMemory(canonical);
            CryptographicOperations.ZeroMemory(canonicalPin);
        }
    }

    private static Task TestPinnedReleaseManifestRejectsNoncanonicalWires()
    {
        byte[] first = { 0x01 };
        byte[] second = { 0x02 };
        byte[] digest = SHA256.HashData(Array.Empty<byte>());
        try
        {
            ReleaseArtifactContent[] canonicalArtifacts =
            {
                new("role.dll", first),
                new("role.exe", second),
            };
            byte[] canonical = EncodeReleaseManifest(
                "role.exe",
                canonicalArtifacts,
                digest);
            try
            {
                AssertPinnedManifestFormatFailure(
                    MutateReleaseManifest(canonical, 0, 0));
                AssertPinnedManifestFormatFailure(
                    MutateReleaseManifest(canonical, 8, 2));
                AssertPinnedManifestFormatFailure(
                    MutateReleaseManifest(canonical, 9, 2));
                AssertPinnedManifestFormatFailure(
                    MutateReleaseManifest(canonical, 8, 3));
                AssertPinnedManifestFormatFailure(
                    MutateReleaseManifest(canonical, 9, 3));
                AssertPinnedManifestFormatFailure(
                    MutateReleaseManifest(canonical, 11, 2));
                AssertPinnedManifestFormatFailure(
                    MutateReleaseManifest(canonical, 12, 1));

                AssertPinnedManifestFormatFailure(EncodeReleaseManifest(
                    "role.exe",
                    new[]
                    {
                        new ReleaseArtifactContent("role.exe", second),
                        new ReleaseArtifactContent("role.dll", first),
                    },
                    digest,
                    preserveInputOrder: true));
                AssertPinnedManifestFormatFailure(EncodeReleaseManifest(
                    "role.exe",
                    new[]
                    {
                        new ReleaseArtifactContent("Role.dll", first),
                        new ReleaseArtifactContent("role.dll", second),
                        new ReleaseArtifactContent("role.exe", second),
                    },
                    digest,
                    preserveInputOrder: true));
                AssertPinnedManifestFormatFailure(EncodeReleaseManifest(
                    "missing.exe",
                    canonicalArtifacts,
                    digest));

                AssertPinnedManifestFormatFailure(EncodeReleaseManifest(
                    "Role.exe",
                    canonicalArtifacts,
                    digest));

                byte[] zeroCount = canonical.ToArray();
                BinaryPrimitives.WriteUInt32BigEndian(
                    zeroCount.AsSpan(26),
                    0);
                AssertPinnedManifestFormatFailure(zeroCount);

                byte[] tooManyCount = canonical.ToArray();
                BinaryPrimitives.WriteUInt32BigEndian(
                    tooManyCount.AsSpan(26),
                    TrustedArtifactSetLease.MaximumArtifactCount + 1U);
                AssertPinnedManifestFormatFailure(tooManyCount);

                byte[] excessiveLength = canonical.ToArray();
                BinaryPrimitives.WriteUInt64BigEndian(
                    excessiveLength.AsSpan(40),
                    checked((ulong)long.MaxValue + 1UL));
                AssertPinnedManifestFormatFailure(excessiveLength);

                byte[] nonAscii = canonical.ToArray();
                nonAscii[32] = 0x80;
                AssertPinnedManifestFormatFailure(nonAscii);

                AssertPinnedManifestFormatFailure(EncodeReleaseManifest(
                    "role.exe",
                    new[]
                    {
                        new ReleaseArtifactContent("folder\\role.dll", first),
                        new ReleaseArtifactContent("role.exe", second),
                    },
                    digest));

                AssertPinnedManifestFormatFailure(CreateOversizedNameWire(
                    canonical,
                    new string('a', 256)));

                byte[] trailing = new byte[canonical.Length + 1];
                canonical.CopyTo(trailing, 0);
                AssertPinnedManifestFormatFailure(trailing);
                AssertPinnedManifestFormatFailure(canonical[..^1]);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(canonical);
            }

            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(first);
            CryptographicOperations.ZeroMemory(second);
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static Task TestPinnedReleaseManifestBindsArtifactSetDigest()
    {
        using FilePublicationTestDirectory directory = new();
        byte[] executable = { 0x4d, 0x5a, 0x61 };
        byte[] assembly = { 0x61, 0x73, 0x6d, 0x61 };
        byte[]? manifest = null;
        byte[]? pin = null;
        try
        {
            ReleaseArtifactContent[] artifacts =
            {
                new("role.exe", executable),
                new("role.dll", assembly),
            };
            WriteReleaseArtifacts(directory.Path, artifacts);
            manifest = CreateReleaseManifest(
                directory.Path,
                "role.exe",
                artifacts);
            manifest[^1] ^= 0xff;
            pin = ComputeReleaseManifestPin(manifest);
            AssertThrows<SecurityException>(() =>
            {
                using PinnedReleaseArtifactSetLease ignored =
                    PinnedReleaseArtifactSetLease.Open(
                        directory.Path,
                        manifest,
                        pin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });

            File.WriteAllBytes(
                Path.Combine(directory.Path, "role.dll"),
                new byte[] { 0x66, 0x72, 0x65, 0x65 });
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(executable);
            CryptographicOperations.ZeroMemory(assembly);
            if (manifest is not null)
            {
                CryptographicOperations.ZeroMemory(manifest);
            }

            if (pin is not null)
            {
                CryptographicOperations.ZeroMemory(pin);
            }
        }
    }

    private static Task TestPinnedReleaseManifestBounds()
    {
        byte[] content = { 0x4d, 0x5a };
        byte[] digest = SHA256.HashData(Array.Empty<byte>());
        byte[]? manifest = null;
        byte[]? pin = null;
        try
        {
            manifest = EncodeReleaseManifest(
                "role.exe",
                new[] { new ReleaseArtifactContent("role.exe", content) },
                digest);
            pin = ComputeReleaseManifestPin(manifest);
            using CancellationTokenSource cancelled = new();
            cancelled.Cancel();
            AssertThrows<OperationCanceledException>(() =>
            {
                using PinnedReleaseArtifactSetLease ignored =
                    PinnedReleaseArtifactSetLease.Open(
                        @"C:\unused",
                        manifest,
                        pin,
                        NewArtifactDeadline(),
                        cancelled.Token);
            });

            ManualTimeProvider expiredClock = new(CanonicalTestUtcNow());
            MonotonicDeadline expired = MonotonicDeadline.Start(
                expiredClock,
                TestTimeout);
            expiredClock.Advance(TestTimeout);
            AssertThrows<TimeoutException>(() =>
            {
                using PinnedReleaseArtifactSetLease ignored =
                    PinnedReleaseArtifactSetLease.Open(
                        @"C:\unused",
                        manifest,
                        pin,
                        expired,
                        CancellationToken.None);
            });

            AdvancingTimestampTimeProvider advancing = new();
            MonotonicDeadline hashDeadline = MonotonicDeadline.Start(
                advancing,
                TimeSpan.FromTicks(4));
            AssertThrows<TimeoutException>(() =>
            {
                using PinnedReleaseArtifactSetLease ignored =
                    PinnedReleaseArtifactSetLease.Open(
                        @"C:\unused",
                        manifest,
                        pin,
                        hashDeadline,
                        CancellationToken.None);
            });

            byte[] oversized = new byte[
                ReleaseManifestV1.MaximumEncodedLength + 1];
            byte[] oversizedPin = ComputeReleaseManifestPin(oversized);
            try
            {
                AssertThrows<ArgumentException>(() =>
                {
                    using PinnedReleaseArtifactSetLease ignored =
                        PinnedReleaseArtifactSetLease.Open(
                            @"C:\unused",
                            oversized,
                            oversizedPin,
                            NewArtifactDeadline(),
                            CancellationToken.None);
                });
            }
            finally
            {
                CryptographicOperations.ZeroMemory(oversized);
                CryptographicOperations.ZeroMemory(oversizedPin);
            }

            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(content);
            CryptographicOperations.ZeroMemory(digest);
            if (manifest is not null)
            {
                CryptographicOperations.ZeroMemory(manifest);
            }

            if (pin is not null)
            {
                CryptographicOperations.ZeroMemory(pin);
            }
        }
    }

    private static Task TestPinnedReleaseManifestGoldenIdentity()
    {
        byte[] wire = Convert.FromHexString(
            "48524352454c303101010001000000000008726f6c652e657865" +
            "000000010008726f6c652e6578650000000000000002" +
            "112233445566778899aabbccddeeff00112233445566778899aabbccddeeff00" +
            "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899");
        byte[] expectedPin = Convert.FromHexString(
            "b6d5aaceed16f2860b237ffd94dfe1a44d46e5c19932a5365219930159f789cc");
        byte[] actualPin = ComputeReleaseManifestPin(wire);
        try
        {
            Assert(actualPin.AsSpan().SequenceEqual(expectedPin),
                "the independent golden release-manifest pin changed");
            using ReleaseManifestV1 parsed = ReleaseManifestV1
                .ParseStructuralCanonical(
                    wire,
                    NewArtifactDeadline(),
                    CancellationToken.None);
            AssertEqual("role.exe", parsed.ExecutableRelativeFileName,
                "golden release executable filename");
            AssertEqual(1, parsed.ArtifactCount,
                "golden release artifact count");
            Assert(!parsed.IsEligibleForTrustedLaunch,
                "structural manifest parsing must not establish launch eligibility");
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wire);
            CryptographicOperations.ZeroMemory(expectedPin);
            CryptographicOperations.ZeroMemory(actualPin);
        }
    }

    private static void AssertAuthenticatedReleaseManifestArtifactOwnership(
        AuthenticatedReleaseManifestV1 authenticated,
        ReadOnlySpan<byte> canonical,
        ReadOnlySpan<byte> canonicalPin)
    {
        TrustedArtifactExpectation[]? first = null;
        TrustedArtifactExpectation[]? beforeOwnerDisposal = null;
        byte[]? retainedWire = null;
        byte[]? retainedPin = null;
        try
        {
            first = authenticated.CopyArtifacts();
            AssertEqual(1, first.Length,
                "first independently owned release artifact array length");
            byte[] firstDigest = first[0].CopySha256Digest();
            try
            {
                first[0].Dispose();
                byte[] wipedFirstDigest = first[0].CopySha256Digest();
                try
                {
                    Assert(wipedFirstDigest.All(value => value == 0),
                        "disposing an artifact copy must wipe that copy");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(wipedFirstDigest);
                }

                TrustedArtifactExpectation[] fresh =
                    authenticated.CopyArtifacts();
                try
                {
                    byte[] freshDigest = fresh[0].CopySha256Digest();
                    try
                    {
                        Assert(freshDigest.Any(value => value != 0),
                            "disposing an artifact copy must not wipe the authenticated owner");
                        Assert(freshDigest.AsSpan().SequenceEqual(firstDigest),
                            "the authenticated owner must retain its exact artifact digest");
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(freshDigest);
                    }
                }
                finally
                {
                    DisposeTrustedArtifactExpectations(fresh);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(firstDigest);
            }

            beforeOwnerDisposal = authenticated.CopyArtifacts();
            retainedWire = authenticated.CopyCanonicalManifest();
            retainedPin = authenticated.CopyManifestPinSha256();
            authenticated.Dispose();
            byte[] survivingDigest =
                beforeOwnerDisposal[0].CopySha256Digest();
            try
            {
                Assert(survivingDigest.Any(value => value != 0),
                    "disposing the authenticated owner must not wipe prior artifact copies");
                Assert(retainedWire.AsSpan().SequenceEqual(canonical),
                    "disposing the authenticated owner must not alter prior wire copies");
                Assert(retainedPin.AsSpan().SequenceEqual(canonicalPin),
                    "disposing the authenticated owner must not alter prior pin copies");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(survivingDigest);
            }

            AssertThrows<ObjectDisposedException>(() =>
                authenticated.CopyArtifacts());
            AssertThrows<ObjectDisposedException>(() =>
                authenticated.CopyCanonicalManifest());
        }
        finally
        {
            DisposeTrustedArtifactExpectations(first);
            DisposeTrustedArtifactExpectations(beforeOwnerDisposal);
            WipeNativeLaunchPolicyBytes(retainedWire);
            WipeNativeLaunchPolicyBytes(retainedPin);
        }
    }

    private static void DisposeTrustedArtifactExpectations(
        TrustedArtifactExpectation[]? artifacts)
    {
        if (artifacts is null)
        {
            return;
        }

        foreach (TrustedArtifactExpectation artifact in artifacts)
        {
            artifact.Dispose();
        }
    }

    private static byte[] CreateReleaseManifest(
        string applicationDirectory,
        string executableRelativeFileName,
        IReadOnlyList<ReleaseArtifactContent> artifacts)
    {
        TrustedArtifactExpectation[] expectations = artifacts
            .Select(static artifact => ArtifactExpectation(
                artifact.RelativeFileName,
                artifact.Content.Span))
            .ToArray();
        byte[]? artifactSetManifest = null;
        try
        {
            using (TrustedArtifactSetLease set = OpenArtifactSet(
                       applicationDirectory,
                       executableRelativeFileName,
                       expectations))
            {
                artifactSetManifest = set.CopyManifestSha256();
            }

            return EncodeReleaseManifest(
                executableRelativeFileName,
                artifacts,
                artifactSetManifest);
        }
        finally
        {
            if (artifactSetManifest is not null)
            {
                CryptographicOperations.ZeroMemory(artifactSetManifest);
            }
        }
    }

    private static byte[] EncodeReleaseManifest(
        string executableRelativeFileName,
        IReadOnlyList<ReleaseArtifactContent> artifacts,
        ReadOnlySpan<byte> artifactSetManifestSha256,
        bool preserveInputOrder = false,
        ReleaseArtifactRole artifactRole =
            ReleaseArtifactRole.SyntheticTestHarness,
        ReleaseDeploymentKind deploymentKind =
            ReleaseDeploymentKind.FrameworkDependentSnapshot)
    {
        IEnumerable<ReleaseArtifactContent> ordered = preserveInputOrder
            ? artifacts
            : artifacts.OrderBy(
                static artifact => artifact.RelativeFileName,
                StringComparer.Ordinal);
        using MemoryStream output = new(ReleaseManifestV1.MaximumEncodedLength);
        output.Write(Encoding.ASCII.GetBytes("HRCREL01"));
        output.WriteByte((byte)artifactRole);
        output.WriteByte((byte)deploymentKind);
        BootstrapBinary.WriteUInt16(
            output,
            (ushort)ReleaseTargetRuntimeIdentifier.WinX64);
        BootstrapBinary.WriteUInt32(output, 0);
        WriteReleaseFileName(output, executableRelativeFileName);
        BootstrapBinary.WriteUInt32(output, checked((uint)artifacts.Count));
        foreach (ReleaseArtifactContent artifact in ordered)
        {
            WriteReleaseFileName(output, artifact.RelativeFileName);
            BootstrapBinary.WriteUInt64(
                output,
                checked((ulong)artifact.Content.Length));
            byte[] digest = SHA256.HashData(artifact.Content.Span);
            try
            {
                output.Write(digest);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }

        output.Write(artifactSetManifestSha256);
        return output.ToArray();
    }

    private static void WriteReleaseFileName(
        Stream output,
        string relativeFileName)
    {
        byte[] name = Encoding.ASCII.GetBytes(relativeFileName);
        try
        {
            BootstrapBinary.WriteUInt16(output, checked((ushort)name.Length));
            output.Write(name);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(name);
        }
    }

    private static void WriteReleaseArtifacts(
        string applicationDirectory,
        IEnumerable<ReleaseArtifactContent> artifacts)
    {
        foreach (ReleaseArtifactContent artifact in artifacts)
        {
            File.WriteAllBytes(
                Path.Combine(applicationDirectory, artifact.RelativeFileName),
                artifact.Content.ToArray());
        }
    }

    private static byte[] ComputeReleaseManifestPin(ReadOnlySpan<byte> manifest)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        hash.AppendData(ReleaseManifestPinDomain);
        hash.AppendData(manifest);
        return hash.GetHashAndReset();
    }

    private static void AssertPinnedManifestFormatFailure(byte[] manifest)
    {
        byte[] pin = ComputeReleaseManifestPin(manifest);
        try
        {
            AssertThrows<FormatException>(() =>
            {
                using PinnedReleaseArtifactSetLease ignored =
                    PinnedReleaseArtifactSetLease.Open(
                        @"C:\manifest-path-is-not-consulted",
                        manifest,
                        pin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(manifest);
            CryptographicOperations.ZeroMemory(pin);
        }
    }

    private static byte[] MutateReleaseManifest(
        ReadOnlySpan<byte> source,
        int offset,
        byte value)
    {
        byte[] result = source.ToArray();
        result[offset] = value;
        return result;
    }

    private static byte[] CreateOversizedNameWire(
        ReadOnlySpan<byte> canonical,
        string oversizedName)
    {
        byte[] name = Encoding.ASCII.GetBytes(oversizedName);
        try
        {
            using MemoryStream output = new();
            output.Write(canonical[..30]);
            BootstrapBinary.WriteUInt16(output, checked((ushort)name.Length));
            output.Write(name);
            output.Write(canonical[40..]);
            return output.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(name);
        }
    }

    private readonly record struct ReleaseArtifactContent(
        string RelativeFileName,
        ReadOnlyMemory<byte> Content);

    private sealed class AdvancingTimestampTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return Interlocked.Increment(ref timestamp);
        }
    }
}
