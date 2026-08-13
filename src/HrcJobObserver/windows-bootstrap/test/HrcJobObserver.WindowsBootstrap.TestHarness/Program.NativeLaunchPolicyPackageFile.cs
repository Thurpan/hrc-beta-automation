using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class Program
{
    private const string ExpectedNativeLaunchPolicyPackageFileName =
        "native-launch-policy-v1.bin";

    private static Task TestNativeLaunchPolicyPackageFileRoundTrip()
    {
        using NativeLaunchPolicyPackageFileFixture fixture =
            CreateNativeLaunchPolicyPackageFileFixture();
        AssertEqual(
            ExpectedNativeLaunchPolicyPackageFileName,
            NativeLaunchPolicyPackageFileLease.PackageFileName,
            "independent native launch-policy fixed leaf");
        byte[] expectedPackage = (byte[])fixture.Package.Clone();
        byte[] expectedPin = (byte[])fixture.PackagePinSha256.Clone();
        byte[] sibling = { 0x73, 0x69, 0x62, 0x6c, 0x69, 0x6e, 0x67 };
        string siblingPath = Path.Combine(fixture.Directory.Path, "unrelated.keep");
        string movedRoot = fixture.Directory.Path + "-moved";
        byte[]? snapshot = null;
        byte[]? packageCopy = null;
        byte[]? pinCopy = null;
        byte[]? releaseCopy = null;
        byte[]? releasePinCopy = null;
        byte[]? moduleCopy = null;
        byte[]? modulePinCopy = null;
        byte[]? retainedExpectedPin = null;
        byte[]? retainedPackage = null;
        byte[]? retainedPackagePin = null;
        NativeLaunchPolicyPackageFileLease? lease = null;
        try
        {
            File.WriteAllBytes(siblingPath, sibling);
            List<NativeLaunchPolicyPackageFileStage> stages = new();
            lease = NativeLaunchPolicyPackageFileLease.Open(
                fixture.Directory.Path,
                fixture.Directory.OwnerSid,
                fixture.PackagePinSha256,
                NewArtifactDeadline(),
                CancellationToken.None,
                (stage, borrowed) =>
                {
                    stages.Add(stage);
                    if (stage == NativeLaunchPolicyPackageFileStage
                            .DirectoryValidated)
                    {
                        CryptographicOperations.ZeroMemory(
                            fixture.PackagePinSha256);
                        CryptographicOperations.ZeroMemory(fixture.Package);
                    }

                    if (stage == NativeLaunchPolicyPackageFileStage.SnapshotRead)
                    {
                        snapshot = borrowed ?? throw new InvalidOperationException(
                            "The successful package-file snapshot is missing.");
                    }
                    else
                    {
                        Assert(borrowed is null,
                            "only SnapshotRead may expose a borrowed package buffer");
                    }
                });
            Assert(stages.SequenceEqual(Enum.GetValues<
                    NativeLaunchPolicyPackageFileStage>()),
                "the package-file open stages must remain exact and ordered");
            Assert(snapshot is not null && AllZero(snapshot),
                "successful package-file authentication must wipe its borrowed snapshot");

            Assert(AllZero(fixture.Package) &&
                    AllZero(fixture.PackagePinSha256),
                "the selector must own its external pin before guarded filesystem work");
            AssertEqual(fixture.PackagePath, lease.PackageFilePath,
                "selected native launch-policy package path");
            AssertEqual(NativeLaunchPolicyProfile.SyntheticNativeFixture,
                lease.Profile, "selected native launch-policy profile");
            AssertEqual(GoldenNativeLaunchPolicyGeneration, lease.Generation,
                "selected native launch-policy generation");
            AssertEqual(ReleaseArtifactRole.SyntheticNativeFixture,
                lease.ReleaseArtifactRole, "selected release role");
            AssertEqual(ReleaseDeploymentKind.NativeNoCrtSystem32Fixture,
                lease.ReleaseDeploymentKind, "selected deployment kind");
            AssertEqual(ReleaseTargetRuntimeIdentifier.WinX64,
                lease.TargetRuntimeIdentifier, "selected runtime label");
            AssertEqual(
                TrustedNativeSystemModuleConsumerProfile.SyntheticNativeFixture,
                lease.NativeSystemModuleConsumerProfile,
                "selected module consumer profile");
            Assert(!lease.IsEligibleForTrustedLaunch,
                "the selected package must remain ineligible for trusted launch");

            packageCopy = lease.CopyCanonicalPackage();
            pinCopy = lease.CopyPackagePinSha256();
            releaseCopy = lease.CopyCanonicalReleaseManifest();
            releasePinCopy = lease.CopyReleaseManifestPinSha256();
            moduleCopy = lease.CopyCanonicalNativeSystemModulePolicy();
            modulePinCopy = lease.CopyNativeSystemModulePolicyPinSha256();
            Assert(packageCopy.AsSpan().SequenceEqual(expectedPackage),
                "selected package copy");
            Assert(pinCopy.AsSpan().SequenceEqual(expectedPin),
                "selected package-pin copy");
            uint releaseLength = BinaryPrimitives.ReadUInt32BigEndian(
                expectedPackage.AsSpan(20, sizeof(uint)));
            Assert(releaseCopy.AsSpan().SequenceEqual(
                    expectedPackage.AsSpan(92, checked((int)releaseLength))),
                "selected nested release copy");
            Assert(moduleCopy.AsSpan().SequenceEqual(
                    expectedPackage.AsSpan(checked(92 + (int)releaseLength))),
                "selected nested module-policy copy");
            Assert(releasePinCopy.AsSpan().SequenceEqual(
                    expectedPackage.AsSpan(28, 32)),
                "selected nested release pin");
            Assert(modulePinCopy.AsSpan().SequenceEqual(
                    expectedPackage.AsSpan(60, 32)),
                "selected nested module-policy pin");
            packageCopy[0] ^= 0xff;
            pinCopy[0] ^= 0xff;
            byte[] independentPackage = lease.CopyCanonicalPackage();
            byte[] independentPin = lease.CopyPackagePinSha256();
            try
            {
                Assert(independentPackage.AsSpan().SequenceEqual(expectedPackage) &&
                        independentPin.AsSpan().SequenceEqual(expectedPin),
                    "selected package copies must be independently owned");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(independentPackage);
                CryptographicOperations.ZeroMemory(independentPin);
            }

            lease.Revalidate(NewArtifactDeadline(), CancellationToken.None);
            using (FileStream reader = new(
                fixture.PackagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                AssertEqual((long)expectedPackage.Length, reader.Length,
                    "concurrent selected-package reader length");
            }

            AssertPackageFileWriteDeleteAndRootRenameBlocked(
                fixture,
                movedRoot);
            Assert(File.ReadAllBytes(siblingPath).AsSpan().SequenceEqual(sibling),
                "fixed-leaf selection must allow and preserve unrelated siblings");

            retainedExpectedPin = GetPrivateField(
                lease,
                "expectedPackagePinSha256") as byte[] ??
                throw new InvalidOperationException(
                    "The selected outer-pin backing is unavailable.");
            NativeLaunchPolicyPackageV1 retainedPolicy = GetPrivateField(
                lease,
                "package") as NativeLaunchPolicyPackageV1 ??
                throw new InvalidOperationException(
                    "The retained selected package is unavailable.");
            retainedPackage = GetPrivateField(
                retainedPolicy,
                "canonicalPackage") as byte[] ??
                throw new InvalidOperationException(
                    "The retained package backing is unavailable.");
            retainedPackagePin = GetPrivateField(
                retainedPolicy,
                "packagePinSha256") as byte[] ??
                throw new InvalidOperationException(
                    "The retained package-pin backing is unavailable.");

            lease.Dispose();
            lease.Dispose();
            Assert(AllZero(retainedExpectedPin) && AllZero(retainedPackage) &&
                    AllZero(retainedPackagePin),
                "disposing the package-file lease must wipe every retained package authority buffer");
            AssertThrows<ObjectDisposedException>(() =>
            {
                _ = lease.PackageFilePath;
            });
            AssertThrows<ObjectDisposedException>(() =>
                lease.Revalidate(NewArtifactDeadline(), CancellationToken.None));
            AssertThrows<ObjectDisposedException>(() =>
            {
                _ = lease.CopyCanonicalPackage();
            });
            lease = null;

            AssertPackageFileReleased(fixture, expectedPackage, movedRoot);
            return Task.CompletedTask;
        }
        finally
        {
            lease?.Dispose();
            WipeNativeLaunchPolicyBytes(expectedPackage);
            WipeNativeLaunchPolicyBytes(expectedPin);
            WipeNativeLaunchPolicyBytes(sibling);
            WipeNativeLaunchPolicyBytes(packageCopy);
            WipeNativeLaunchPolicyBytes(pinCopy);
            WipeNativeLaunchPolicyBytes(releaseCopy);
            WipeNativeLaunchPolicyBytes(releasePinCopy);
            WipeNativeLaunchPolicyBytes(moduleCopy);
            WipeNativeLaunchPolicyBytes(modulePinCopy);
        }
    }

    private static Task TestNativeLaunchPolicyPackageFileAuthenticationOrder()
    {
        using NativeLaunchPolicyPackageFileFixture fixture =
            CreateNativeLaunchPolicyPackageFileFixture();
        byte[] malformed = (byte[])fixture.Package.Clone();
        malformed[0] ^= 0xff;
        byte[] correctMalformedPin = ComputeNativeLaunchPolicyPackagePin(malformed);
        byte[] wrongMalformedPin = (byte[])correctMalformedPin.Clone();
        wrongMalformedPin[0] ^= 0xff;
        byte[] shortPin = fixture.PackagePinSha256.AsSpan(0, 31).ToArray();
        byte[] longPin = new byte[33];
        fixture.PackagePinSha256.CopyTo(longPin, 0);
        byte[] rawSha256 = SHA256.HashData(fixture.Package);
        byte[] tooShort = new byte[NativeLaunchPolicyPackageV1.MinimumEncodedLength - 1];
        byte[] tooLong = new byte[NativeLaunchPolicyPackageV1.MaximumEncodedLength + 1];
        try
        {
            AssertThrows<ArgumentException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    NativeLaunchPolicyPackageFileLease.Open(
                        fixture.Directory.Path,
                        fixture.Directory.OwnerSid,
                        shortPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    NativeLaunchPolicyPackageFileLease.Open(
                        fixture.Directory.Path,
                        fixture.Directory.OwnerSid,
                        longPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });

            fixture.ReplacePackageFile(malformed);
            byte[] malformedBackup = (byte[])malformed.Clone();
            byte[] wrongPinBackup = (byte[])wrongMalformedPin.Clone();
            AssertThrows<SecurityException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    NativeLaunchPolicyPackageFileLease.Open(
                        fixture.Directory.Path,
                        fixture.Directory.OwnerSid,
                        wrongMalformedPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            Assert(malformed.AsSpan().SequenceEqual(malformedBackup) &&
                    wrongMalformedPin.AsSpan().SequenceEqual(wrongPinBackup) &&
                    File.ReadAllBytes(fixture.PackagePath).AsSpan()
                        .SequenceEqual(malformedBackup),
                "failed outer authentication must not mutate caller or file bytes");
            AssertThrows<FormatException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    NativeLaunchPolicyPackageFileLease.Open(
                        fixture.Directory.Path,
                        fixture.Directory.OwnerSid,
                        correctMalformedPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            WipeNativeLaunchPolicyBytes(malformedBackup);
            WipeNativeLaunchPolicyBytes(wrongPinBackup);

            fixture.ReplacePackageFile(fixture.Package);
            AssertThrows<SecurityException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    NativeLaunchPolicyPackageFileLease.Open(
                        fixture.Directory.Path,
                        fixture.Directory.OwnerSid,
                        rawSha256,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            using (NativeLaunchPolicyPackageFileLease correct =
                NativeLaunchPolicyPackageFileLease.Open(
                    fixture.Directory.Path,
                    fixture.Directory.OwnerSid,
                    fixture.PackagePinSha256,
                    NewArtifactDeadline(),
                    CancellationToken.None))
            {
                AssertEqual(GoldenNativeLaunchPolicyGeneration,
                    correct.Generation,
                    "domain-separated outer pin must authenticate the fixed file");
            }

            foreach (byte[] invalidLength in new[] { tooShort, tooLong })
            {
                fixture.ReplacePackageFile(invalidLength);
                bool snapshotRead = false;
                AssertThrows<InvalidDataException>(() =>
                {
                    using NativeLaunchPolicyPackageFileLease ignored =
                        NativeLaunchPolicyPackageFileLease.Open(
                            fixture.Directory.Path,
                            fixture.Directory.OwnerSid,
                            fixture.PackagePinSha256,
                            NewArtifactDeadline(),
                            CancellationToken.None,
                            (stage, _) => snapshotRead |= stage ==
                                NativeLaunchPolicyPackageFileStage.SnapshotRead);
                });
                Assert(!snapshotRead,
                    "out-of-range package files must fail before snapshot allocation");
            }

            return Task.CompletedTask;
        }
        finally
        {
            WipeNativeLaunchPolicyBytes(malformed);
            WipeNativeLaunchPolicyBytes(correctMalformedPin);
            WipeNativeLaunchPolicyBytes(wrongMalformedPin);
            WipeNativeLaunchPolicyBytes(shortPin);
            WipeNativeLaunchPolicyBytes(longPin);
            WipeNativeLaunchPolicyBytes(rawSha256);
            WipeNativeLaunchPolicyBytes(tooShort);
            WipeNativeLaunchPolicyBytes(tooLong);
        }
    }

    private static Task TestNativeLaunchPolicyPackageFileNamespaceGuards()
    {
        using (NativeLaunchPolicyPackageFileFixture fixture =
            CreateNativeLaunchPolicyPackageFileFixture())
        {
            AssertThrows<SecurityException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    OpenNativeLaunchPolicyPackageFile(fixture, "S-1-1-0");
            });
            foreach (string invalidPath in new[]
            {
                Path.GetFileName(fixture.Directory.Path),
                @"\\server\share\package-policy",
                @"\\?\C:\package-policy",
                fixture.Directory.Path + Path.DirectorySeparatorChar,
                Path.Combine(fixture.Directory.Path, "."),
            })
            {
                AssertThrows<ArgumentException>(() =>
                {
                    using NativeLaunchPolicyPackageFileLease ignored =
                        NativeLaunchPolicyPackageFileLease.Open(
                            invalidPath,
                            fixture.Directory.OwnerSid,
                            fixture.PackagePinSha256,
                            NewArtifactDeadline(),
                            CancellationToken.None);
                });
            }
        }

        using (FilePublicationTestDirectory missing = new())
        {
            AssertThrows<FileNotFoundException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    NativeLaunchPolicyPackageFileLease.Open(
                        missing.Path,
                        missing.OwnerSid,
                        new byte[32],
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
        }

        using (NativeLaunchPolicyPackageFileFixture wrongLeafAcl =
            CreateNativeLaunchPolicyPackageFileFixture())
        {
            wrongLeafAcl.ReplacePackageFile(
                wrongLeafAcl.Package,
                includeSystem: false);
            AssertThrows<SecurityException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    OpenNativeLaunchPolicyPackageFile(wrongLeafAcl);
            });
        }

        using (FilePublicationTestDirectory wrongRootAcl = new(
            includeSystem: false))
        {
            byte[] package = CreateIndependentNativeLaunchPolicyPackage(out byte[] pin);
            try
            {
                CreateProtectedTestFile(
                    Path.Combine(
                        wrongRootAcl.Path,
                        ExpectedNativeLaunchPolicyPackageFileName),
                    wrongRootAcl.OwnerSid,
                    package,
                    includeSystem: false);
                AssertThrows<SecurityException>(() =>
                {
                    using NativeLaunchPolicyPackageFileLease ignored =
                        NativeLaunchPolicyPackageFileLease.Open(
                            wrongRootAcl.Path,
                            wrongRootAcl.OwnerSid,
                            pin,
                            NewArtifactDeadline(),
                            CancellationToken.None);
                });
            }
            finally
            {
                WipeNativeLaunchPolicyBytes(package);
                WipeNativeLaunchPolicyBytes(pin);
            }
        }

        using (NativeLaunchPolicyPackageFileFixture wrongCase =
            CreateNativeLaunchPolicyPackageFileFixture())
        {
            File.Delete(wrongCase.PackagePath);
            string upper = Path.Combine(
                wrongCase.Directory.Path,
                ExpectedNativeLaunchPolicyPackageFileName.ToUpperInvariant());
            CreateProtectedTestFile(
                upper,
                wrongCase.Directory.OwnerSid,
                wrongCase.Package,
                includeSystem: true);
            AssertThrows<SecurityException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    OpenNativeLaunchPolicyPackageFile(wrongCase);
            });
        }

        using (NativeLaunchPolicyPackageFileFixture hardLink =
            CreateNativeLaunchPolicyPackageFileFixture())
        {
            CreateHardLinkForTest(
                Path.Combine(hardLink.Directory.Path, "package-hard-link.bin"),
                hardLink.PackagePath);
            AssertThrows<SecurityException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    OpenNativeLaunchPolicyPackageFile(hardLink);
            });
        }

        using (FilePublicationTestDirectory outer = new())
        using (NativeLaunchPolicyPackageFileFixture target =
            CreateNativeLaunchPolicyPackageFileFixture())
        {
            string junction = Path.Combine(outer.Path, "policy-root-junction");
            CreateProtectedTestDirectory(
                junction,
                outer.OwnerSid,
                includeSystem: true);
            CreateDirectoryJunction(junction, target.Directory.Path);
            AssertThrows<SecurityException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    NativeLaunchPolicyPackageFileLease.Open(
                        junction,
                        outer.OwnerSid,
                        target.PackagePinSha256,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
        }

        using (NativeLaunchPolicyPackageFileFixture leafJunction =
            CreateNativeLaunchPolicyPackageFileFixture())
        using (FilePublicationTestDirectory target = new())
        {
            File.Delete(leafJunction.PackagePath);
            CreateProtectedTestDirectory(
                leafJunction.PackagePath,
                leafJunction.Directory.OwnerSid,
                includeSystem: true);
            CreateDirectoryJunction(leafJunction.PackagePath, target.Path);
            AssertThrowsAny(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    OpenNativeLaunchPolicyPackageFile(leafJunction);
            }, typeof(SecurityException), typeof(Win32Exception),
                typeof(IOException));
        }

        using (NativeLaunchPolicyPackageFileFixture writerConflict =
            CreateNativeLaunchPolicyPackageFileFixture())
        {
            using FileStream writer = new(
                writerConflict.PackagePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.ReadWrite | FileShare.Delete);
            AssertThrows<Win32Exception>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    OpenNativeLaunchPolicyPackageFile(writerConflict);
            });
        }

        using (NativeLaunchPolicyPackageFileFixture mappingConflict =
            CreateNativeLaunchPolicyPackageFileFixture())
        {
            using FileStream writer = new(
                mappingConflict.PackagePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.ReadWrite | FileShare.Delete);
            using MemoryMappedFile mapping = MemoryMappedFile.CreateFromFile(
                writer,
                mapName: null,
                capacity: 0,
                MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None,
                leaveOpen: true);
            using MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0,
                mappingConflict.Package.Length,
                MemoryMappedFileAccess.ReadWrite);
            writer.Dispose();
            AssertThrows<Win32Exception>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    OpenNativeLaunchPolicyPackageFile(mappingConflict);
            });
        }

        using (NativeLaunchPolicyPackageFileFixture replacement =
            CreateNativeLaunchPolicyPackageFileFixture())
        {
            bool replaced = false;
            AssertThrows<SecurityException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    NativeLaunchPolicyPackageFileLease.Open(
                        replacement.Directory.Path,
                        replacement.Directory.OwnerSid,
                        replacement.PackagePinSha256,
                        NewArtifactDeadline(),
                        CancellationToken.None,
                        (stage, _) =>
                        {
                            if (stage == NativeLaunchPolicyPackageFileStage
                                    .LeafIdentityEnumerated && !replaced)
                            {
                                PosixUnlinkTestFile(replacement.PackagePath);
                                CreateProtectedTestFile(
                                    replacement.PackagePath,
                                    replacement.Directory.OwnerSid,
                                    replacement.Package,
                                    includeSystem: true);
                                replaced = true;
                            }
                        });
            });
            Assert(replaced,
                "the enumerate-to-open replacement hook must run");
        }

        using (NativeLaunchPolicyPackageFileFixture retained =
            CreateNativeLaunchPolicyPackageFileFixture())
        {
            bool deleteAttempted = false;
            bool deleteRejected = false;
            using NativeLaunchPolicyPackageFileLease lease =
                NativeLaunchPolicyPackageFileLease.Open(
                    retained.Directory.Path,
                    retained.Directory.OwnerSid,
                    retained.PackagePinSha256,
                    NewArtifactDeadline(),
                    CancellationToken.None,
                    (stage, _) =>
                    {
                        if (stage == NativeLaunchPolicyPackageFileStage
                                .LeafHandleOpened && !deleteAttempted)
                        {
                            deleteAttempted = true;
                            try
                            {
                                PosixUnlinkTestFile(retained.PackagePath);
                            }
                            catch (Win32Exception exception) when (
                                exception.NativeErrorCode == 32)
                            {
                                deleteRejected = true;
                            }
                            catch (IOException exception) when (
                                (exception.HResult & 0xffff) == 32)
                            {
                                deleteRejected = true;
                            }
                        }
                    });
            Assert(deleteAttempted && deleteRejected,
                "the retained package handle must reject post-open name deletion");
        }

        return Task.CompletedTask;
    }

    private static Task TestNativeLaunchPolicyPackageFileBoundsAndRollback()
    {
        using NativeLaunchPolicyPackageFileFixture fixture =
            CreateNativeLaunchPolicyPackageFileFixture();
        using CancellationTokenSource alreadyCancelled = new();
        alreadyCancelled.Cancel();
        AssertThrows<OperationCanceledException>(() =>
        {
            using NativeLaunchPolicyPackageFileLease ignored =
                NativeLaunchPolicyPackageFileLease.Open(
                    fixture.Directory.Path,
                    fixture.Directory.OwnerSid,
                    fixture.PackagePinSha256,
                    NewArtifactDeadline(),
                    alreadyCancelled.Token);
        });

        ManualTimeProvider expiredClock = new(DateTimeOffset.UnixEpoch);
        MonotonicDeadline expired = MonotonicDeadline.Start(
            expiredClock,
            TimeSpan.FromTicks(1));
        expiredClock.Advance(TimeSpan.FromTicks(2));
        AssertThrows<TimeoutException>(() =>
        {
            using NativeLaunchPolicyPackageFileLease ignored =
                NativeLaunchPolicyPackageFileLease.Open(
                    fixture.Directory.Path,
                    fixture.Directory.OwnerSid,
                    fixture.PackagePinSha256,
                    expired,
                    CancellationToken.None);
        });

        byte[]? cancelledSnapshot = null;
        using (CancellationTokenSource duringRead = new())
        {
            AssertThrows<OperationCanceledException>(() =>
            {
                using NativeLaunchPolicyPackageFileLease ignored =
                    NativeLaunchPolicyPackageFileLease.Open(
                        fixture.Directory.Path,
                        fixture.Directory.OwnerSid,
                        fixture.PackagePinSha256,
                        NewArtifactDeadline(),
                        duringRead.Token,
                        (stage, borrowed) =>
                        {
                            if (stage == NativeLaunchPolicyPackageFileStage
                                    .SnapshotRead)
                            {
                                cancelledSnapshot = borrowed;
                                duringRead.Cancel();
                            }
                        });
            });
        }
        Assert(cancelledSnapshot is not null && AllZero(cancelledSnapshot),
            "cancellation after snapshot read must wipe the failed snapshot");
        AssertPackageFileReleasedForWrite(fixture);
        using (NativeLaunchPolicyPackageFileLease cleanAfterCancellation =
            OpenNativeLaunchPolicyPackageFile(fixture))
        {
            cleanAfterCancellation.Revalidate(
                NewArtifactDeadline(),
                CancellationToken.None);
        }

        ManualTimeProvider lateClock = new(DateTimeOffset.UnixEpoch);
        MonotonicDeadline lateDeadline = MonotonicDeadline.Start(
            lateClock,
            TestTimeout);
        byte[]? lateSnapshot = null;
        bool reachedBeforeReturn = false;
        AssertThrows<TimeoutException>(() =>
        {
            using NativeLaunchPolicyPackageFileLease ignored =
                NativeLaunchPolicyPackageFileLease.Open(
                    fixture.Directory.Path,
                    fixture.Directory.OwnerSid,
                    fixture.PackagePinSha256,
                    lateDeadline,
                    CancellationToken.None,
                    (stage, borrowed) =>
                    {
                        if (stage == NativeLaunchPolicyPackageFileStage.SnapshotRead)
                        {
                            lateSnapshot = borrowed;
                        }

                        if (stage == NativeLaunchPolicyPackageFileStage.BeforeReturn)
                        {
                            reachedBeforeReturn = true;
                            lateClock.Advance(TestTimeout + TimeSpan.FromTicks(1));
                        }
                    });
        });
        Assert(reachedBeforeReturn,
            "the final-transfer deadline hook must run");
        Assert(lateSnapshot is not null && AllZero(lateSnapshot),
            "late final-transfer failure must leave the authentication snapshot wiped");
        AssertPackageFileReleasedForWrite(fixture);

        using NativeLaunchPolicyPackageFileLease lease =
            OpenNativeLaunchPolicyPackageFile(fixture);
        using (CancellationTokenSource cancelledRevalidation = new())
        {
            cancelledRevalidation.Cancel();
            AssertThrows<OperationCanceledException>(() => lease.Revalidate(
                NewArtifactDeadline(),
                cancelledRevalidation.Token));
        }
        AssertThrows<TimeoutException>(() => lease.Revalidate(
            expired,
            CancellationToken.None));
        lease.Revalidate(NewArtifactDeadline(), CancellationToken.None);
        lease.Dispose();
        AssertPackageFileReleasedForWrite(fixture);
        return Task.CompletedTask;
    }

    private static NativeLaunchPolicyPackageFileLease
        OpenNativeLaunchPolicyPackageFile(
            NativeLaunchPolicyPackageFileFixture fixture)
    {
        return OpenNativeLaunchPolicyPackageFile(
            fixture,
            fixture.Directory.OwnerSid);
    }

    private static NativeLaunchPolicyPackageFileLease
        OpenNativeLaunchPolicyPackageFile(
            NativeLaunchPolicyPackageFileFixture fixture,
            string expectedOwnerSid)
    {
        return NativeLaunchPolicyPackageFileLease.Open(
            fixture.Directory.Path,
            expectedOwnerSid,
            fixture.PackagePinSha256,
            NewArtifactDeadline(),
            CancellationToken.None);
    }

    private static byte[] CreateIndependentNativeLaunchPolicyPackage(
        out byte[] packagePinSha256)
    {
        byte[] release = CreateGoldenNativeReleaseManifest();
        byte[] releasePin = ComputeReleaseManifestPin(release);
        byte[] modules = CreateGoldenNativeSystemModulePolicy();
        byte[] modulePin = ComputeNativeSystemModulePolicyPin(modules);
        try
        {
            byte[] package = EncodeNativeLaunchPolicyPackage(
                GoldenNativeLaunchPolicyGeneration,
                release,
                releasePin,
                modules,
                modulePin);
            try
            {
                packagePinSha256 = ComputeNativeLaunchPolicyPackagePin(package);
                return package;
            }
            catch
            {
                WipeNativeLaunchPolicyBytes(package);
                throw;
            }
        }
        finally
        {
            WipeNativeLaunchPolicyBytes(release);
            WipeNativeLaunchPolicyBytes(releasePin);
            WipeNativeLaunchPolicyBytes(modules);
            WipeNativeLaunchPolicyBytes(modulePin);
        }
    }

    private static void AssertPackageFileWriteDeleteAndRootRenameBlocked(
        NativeLaunchPolicyPackageFileFixture fixture,
        string movedRoot)
    {
        AssertThrowsAny(
            () =>
            {
                using FileStream ignored = new(
                    fixture.PackagePath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);
            },
            typeof(IOException),
            typeof(UnauthorizedAccessException));
        AssertThrowsAny(
            () => File.Delete(fixture.PackagePath),
            typeof(IOException),
            typeof(UnauthorizedAccessException));
        AssertThrowsAny(
            () => Directory.Move(fixture.Directory.Path, movedRoot),
            typeof(IOException),
            typeof(UnauthorizedAccessException));
        Assert(File.Exists(fixture.PackagePath) &&
                Directory.Exists(fixture.Directory.Path) &&
                !Directory.Exists(movedRoot),
            "the package and guarded root must remain pinned by the lease");
    }

    private static void AssertPackageFileReleasedForWrite(
        NativeLaunchPolicyPackageFileFixture fixture)
    {
        using FileStream writer = new(
            fixture.PackagePath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete);
        using MemoryMappedFile mapping = MemoryMappedFile.CreateFromFile(
            writer,
            mapName: null,
            capacity: 0,
            MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None,
            leaveOpen: true);
        using MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
            0,
            fixture.Package.Length,
            MemoryMappedFileAccess.ReadWrite);
        byte original = view.ReadByte(0);
        byte changed = unchecked((byte)(original ^ 0xff));
        view.Write(0, changed);
        view.Flush();
        AssertEqual(changed, view.ReadByte(0),
            "released package-file writable mapping sentinel");
        view.Write(0, original);
        view.Flush();
    }

    private static void AssertPackageFileReleased(
        NativeLaunchPolicyPackageFileFixture fixture,
        ReadOnlySpan<byte> exactPackage,
        string movedRoot)
    {
        AssertPackageFileReleasedForWrite(fixture);
        File.Delete(fixture.PackagePath);
        CreateProtectedTestFile(
            fixture.PackagePath,
            fixture.Directory.OwnerSid,
            exactPackage,
            includeSystem: true);
        Directory.Move(fixture.Directory.Path, movedRoot);
        Assert(Directory.Exists(movedRoot) &&
                !Directory.Exists(fixture.Directory.Path),
            "disposing the package-file lease must release its guarded root");
        Directory.Move(movedRoot, fixture.Directory.Path);
    }

    private static NativeLaunchPolicyPackageFileFixture
        CreateNativeLaunchPolicyPackageFileFixture()
    {
        FilePublicationTestDirectory? directory = null;
        byte[]? release = null;
        byte[]? releasePin = null;
        byte[]? modules = null;
        byte[]? modulePin = null;
        byte[]? package = null;
        byte[]? packagePin = null;
        try
        {
            directory = new FilePublicationTestDirectory();
            release = CreateGoldenNativeReleaseManifest();
            releasePin = ComputeReleaseManifestPin(release);
            modules = CreateGoldenNativeSystemModulePolicy();
            modulePin = ComputeNativeSystemModulePolicyPin(modules);
            package = EncodeNativeLaunchPolicyPackage(
                GoldenNativeLaunchPolicyGeneration,
                release,
                releasePin,
                modules,
                modulePin);
            packagePin = ComputeNativeLaunchPolicyPackagePin(package);
            string packagePath = Path.Combine(
                directory.Path,
                ExpectedNativeLaunchPolicyPackageFileName);
            CreateProtectedTestFile(
                packagePath,
                directory.OwnerSid,
                package,
                includeSystem: true);

            NativeLaunchPolicyPackageFileFixture result = new(
                directory,
                packagePath,
                package,
                packagePin);
            directory = null;
            package = null;
            packagePin = null;
            return result;
        }
        finally
        {
            WipeNativeLaunchPolicyBytes(release);
            WipeNativeLaunchPolicyBytes(releasePin);
            WipeNativeLaunchPolicyBytes(modules);
            WipeNativeLaunchPolicyBytes(modulePin);
            WipeNativeLaunchPolicyBytes(package);
            WipeNativeLaunchPolicyBytes(packagePin);
            directory?.Dispose();
        }
    }

    private sealed class NativeLaunchPolicyPackageFileFixture : IDisposable
    {
        private bool disposed;

        internal NativeLaunchPolicyPackageFileFixture(
            FilePublicationTestDirectory directory,
            string packagePath,
            byte[] package,
            byte[] packagePinSha256)
        {
            Directory = directory;
            PackagePath = packagePath;
            Package = package;
            PackagePinSha256 = packagePinSha256;
        }

        internal FilePublicationTestDirectory Directory { get; }

        internal string PackagePath { get; }

        internal byte[] Package { get; }

        internal byte[] PackagePinSha256 { get; }

        internal void ReplacePackageFile(
            ReadOnlySpan<byte> bytes,
            bool includeSystem = true)
        {
            File.Delete(PackagePath);
            CreateProtectedTestFile(
                PackagePath,
                Directory.OwnerSid,
                bytes,
                includeSystem);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                Directory.Dispose();
            }
            finally
            {
                CryptographicOperations.ZeroMemory(Package);
                CryptographicOperations.ZeroMemory(PackagePinSha256);
            }
        }
    }
}
