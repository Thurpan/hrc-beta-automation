using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class Program
{
    private static async Task TestPackageBoundNativeFixtureContainmentExit()
    {
        NativeFixtureReaperSnapshot reaperBefore = CaptureNativeFixtureReaper();
        using (NativeReleaseFixture legacyFixture = NativeReleaseFixture.Create())
        {
            ContainedAuditedNativeFixtureProcess legacy =
                LaunchAuditedNativeFixture(
                    legacyFixture,
                    ContainedNativeFixtureMode.Exit,
                    NewNativeContainmentDeadline());
            try
            {
                Assert(!legacy.IsBoundToSelectedNativeLaunchPolicyPackage,
                    "the legacy launcher must report no selected-package binding");
                AssertThrows<InvalidOperationException>(() =>
                {
                    _ = legacy.SelectedNativeLaunchPolicyGeneration;
                });
                AssertEqual(
                    0U,
                    await legacy.WaitForExitAsync(NewNativeContainmentDeadline())
                        .ConfigureAwait(false),
                    "legacy comparison Exit code");
            }
            finally
            {
                await legacy.DisposeAsync().ConfigureAwait(false);
            }
        }

        using PackageBoundNativeFixture fixture =
            PackageBoundNativeFixture.Create();
        byte[] expectedPackage = (byte[])fixture.Package.Clone();
        byte[] expectedPin = (byte[])fixture.PackagePinSha256.Clone();
        byte[] pinBackup = (byte[])fixture.PackagePinSha256.Clone();
        byte[] embeddedManifestBackup =
            (byte[])fixture.Application.EmbeddedManifest.Clone();
        byte[]? borrowedSnapshot = null;
        byte[]? firstPinCopy = null;
        byte[]? secondPinCopy = null;
        byte[]? selectorExpectedPin = null;
        byte[]? retainedCanonicalPackage = null;
        byte[]? retainedPackagePin = null;
        byte[]? retainedReleaseManifest = null;
        byte[]? retainedReleasePin = null;
        byte[]? retainedModulePolicy = null;
        byte[]? retainedModulePin = null;
        NativeLaunchPolicyPackageFileLease? retainedSelector = null;
        ContainedAuditedNativeFixtureProcess? child = null;
        string movedPackageRoot = fixture.PackageDirectory.Path + "-moved-" +
            Guid.NewGuid().ToString("N");
        try
        {
            child = LaunchPackageBoundNativeFixture(
                fixture,
                ContainedNativeFixtureMode.Exit,
                NewNativeContainmentDeadline(),
                CancellationToken.None,
                (stage, borrowed) =>
                {
                    if (stage == NativeLaunchPolicyPackageFileStage.SnapshotRead)
                    {
                        borrowedSnapshot = borrowed;
                    }
                });
            Assert(fixture.PackagePinSha256.AsSpan().SequenceEqual(pinBackup),
                "selected launch must not mutate the caller's outer package pin");
            Assert(fixture.Application.EmbeddedManifest.AsSpan().SequenceEqual(
                    embeddedManifestBackup),
                "selected launch must not mutate the caller's embedded manifest");
            Assert(borrowedSnapshot is not null && AllZero(borrowedSnapshot),
                "successful selected launch must wipe its borrowed package snapshot");

            retainedSelector = GetPrivateField(child, "selectedPackage") as
                NativeLaunchPolicyPackageFileLease ??
                throw new InvalidOperationException(
                    "The selected launcher did not retain its package lease.");
            selectorExpectedPin = GetPrivateField(
                retainedSelector,
                "expectedPackagePinSha256") as byte[] ??
                throw new InvalidOperationException(
                    "The selected package expected-pin backing was unavailable.");
            NativeLaunchPolicyPackageV1 retainedPackage = GetPrivateField(
                retainedSelector,
                "package") as NativeLaunchPolicyPackageV1 ??
                throw new InvalidOperationException(
                    "The selected package backing was unavailable.");
            retainedCanonicalPackage = GetPrivateField(
                retainedPackage,
                "canonicalPackage") as byte[] ??
                throw new InvalidOperationException(
                    "The retained canonical package backing was unavailable.");
            retainedPackagePin = GetPrivateField(
                retainedPackage,
                "packagePinSha256") as byte[] ??
                throw new InvalidOperationException(
                    "The retained package-pin backing was unavailable.");
            AuthenticatedReleaseManifestV1 retainedRelease = GetPrivateField(
                retainedPackage,
                "releaseManifest") as AuthenticatedReleaseManifestV1 ??
                throw new InvalidOperationException(
                    "The retained release-manifest backing was unavailable.");
            retainedReleaseManifest = GetPrivateField(
                retainedRelease,
                "canonicalManifest") as byte[] ??
                throw new InvalidOperationException(
                    "The retained release-manifest bytes were unavailable.");
            retainedReleasePin = GetPrivateField(
                retainedRelease,
                "manifestPinSha256") as byte[] ??
                throw new InvalidOperationException(
                    "The retained release-manifest pin was unavailable.");
            TrustedNativeSystemModulePolicyV1 retainedModules = GetPrivateField(
                retainedPackage,
                "systemModulePolicy") as TrustedNativeSystemModulePolicyV1 ??
                throw new InvalidOperationException(
                    "The retained module-policy backing was unavailable.");
            retainedModulePolicy = GetPrivateField(
                retainedModules,
                "canonicalPolicy") as byte[] ??
                throw new InvalidOperationException(
                    "The retained module-policy bytes were unavailable.");
            retainedModulePin = GetPrivateField(
                retainedModules,
                "policyPinSha256") as byte[] ??
                throw new InvalidOperationException(
                    "The retained module-policy pin was unavailable.");

            CryptographicOperations.ZeroMemory(fixture.Package);
            CryptographicOperations.ZeroMemory(fixture.PackagePinSha256);
            CryptographicOperations.ZeroMemory(
                fixture.Application.EmbeddedManifest);
            Assert(child.IsBoundToSelectedNativeLaunchPolicyPackage,
                "the selected launcher must report its retained package binding");
            AssertEqual(
                GoldenNativeLaunchPolicyGeneration,
                child.SelectedNativeLaunchPolicyGeneration,
                "selected native launch-policy generation");
            Assert(!child.IsEligibleForTrustedLaunch,
                "package-bound synthetic containment must remain ineligible for trusted launch");
            firstPinCopy = child.CopySelectedNativeLaunchPolicyPackagePinSha256();
            Assert(firstPinCopy.AsSpan().SequenceEqual(expectedPin),
                "the selected wrapper must retain the exact outer package pin");
            firstPinCopy[0] ^= 0xff;
            secondPinCopy = child.CopySelectedNativeLaunchPolicyPackagePinSha256();
            Assert(secondPinCopy.AsSpan().SequenceEqual(expectedPin),
                "selected outer-package pin copies must be independent");
            child.RevalidateSelectedNativeLaunchPolicyPackageBinding(
                NewNativeContainmentDeadline(),
                CancellationToken.None);
            AssertEqual(
                0U,
                await child.WaitForExitAsync(NewNativeContainmentDeadline())
                    .ConfigureAwait(false),
                "package-bound native Exit code");
            AssertPackageAuthorityBlocked(
                fixture.PackagePath,
                fixture.PackageDirectory.Path,
                movedPackageRoot);
            AssertNativeFixtureDirectoryRenameDenied(
                fixture.Application,
                "an exited package-bound wrapper must retain its application namespace");
        }
        finally
        {
            if (child is not null)
            {
                await child.DisposeAsync().ConfigureAwait(false);
            }

            WipeNativeLaunchPolicyBytes(firstPinCopy);
            WipeNativeLaunchPolicyBytes(secondPinCopy);
            WipeNativeLaunchPolicyBytes(pinBackup);
            WipeNativeLaunchPolicyBytes(embeddedManifestBackup);
        }

        Assert(retainedSelector is not null &&
                selectorExpectedPin is not null && AllZero(selectorExpectedPin) &&
                retainedCanonicalPackage is not null &&
                    AllZero(retainedCanonicalPackage) &&
                retainedPackagePin is not null && AllZero(retainedPackagePin) &&
                retainedReleaseManifest is not null &&
                    AllZero(retainedReleaseManifest) &&
                retainedReleasePin is not null && AllZero(retainedReleasePin) &&
                retainedModulePolicy is not null && AllZero(retainedModulePolicy) &&
                retainedModulePin is not null && AllZero(retainedModulePin),
            "wrapper disposal must wipe each directly captured selected-package canonical-byte and pin backing");
        AssertThrows<ObjectDisposedException>(() => retainedSelector!.Revalidate(
            NewNativeContainmentDeadline(),
            CancellationToken.None));
        AssertThrows<ObjectDisposedException>(() =>
            child!.RevalidateSelectedNativeLaunchPolicyPackageBinding(
                NewNativeContainmentDeadline(),
                CancellationToken.None));
        AssertPackageAuthorityReleased(
            fixture.PackagePath,
            fixture.PackageDirectory,
            expectedPackage);
        AssertNativeFixtureDirectoryRenameable(fixture.Application);
        AssertNativeFixtureReaperUnchanged(reaperBefore);
        WipeNativeLaunchPolicyBytes(expectedPackage);
        WipeNativeLaunchPolicyBytes(expectedPin);
    }

    private static Task TestPackageBoundNativeFixtureContainmentPreCreateRejection()
    {
        NativeFixtureReaperSnapshot reaperBefore = CaptureNativeFixtureReaper();
        using (PackageBoundNativeFixture sameIdentity =
                   PackageBoundNativeFixture.Create())
        {
            using NativeLaunchPolicyPackageFileLease selector =
                NativeLaunchPolicyPackageFileLease.Open(
                    sameIdentity.PackageDirectory.Path,
                    sameIdentity.PackageDirectory.OwnerSid,
                    sameIdentity.PackagePinSha256,
                    NewNativeContainmentDeadline(),
                    CancellationToken.None);
            AssertThrows<SecurityException>(() =>
                selector.RequireDistinctApplicationDirectory(
                    sameIdentity.PackageDirectory.Path,
                    NewNativeContainmentDeadline(),
                    CancellationToken.None));
            selector.Revalidate(
                NewNativeContainmentDeadline(),
                CancellationToken.None);
            selector.Dispose();
            AssertPackageAuthorityReleasedForWrite(sameIdentity.PackagePath);
            AssertPackageRootRenameable(sameIdentity.PackageDirectory);
        }

        using (PackageBoundNativeFixture wrongPinFixture =
                   PackageBoundNativeFixture.Create())
        {
            byte[] wrongPin = (byte[])wrongPinFixture.PackagePinSha256.Clone();
            wrongPin[0] ^= 0xff;
            try
            {
                AssertPackageBoundPreCreateFailure<SecurityException>(
                    wrongPinFixture,
                    wrongPin,
                    reaperBefore,
                    "wrong outer package pin");
            }
            finally
            {
                WipeNativeLaunchPolicyBytes(wrongPin);
            }
        }

        using (PackageBoundNativeFixture releaseMismatch =
                   PackageBoundNativeFixture.Create(
                       PackageBoundNativeFixture.CreateReleaseDigestMismatch))
        {
            AssertPackageBoundPreCreateFailure<SecurityException>(
                releaseMismatch,
                releaseMismatch.PackagePinSha256,
                reaperBefore,
                "package-bound release mismatch");
        }

        using (PackageBoundNativeFixture moduleMismatch =
                   PackageBoundNativeFixture.Create(
                       modulePolicyTransform:
                           PackageBoundNativeFixture.CreateModuleDigestMismatch))
        {
            AssertPackageBoundPreCreateFailure<SecurityException>(
                moduleMismatch,
                moduleMismatch.PackagePinSha256,
                reaperBefore,
                "package-bound module-policy mismatch");
        }

        using (PackageBoundNativeFixture collision =
                   PackageBoundNativeFixture.Create())
        {
            string collocatedPackagePath = Path.Combine(
                collision.Application.Directory.Path,
                ExpectedNativeLaunchPolicyPackageFileName);
            CreateProtectedTestFile(
                collocatedPackagePath,
                collision.Application.Directory.OwnerSid,
                collision.Package,
                includeSystem: true);
            byte[] pinBackup = (byte[])collision.PackagePinSha256.Clone();
            byte[] embeddedBackup =
                (byte[])collision.Application.EmbeddedManifest.Clone();
            byte[]? borrowedSnapshot = null;
            using NativeContainmentFaultProbe createSentinel = new(
                NativeContainmentFaultStage.AfterCreateProcessHandlesAdopted);
            try
            {
                AssertThrows<SecurityException>(() =>
                    ContainedAuditedNativeFixtureProcess
                        .OpenSelectedPackageAndLaunch(
                            collision.Application.Directory.Path,
                            collision.Application.Directory.OwnerSid,
                            collision.PackagePinSha256,
                            collision.Application.Directory.Path,
                            collision.Application.EmbeddedManifest,
                            ContainedNativeFixtureMode.Block,
                            NewNativeContainmentDeadline(),
                            CancellationToken.None,
                            (stage, borrowed) =>
                            {
                                if (stage == NativeLaunchPolicyPackageFileStage
                                        .SnapshotRead)
                                {
                                    borrowedSnapshot = borrowed;
                                }
                            },
                            createSentinel));
                Assert(collision.PackagePinSha256.AsSpan().SequenceEqual(
                        pinBackup) &&
                        collision.Application.EmbeddedManifest.AsSpan()
                            .SequenceEqual(embeddedBackup),
                    "directory-collision failure must not mutate caller inputs");
                Assert(borrowedSnapshot is not null && AllZero(borrowedSnapshot),
                    "directory-collision failure must wipe its package snapshot");
                AssertEqual(0, createSentinel.Calls,
                    "package/application directory collision must precede CreateProcess");
                AssertNativeFixtureReaperUnchanged(reaperBefore);
            }
            finally
            {
                WipeNativeLaunchPolicyBytes(pinBackup);
                WipeNativeLaunchPolicyBytes(embeddedBackup);
                if (File.Exists(collocatedPackagePath))
                {
                    File.Delete(collocatedPackagePath);
                }
            }

            AssertNativeFixtureDirectoryRenameable(collision.Application);
        }

        AssertNativeFixtureReaperUnchanged(reaperBefore);
        return Task.CompletedTask;
    }

    private static Task TestPackageBoundNativeFixtureContainmentFaultCleanup()
    {
        NativeFixtureReaperSnapshot reaperBefore = CaptureNativeFixtureReaper();
        NativeContainmentFaultStage[] stages =
        {
            NativeContainmentFaultStage.AfterNamespacePinned,
            NativeContainmentFaultStage.AfterCreateProcessHandlesAdopted,
            NativeContainmentFaultStage.AfterInitialDebugEventOwned,
            NativeContainmentFaultStage.AfterExactJobVerified,
            NativeContainmentFaultStage.AfterImageFileIdentityVerified,
            NativeContainmentFaultStage.AfterDebugEventContinued,
            NativeContainmentFaultStage.AfterStartupLoadEventOwned,
            NativeContainmentFaultStage.AfterNtdllEvidenceCaptured,
            NativeContainmentFaultStage.AfterKernel32EvidenceCaptured,
            NativeContainmentFaultStage.AfterKernelBaseEvidenceCaptured,
            NativeContainmentFaultStage.AfterApphelpEvidenceCaptured,
            NativeContainmentFaultStage.AfterInitialBreakpointOwned,
            NativeContainmentFaultStage.AfterInitialBreakpointThreadSuspended,
            NativeContainmentFaultStage.AfterDebuggerDetached,
            NativeContainmentFaultStage.BeforeResume,
            NativeContainmentFaultStage.AfterResume,
        };
        foreach (NativeContainmentFaultStage stage in stages)
        {
            using PackageBoundNativeFixture fixture =
                PackageBoundNativeFixture.Create();
            using NativeContainmentFaultProbe faults = new(stage);
            byte[] expectedPackage = (byte[])fixture.Package.Clone();
            byte[]? borrowedSnapshot = null;
            try
            {
                ContainedNativeFixtureMode mode = stage ==
                    NativeContainmentFaultStage.AfterInitialBreakpointOwned
                        ? ContainedNativeFixtureMode.Exit
                        : ContainedNativeFixtureMode.Block;
                AssertThrows<TestNativeContainmentFaultException>(() =>
                    LaunchPackageBoundNativeFixture(
                        fixture,
                        mode,
                        NewNativeContainmentDeadline(),
                        CancellationToken.None,
                        (packageStage, borrowed) =>
                        {
                            if (packageStage ==
                                NativeLaunchPolicyPackageFileStage.SnapshotRead)
                            {
                                borrowedSnapshot = borrowed;
                            }
                        },
                        faults));
                AssertEqual(1, faults.Calls,
                    $"{stage} package-bound containment fault call count");
                Assert(borrowedSnapshot is not null &&
                        AllZero(borrowedSnapshot),
                    $"{stage} package-bound failure must wipe its selector snapshot");
                Assert(File.ReadAllBytes(fixture.PackagePath).AsSpan()
                        .SequenceEqual(expectedPackage),
                    $"{stage} package-bound failure must preserve the selected package file");
                if (stage == NativeContainmentFaultStage.AfterNamespacePinned)
                {
                    Assert(faults.ExactProcess is null && faults.ProcessId == 0,
                        "the package-bound namespace-only fault must precede process creation");
                }
                else
                {
                    Assert(faults.ProcessId != 0 && faults.ExactProcess is not null,
                        $"{stage} must retain the exact package-bound process object");
                    AssertExactNativeProcessExited(faults.ExactProcess!);
                    if (stage ==
                        NativeContainmentFaultStage.AfterInitialBreakpointOwned)
                    {
                        AssertExactNativeExitRoleWasTerminated(
                            faults.ExactProcess!);
                    }
                }

                AssertPackageAuthorityReleased(
                    fixture.PackagePath,
                    fixture.PackageDirectory,
                    expectedPackage);
                AssertNativeFixtureDirectoryRenameable(fixture.Application);
                AssertNativeFixtureReaperUnchanged(reaperBefore);
            }
            finally
            {
                WipeNativeLaunchPolicyBytes(expectedPackage);
            }
        }

        AssertNativeFixtureReaperUnchanged(reaperBefore);
        return Task.CompletedTask;
    }

    private static async Task
        TestPackageBoundNativeFixtureContainmentBoundsAndDisposal()
    {
        NativeFixtureReaperSnapshot reaperBefore = CaptureNativeFixtureReaper();
        using (PackageBoundNativeFixture cancelledBeforeSelection =
                   PackageBoundNativeFixture.Create())
        using (CancellationTokenSource alreadyCancelled = new())
        {
            alreadyCancelled.Cancel();
            AssertThrows<OperationCanceledException>(() =>
                LaunchPackageBoundNativeFixture(
                    cancelledBeforeSelection,
                    ContainedNativeFixtureMode.Block,
                    NewNativeContainmentDeadline(),
                    alreadyCancelled.Token));
            AssertPackageAuthorityReleasedForWrite(
                cancelledBeforeSelection.PackagePath);
            AssertPackageRootRenameable(
                cancelledBeforeSelection.PackageDirectory);
            AssertNativeFixtureDirectoryRenameable(
                cancelledBeforeSelection.Application);
            AssertNativeFixtureReaperUnchanged(reaperBefore);
        }

        using (PackageBoundNativeFixture cancelledDuringSelection =
                   PackageBoundNativeFixture.Create())
        using (CancellationTokenSource cancellation = new())
        {
            byte[]? cancelledSnapshot = null;
            AssertThrows<OperationCanceledException>(() =>
                LaunchPackageBoundNativeFixture(
                    cancelledDuringSelection,
                    ContainedNativeFixtureMode.Block,
                    NewNativeContainmentDeadline(),
                    cancellation.Token,
                    (stage, borrowed) =>
                    {
                        if (stage ==
                            NativeLaunchPolicyPackageFileStage.SnapshotRead)
                        {
                            cancelledSnapshot = borrowed;
                            cancellation.Cancel();
                        }
                    }));
            Assert(cancelledSnapshot is not null && AllZero(cancelledSnapshot),
                "selection cancellation must wipe its borrowed package snapshot");
            AssertPackageAuthorityReleasedForWrite(
                cancelledDuringSelection.PackagePath);
            AssertPackageRootRenameable(
                cancelledDuringSelection.PackageDirectory);
            AssertNativeFixtureDirectoryRenameable(
                cancelledDuringSelection.Application);
            AssertNativeFixtureReaperUnchanged(reaperBefore);
        }

        using (PackageBoundNativeFixture lateSelection =
                   PackageBoundNativeFixture.Create())
        using (NativeContainmentFaultProbe createSentinel = new(
                   NativeContainmentFaultStage.AfterCreateProcessHandlesAdopted))
        {
            ManualTimeProvider clock = new(CanonicalTestUtcNow());
            MonotonicDeadline deadline = MonotonicDeadline.Start(
                clock,
                NativeContainmentTestTimeout);
            byte[]? lateSnapshot = null;
            bool reachedBeforeReturn = false;
            AssertThrows<TimeoutException>(() =>
                LaunchPackageBoundNativeFixture(
                    lateSelection,
                    ContainedNativeFixtureMode.Block,
                    deadline,
                    CancellationToken.None,
                    (stage, borrowed) =>
                    {
                        if (stage ==
                            NativeLaunchPolicyPackageFileStage.SnapshotRead)
                        {
                            lateSnapshot = borrowed;
                        }

                        if (stage ==
                            NativeLaunchPolicyPackageFileStage.BeforeReturn)
                        {
                            reachedBeforeReturn = true;
                            clock.Advance(
                                NativeContainmentTestTimeout +
                                TimeSpan.FromTicks(1));
                        }
                    },
                    createSentinel));
            Assert(reachedBeforeReturn && createSentinel.Calls == 0,
                "a late selector return must fail before CreateProcess");
            Assert(lateSnapshot is not null && AllZero(lateSnapshot),
                "a late selector return must wipe its package snapshot");
            AssertPackageAuthorityReleasedForWrite(lateSelection.PackagePath);
            AssertPackageRootRenameable(lateSelection.PackageDirectory);
            AssertNativeFixtureDirectoryRenameable(lateSelection.Application);
            AssertNativeFixtureReaperUnchanged(reaperBefore);
        }

        using (PackageBoundNativeFixture preResumeFixture =
                   PackageBoundNativeFixture.Create())
        using (LateNativeContainmentProbe preResume = new(
                   NativeContainmentFaultStage.AfterApphelpEvidenceCaptured))
        {
            ManualTimeProvider clock = new(CanonicalTestUtcNow());
            MonotonicDeadline deadline = MonotonicDeadline.Start(
                clock,
                NativeContainmentTestTimeout);
            preResume.Clock = clock;
            byte[] expectedPackage = (byte[])preResumeFixture.Package.Clone();
            try
            {
                AssertThrows<TimeoutException>(() =>
                    LaunchPackageBoundNativeFixture(
                        preResumeFixture,
                        ContainedNativeFixtureMode.Block,
                        deadline,
                        CancellationToken.None,
                        packageTestHook: null,
                        faults: preResume));
                AssertEqual(1, preResume.Calls,
                    "late package-bound pre-resume probe call count");
                Assert(preResume.ExactProcess is not null,
                    "the late package-bound pre-resume probe must retain the exact process");
                AssertExactNativeProcessExited(preResume.ExactProcess!);
                AssertPackageAuthorityReleased(
                    preResumeFixture.PackagePath,
                    preResumeFixture.PackageDirectory,
                    expectedPackage);
                AssertNativeFixtureDirectoryRenameable(
                    preResumeFixture.Application);
                AssertNativeFixtureReaperUnchanged(reaperBefore);
            }
            finally
            {
                WipeNativeLaunchPolicyBytes(expectedPackage);
            }
        }

        using (PackageBoundNativeFixture cancelledAfterResume =
                   PackageBoundNativeFixture.Create())
        using (CancellationTokenSource cancellation = new())
        using (CancelPackageBoundAfterResumeProbe lateCancellation =
                   new(cancellation))
        {
            byte[] expectedPackage =
                (byte[])cancelledAfterResume.Package.Clone();
            try
            {
                AssertThrows<OperationCanceledException>(() =>
                    LaunchPackageBoundNativeFixture(
                        cancelledAfterResume,
                        ContainedNativeFixtureMode.Block,
                        NewNativeContainmentDeadline(),
                        cancellation.Token,
                        packageTestHook: null,
                        faults: lateCancellation));
                AssertEqual(1, lateCancellation.Calls,
                    "late package-bound cancellation probe call count");
                Assert(lateCancellation.ExactProcess is not null,
                    "late package-bound cancellation must retain the exact process");
                AssertExactNativeProcessExited(lateCancellation.ExactProcess!);
                AssertPackageAuthorityReleased(
                    cancelledAfterResume.PackagePath,
                    cancelledAfterResume.PackageDirectory,
                    expectedPackage);
                AssertNativeFixtureDirectoryRenameable(
                    cancelledAfterResume.Application);
                AssertNativeFixtureReaperUnchanged(reaperBefore);
            }
            finally
            {
                WipeNativeLaunchPolicyBytes(expectedPackage);
            }
        }

        using PackageBoundNativeFixture fixture =
            PackageBoundNativeFixture.Create();
        byte[] retainedPackage = (byte[])fixture.Package.Clone();
        ContainedAuditedNativeFixtureProcess child =
            LaunchPackageBoundNativeFixture(
                fixture,
                ContainedNativeFixtureMode.Block,
                NewNativeContainmentDeadline(),
                CancellationToken.None);
        string movedPackageRoot = fixture.PackageDirectory.Path + "-moved-" +
            Guid.NewGuid().ToString("N");
        try
        {
            Assert(child.IsAlive(),
                "the package-bound Block role must be alive before concurrent disposal");
            Assert(child.IsBoundToSelectedNativeLaunchPolicyPackage,
                "the package-bound Block role must retain its selected package");
            AssertPackageAuthorityBlocked(
                fixture.PackagePath,
                fixture.PackageDirectory.Path,
                movedPackageRoot);
            Task<uint> exit = child.WaitForExitAsync(
                NewNativeContainmentDeadline());
            using Barrier barrier = new(2);
            Task<Task> firstCall = Task.Factory.StartNew(
                () =>
                {
                    barrier.SignalAndWait();
                    return child.DisposeAsync().AsTask();
                },
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
            Task<Task> secondCall = Task.Factory.StartNew(
                () =>
                {
                    barrier.SignalAndWait();
                    return child.DisposeAsync().AsTask();
                },
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
            Task first = await firstCall.ConfigureAwait(false);
            Task second = await secondCall.ConfigureAwait(false);
            Assert(ReferenceEquals(first, second),
                "concurrent package-bound disposal must share one task");
            await Task.WhenAll(first, second).ConfigureAwait(false);
            uint exitCode = await exit.ConfigureAwait(false);
            Assert(exitCode != NativeMethods.StillActive,
                "package-bound disposal must terminate the exact Block process");
            AssertThrows<ObjectDisposedException>(() => child.IsAlive());
            AssertThrows<ObjectDisposedException>(() =>
                child.RevalidateSelectedNativeLaunchPolicyPackageBinding(
                    NewNativeContainmentDeadline(),
                    CancellationToken.None));
        }
        finally
        {
            await child.DisposeAsync().ConfigureAwait(false);
        }

        AssertPackageAuthorityReleased(
            fixture.PackagePath,
            fixture.PackageDirectory,
            retainedPackage);
        AssertNativeFixtureDirectoryRenameable(fixture.Application);
        AssertNativeFixtureReaperUnchanged(reaperBefore);
        WipeNativeLaunchPolicyBytes(retainedPackage);
    }

    private static ContainedAuditedNativeFixtureProcess
        LaunchPackageBoundNativeFixture(
            PackageBoundNativeFixture fixture,
            ContainedNativeFixtureMode mode,
            MonotonicDeadline deadline,
            CancellationToken cancellationToken,
            Action<NativeLaunchPolicyPackageFileStage, byte[]?>?
                packageTestHook = null,
            INativeFixtureContainmentTestFaults? faults = null)
    {
        return ContainedAuditedNativeFixtureProcess
            .OpenSelectedPackageAndLaunch(
                fixture.PackageDirectory.Path,
                fixture.PackageDirectory.OwnerSid,
                fixture.PackagePinSha256,
                fixture.Application.Directory.Path,
                fixture.Application.EmbeddedManifest,
                mode,
                deadline,
                cancellationToken,
                packageTestHook,
                faults);
    }

    private static void AssertPackageAuthorityBlocked(
        string packagePath,
        string packageRoot,
        string movedPackageRoot)
    {
        AssertThrowsAny(
            () =>
            {
                using FileStream ignored = new(
                    packagePath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);
            },
            typeof(IOException),
            typeof(UnauthorizedAccessException));
        AssertThrowsAny(
            () => File.Delete(packagePath),
            typeof(IOException),
            typeof(UnauthorizedAccessException));
        AssertThrowsAny(
            () => Directory.Move(packageRoot, movedPackageRoot),
            typeof(IOException),
            typeof(UnauthorizedAccessException));
        Assert(File.Exists(packagePath) && Directory.Exists(packageRoot) &&
                !Directory.Exists(movedPackageRoot),
            "the package-bound wrapper must retain its file and guarded root authority");
    }

    private static void AssertPackageAuthorityReleased(
        string packagePath,
        FilePublicationTestDirectory packageDirectory,
        ReadOnlySpan<byte> exactPackage)
    {
        AssertPackageAuthorityReleasedForWrite(packagePath);
        File.Delete(packagePath);
        CreateProtectedTestFile(
            packagePath,
            packageDirectory.OwnerSid,
            exactPackage,
            includeSystem: true);
        AssertPackageRootRenameable(packageDirectory);
    }

    private static void AssertPackageAuthorityReleasedForWrite(
        string packagePath)
    {
        using FileStream writer = new(
            packagePath,
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
            writer.Length,
            MemoryMappedFileAccess.ReadWrite);
        byte original = view.ReadByte(0);
        byte changed = unchecked((byte)(original ^ 0xff));
        view.Write(0, changed);
        view.Flush();
        AssertEqual(changed, view.ReadByte(0),
            "released package-bound writable mapping sentinel");
        view.Write(0, original);
        view.Flush();
    }

    private static void AssertPackageRootRenameable(
        FilePublicationTestDirectory packageDirectory)
    {
        string movedPath = packageDirectory.Path + "-moved-" +
            Guid.NewGuid().ToString("N");
        try
        {
            Directory.Move(packageDirectory.Path, movedPath);
            Assert(!Directory.Exists(packageDirectory.Path) &&
                    Directory.Exists(movedPath),
                "package-bound cleanup must release its retained root namespace");
        }
        finally
        {
            if (Directory.Exists(movedPath) &&
                !Directory.Exists(packageDirectory.Path))
            {
                Directory.Move(movedPath, packageDirectory.Path);
            }
        }
    }

    private static void AssertPackageBoundPreCreateFailure<TException>(
        PackageBoundNativeFixture fixture,
        byte[] packagePin,
        NativeFixtureReaperSnapshot reaperBefore,
        string description)
        where TException : Exception
    {
        byte[] packageBackup = (byte[])fixture.Package.Clone();
        byte[] pinBackup = (byte[])packagePin.Clone();
        byte[] embeddedBackup =
            (byte[])fixture.Application.EmbeddedManifest.Clone();
        byte[]? borrowedSnapshot = null;
        using NativeContainmentFaultProbe createSentinel = new(
            NativeContainmentFaultStage.AfterCreateProcessHandlesAdopted);
        try
        {
            AssertThrows<TException>(() =>
                ContainedAuditedNativeFixtureProcess.OpenSelectedPackageAndLaunch(
                    fixture.PackageDirectory.Path,
                    fixture.PackageDirectory.OwnerSid,
                    packagePin,
                    fixture.Application.Directory.Path,
                    fixture.Application.EmbeddedManifest,
                    ContainedNativeFixtureMode.Block,
                    NewNativeContainmentDeadline(),
                    CancellationToken.None,
                    (stage, borrowed) =>
                    {
                        if (stage == NativeLaunchPolicyPackageFileStage.SnapshotRead)
                        {
                            borrowedSnapshot = borrowed;
                        }
                    },
                    createSentinel));
            Assert(fixture.Package.AsSpan().SequenceEqual(packageBackup),
                $"{description} must not mutate the caller's package encoder buffer");
            Assert(packagePin.AsSpan().SequenceEqual(pinBackup),
                $"{description} must not mutate the caller's package pin");
            Assert(fixture.Application.EmbeddedManifest.AsSpan().SequenceEqual(
                    embeddedBackup),
                $"{description} must not mutate the caller's embedded manifest");
            Assert(borrowedSnapshot is not null && AllZero(borrowedSnapshot),
                $"{description} must wipe its borrowed package snapshot");
            AssertEqual(0, createSentinel.Calls,
                $"{description} must precede CreateProcess");
            Assert(createSentinel.ExactProcess is null &&
                    createSentinel.ProcessId == 0,
                $"{description} must retain no process authority");
            AssertPackageAuthorityReleasedForWrite(fixture.PackagePath);
            AssertPackageRootRenameable(fixture.PackageDirectory);
            AssertNativeFixtureDirectoryRenameable(fixture.Application);
            AssertNativeFixtureReaperUnchanged(reaperBefore);
        }
        finally
        {
            WipeNativeLaunchPolicyBytes(packageBackup);
            WipeNativeLaunchPolicyBytes(pinBackup);
            WipeNativeLaunchPolicyBytes(embeddedBackup);
        }
    }

    private sealed class CancelPackageBoundAfterResumeProbe :
        INativeFixtureContainmentTestFaults,
        IDisposable
    {
        private readonly CancellationTokenSource cancellation;

        internal CancelPackageBoundAfterResumeProbe(
            CancellationTokenSource cancellation)
        {
            this.cancellation = cancellation;
        }

        internal int Calls { get; private set; }

        internal NativeMethods.SafeProcessHandle? ExactProcess { get; private set; }

        public void AfterNamespacePinned()
        {
        }

        public void AfterCreateProcessHandlesAdopted(uint processId)
        {
            _ = processId;
        }

        public void AfterInitialDebugEventOwned(uint processId)
        {
            _ = processId;
        }

        public void AfterExactJobVerified(uint processId)
        {
            _ = processId;
        }

        public void AfterImageFileIdentityVerified(uint processId)
        {
            _ = processId;
        }

        public void AfterDebugEventContinued(uint processId)
        {
            _ = processId;
        }

        public void AfterDebuggerDetached(uint processId)
        {
            _ = processId;
        }

        public void BeforeResume(uint processId)
        {
            _ = processId;
        }

        public void AfterResume(uint processId)
        {
            Calls++;
            ExactProcess = OpenExactNativeProcess(processId);
            cancellation.Cancel();
        }

        public void Dispose()
        {
            ExactProcess?.Dispose();
            ExactProcess = null;
        }
    }


    private sealed class PackageBoundNativeFixture : IDisposable
    {
        private bool disposed;

        private PackageBoundNativeFixture(
            NativeReleaseFixture application,
            FilePublicationTestDirectory packageDirectory,
            string packagePath,
            byte[] package,
            byte[] packagePinSha256)
        {
            Application = application;
            PackageDirectory = packageDirectory;
            PackagePath = packagePath;
            Package = package;
            PackagePinSha256 = packagePinSha256;
        }

        internal NativeReleaseFixture Application { get; }

        internal FilePublicationTestDirectory PackageDirectory { get; }

        internal string PackagePath { get; }

        internal byte[] Package { get; }

        internal byte[] PackagePinSha256 { get; }

        internal static PackageBoundNativeFixture Create(
            Func<byte[], byte[]>? releaseManifestTransform = null,
            Func<byte[], byte[]>? modulePolicyTransform = null)
        {
            NativeReleaseFixture? application = null;
            FilePublicationTestDirectory? packageDirectory = null;
            CurrentHostNativeSystemModulePolicyInputs? modulePolicy = null;
            byte[]? releaseManifest = null;
            byte[]? releasePin = null;
            byte[]? selectedModulePolicy = null;
            byte[]? selectedModulePin = null;
            byte[]? package = null;
            byte[]? packagePin = null;
            try
            {
                application = NativeReleaseFixture.Create();
                packageDirectory = new FilePublicationTestDirectory();
                modulePolicy = CurrentHostNativeSystemModulePolicyInputs.Create();
                releaseManifest = releaseManifestTransform is null
                    ? (byte[])application.ReleaseManifest.Clone()
                    : releaseManifestTransform(application.ReleaseManifest);
                releasePin = ComputeReleaseManifestPin(releaseManifest);
                selectedModulePolicy = modulePolicyTransform is null
                    ? (byte[])modulePolicy.Policy.Clone()
                    : modulePolicyTransform(modulePolicy.Policy);
                selectedModulePin =
                    ComputeNativeSystemModulePolicyPin(selectedModulePolicy);
                package = EncodeNativeLaunchPolicyPackage(
                    GoldenNativeLaunchPolicyGeneration,
                    releaseManifest,
                    releasePin,
                    selectedModulePolicy,
                    selectedModulePin);
                packagePin = ComputeNativeLaunchPolicyPackagePin(package);
                string packagePath = Path.Combine(
                    packageDirectory.Path,
                    ExpectedNativeLaunchPolicyPackageFileName);
                CreateProtectedTestFile(
                    packagePath,
                    packageDirectory.OwnerSid,
                    package,
                    includeSystem: true);

                PackageBoundNativeFixture result = new(
                    application,
                    packageDirectory,
                    packagePath,
                    package,
                    packagePin);
                application = null;
                packageDirectory = null;
                package = null;
                packagePin = null;
                return result;
            }
            finally
            {
                modulePolicy?.Dispose();
                application?.Dispose();
                packageDirectory?.Dispose();
                WipeNativeLaunchPolicyBytes(releaseManifest);
                WipeNativeLaunchPolicyBytes(releasePin);
                WipeNativeLaunchPolicyBytes(selectedModulePolicy);
                WipeNativeLaunchPolicyBytes(selectedModulePin);
                WipeNativeLaunchPolicyBytes(package);
                WipeNativeLaunchPolicyBytes(packagePin);
            }
        }

        internal static byte[] CreateReleaseDigestMismatch(
            byte[] canonicalReleaseManifest)
        {
            byte[] mismatch = (byte[])canonicalReleaseManifest.Clone();
            int executableNameLength = BinaryPrimitives.ReadUInt16BigEndian(
                mismatch.AsSpan(16, sizeof(ushort)));
            int artifactCountOffset = checked(18 + executableNameLength);
            int artifactNameLengthOffset = checked(artifactCountOffset + 4);
            int artifactNameLength = BinaryPrimitives.ReadUInt16BigEndian(
                mismatch.AsSpan(artifactNameLengthOffset, sizeof(ushort)));
            int artifactDigestOffset = checked(
                artifactNameLengthOffset + 2 + artifactNameLength + 8);
            mismatch[artifactDigestOffset] ^= 0xff;
            return mismatch;
        }

        internal static byte[] CreateModuleDigestMismatch(
            byte[] canonicalModulePolicy)
        {
            byte[] mismatch = (byte[])canonicalModulePolicy.Clone();
            mismatch[218] ^= 0xff;
            return mismatch;
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
                PackageDirectory.Dispose();
            }
            finally
            {
                try
                {
                    Application.Dispose();
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(Package);
                    CryptographicOperations.ZeroMemory(PackagePinSha256);
                }
            }
        }
    }
}
