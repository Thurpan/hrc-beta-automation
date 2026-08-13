using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
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
        AssertTrustedNativeSystemModulePolicyRoundTrip();

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

        NativeStartupSystemModuleSetLease moduleSet =
            NativeStartupSystemModuleSetLease.OpenExpected(
                NewArtifactDeadline(),
                CancellationToken.None);
        try
        {
            AssertEqual(4, NativeStartupSystemModuleSetLease.RequiredModuleCount,
                "required startup system-module count");
            AssertEqual(0, moduleSet.CapturedCount,
                "new startup system-module captured count");
            Assert(!moduleSet.IsSealed,
                "a new startup system-module set must not be sealed");
            Assert(!moduleSet.IsEligibleForTrustedLaunch,
                "a closed local module set must not imply trusted launch");

            for (int ordinal = 0;
                 ordinal < NativeStartupSystemModuleSetLease.RequiredModuleCount;
                 ordinal++)
            {
                NativeStartupSystemModule module =
                    GetExpectedStartupSystemModule(ordinal);
                AssertEqual(module, moduleSet.GetExpectedModule(ordinal),
                    $"startup system-module ordinal {ordinal}");
                Assert(string.Equals(
                        moduleSet.GetExpectedPath(module),
                        Path.Combine(
                            Environment.SystemDirectory,
                            GetExpectedStartupSystemModuleFileName(module)),
                        StringComparison.OrdinalIgnoreCase),
                    $"startup system-module {module} native System32 path");
                Assert(moduleSet.GetExpectedLength(module) > 0,
                    $"startup system-module {module} positive length");

                byte[] digest = moduleSet.CopyExpectedSha256Digest(module);
                byte[] identifier =
                    moduleSet.CopyExpectedFileIdentifier(module);
                byte[]? freshDigest = null;
                byte[]? freshIdentifier = null;
                try
                {
                    AssertEqual(32, digest.Length,
                        $"startup system-module {module} digest length");
                    AssertEqual(16, identifier.Length,
                        $"startup system-module {module} FILE_ID length");
                    digest[0] ^= 0xff;
                    identifier[0] ^= 0xff;
                    freshDigest = moduleSet.CopyExpectedSha256Digest(module);
                    freshIdentifier =
                        moduleSet.CopyExpectedFileIdentifier(module);
                    Assert(!digest.AsSpan().SequenceEqual(freshDigest),
                        $"startup system-module {module} digest copies must be independent");
                    Assert(!identifier.AsSpan().SequenceEqual(freshIdentifier),
                        $"startup system-module {module} FILE_ID copies must be independent");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(digest);
                    CryptographicOperations.ZeroMemory(identifier);
                    if (freshDigest is not null)
                    {
                        CryptographicOperations.ZeroMemory(freshDigest);
                    }

                    if (freshIdentifier is not null)
                    {
                        CryptographicOperations.ZeroMemory(freshIdentifier);
                    }
                }
            }

            AssertExpectedStartupSystemModuleFileIdentifiersAreDistinct(
                moduleSet);
            AssertThrows<ArgumentOutOfRangeException>(() =>
                moduleSet.GetExpectedModule(-1));
            AssertThrows<ArgumentOutOfRangeException>(() =>
                moduleSet.GetExpectedModule(
                    NativeStartupSystemModuleSetLease.RequiredModuleCount));
            AssertThrows<ArgumentOutOfRangeException>(() =>
                moduleSet.GetExpectedPath((NativeStartupSystemModule)99));
            AssertThrows<InvalidOperationException>(() =>
                moduleSet.GetLoadedBaseAddress(
                    NativeStartupSystemModule.Ntdll));
            AssertThrows<InvalidOperationException>(() => moduleSet.Revalidate(
                NewArtifactDeadline(), CancellationToken.None));
        }
        finally
        {
            moduleSet.Dispose();
        }

        moduleSet.Dispose();
        AssertThrows<ObjectDisposedException>(() =>
            _ = moduleSet.CapturedCount);
        AssertThrows<ObjectDisposedException>(() => _ = moduleSet.IsSealed);
        AssertThrows<ObjectDisposedException>(() =>
            _ = moduleSet.IsEligibleForTrustedLaunch);
        AssertThrows<ObjectDisposedException>(() =>
            moduleSet.GetExpectedModule(0));
        AssertThrows<ObjectDisposedException>(() =>
            moduleSet.GetExpectedPath(NativeStartupSystemModule.Ntdll));
        AssertThrows<ObjectDisposedException>(() =>
            moduleSet.GetExpectedLength(NativeStartupSystemModule.Ntdll));
        AssertThrows<ObjectDisposedException>(() =>
            moduleSet.CopyExpectedSha256Digest(
                NativeStartupSystemModule.Ntdll));
        AssertThrows<ObjectDisposedException>(() =>
            moduleSet.CopyExpectedFileIdentifier(
                NativeStartupSystemModule.Ntdll));
        AssertThrows<ObjectDisposedException>(() =>
            moduleSet.GetLoadedBaseAddress(
                NativeStartupSystemModule.Ntdll));
        AssertThrows<ObjectDisposedException>(() => moduleSet.Revalidate(
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

        NativeStartupSystemModuleSetLease moduleSet =
            NativeStartupSystemModuleSetLease.OpenExpected(
                NewArtifactDeadline(),
                CancellationToken.None);
        nint mainImageBase = (nint)0x0001_0000;
        nint[] moduleBases =
        {
            (nint)0x0002_0000,
            (nint)0x0003_0000,
            (nint)0x0004_0000,
            (nint)0x0005_0000,
        };
        try
        {
            for (int ordinal = 0; ordinal < moduleBases.Length; ordinal++)
            {
                NativeStartupSystemModule module =
                    GetExpectedStartupSystemModule(ordinal);
                using SafeFileHandle borrowed =
                    OpenExpectedStartupSystemModule(moduleSet, module);
                NativeStartupSystemModule captured =
                    moduleSet.CaptureNextLoadedModule(
                        borrowed,
                        moduleBases[ordinal],
                        mainImageBase,
                        NewArtifactDeadline(),
                        CancellationToken.None);
                AssertEqual(module, captured,
                    $"captured startup system-module ordinal {ordinal}");
                AssertEqual(ordinal + 1, moduleSet.CapturedCount,
                    $"captured startup system-module count after {module}");
                AssertEqual(
                    moduleBases[ordinal],
                    moduleSet.GetLoadedBaseAddress(module),
                    $"captured startup system-module {module} base address");
                AssertBorrowedStartupSystemModuleHandleReadable(
                    borrowed,
                    $"borrowed {module} handle after aggregate capture");
            }

            moduleSet.SealAtInitialBreakpoint(
                NewArtifactDeadline(),
                CancellationToken.None);
            Assert(moduleSet.IsSealed,
                "the exact complete startup system-module set must seal");
            moduleSet.Revalidate(
                NewArtifactDeadline(),
                CancellationToken.None);
            AssertThrows<InvalidOperationException>(() =>
                moduleSet.SealAtInitialBreakpoint(
                    NewArtifactDeadline(),
                    CancellationToken.None));

            using SafeFileHandle postSeal = OpenExpectedStartupSystemModule(
                moduleSet,
                NativeStartupSystemModule.Ntdll);
            AssertThrows<InvalidOperationException>(() =>
                moduleSet.CaptureNextLoadedModule(
                    postSeal,
                    (nint)0x0005_0000,
                    mainImageBase,
                    NewArtifactDeadline(),
                    CancellationToken.None));
            AssertBorrowedStartupSystemModuleHandleReadable(
                postSeal,
                "borrowed handle after post-seal aggregate capture rejection");
            Assert(moduleSet.IsSealed,
                "post-seal guard failures must preserve the valid sealed set");
            moduleSet.Revalidate(
                NewArtifactDeadline(),
                CancellationToken.None);
        }
        finally
        {
            moduleSet.Dispose();
        }

        AssertThrows<ObjectDisposedException>(() =>
            moduleSet.SealAtInitialBreakpoint(
                NewArtifactDeadline(),
                CancellationToken.None));
        using SafeFileHandle afterDispose = File.OpenHandle(
            Path.Combine(Environment.SystemDirectory, "ntdll.dll"),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        AssertThrows<ObjectDisposedException>(() =>
            moduleSet.CaptureNextLoadedModule(
                afterDispose,
                (nint)0x0002_0000,
                mainImageBase,
                NewArtifactDeadline(),
                CancellationToken.None));
        AssertBorrowedStartupSystemModuleHandleReadable(
            afterDispose,
            "borrowed handle after disposed aggregate capture rejection");
        return Task.CompletedTask;
    }

    private static void AssertTerminalAggregateCaptureFailure(
        string description,
        int prefixCount,
        NativeStartupSystemModule? candidateModule,
        string? candidatePath,
        nint candidateBase,
        nint suppliedMainImageBase,
        MonotonicDeadline failureDeadline,
        CancellationToken failureCancellationToken,
        Type expectedException)
    {
        using NativeStartupSystemModuleSetLease moduleSet =
            NativeStartupSystemModuleSetLease.OpenExpected(
                NewArtifactDeadline(),
                CancellationToken.None);
        CaptureStartupSystemModulePrefix(moduleSet, prefixCount);
        string path = candidatePath ?? moduleSet.GetExpectedPath(
            candidateModule ?? throw new ArgumentException(
                "A candidate module or path is required.",
                nameof(candidateModule)));
        using SafeFileHandle borrowed = File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        AssertThrowsAny(
            () => moduleSet.CaptureNextLoadedModule(
                borrowed,
                candidateBase,
                suppliedMainImageBase,
                failureDeadline,
                failureCancellationToken),
            expectedException);
        AssertBorrowedStartupSystemModuleHandleReadable(
            borrowed,
            $"borrowed handle after {description} failure");
        AssertTerminalAggregateState(moduleSet, prefixCount, description);
    }

    private static void AssertTerminalAggregateSealFailure(
        string description,
        int prefixCount,
        MonotonicDeadline failureDeadline,
        CancellationToken failureCancellationToken,
        Type expectedException)
    {
        using NativeStartupSystemModuleSetLease moduleSet =
            NativeStartupSystemModuleSetLease.OpenExpected(
                NewArtifactDeadline(),
                CancellationToken.None);
        CaptureStartupSystemModulePrefix(moduleSet, prefixCount);
        AssertThrowsAny(
            () => moduleSet.SealAtInitialBreakpoint(
                failureDeadline,
                failureCancellationToken),
            expectedException);
        AssertTerminalAggregateState(moduleSet, prefixCount, description);
    }

    private static void AssertTerminalAggregateExtraCaptureFailure()
    {
        using NativeStartupSystemModuleSetLease moduleSet =
            NativeStartupSystemModuleSetLease.OpenExpected(
                NewArtifactDeadline(),
                CancellationToken.None);
        CaptureStartupSystemModulePrefix(
            moduleSet,
            NativeStartupSystemModuleSetLease.RequiredModuleCount);
        using SafeFileHandle borrowed = OpenExpectedStartupSystemModule(
            moduleSet,
            NativeStartupSystemModule.Ntdll);
        AssertThrows<SecurityException>(() =>
            moduleSet.CaptureNextLoadedModule(
                borrowed,
                (nint)0x0006_0000,
                (nint)0x0001_0000,
                NewArtifactDeadline(),
                CancellationToken.None));
        AssertBorrowedStartupSystemModuleHandleReadable(
            borrowed,
            "borrowed handle after extra aggregate capture failure");
        AssertTerminalAggregateState(
            moduleSet,
            NativeStartupSystemModuleSetLease.RequiredModuleCount,
            "extra capture");
    }

    private static void AssertLateTerminalAggregateCaptureFailure()
    {
        int successfulTimestampReads;
        using (NativeStartupSystemModuleSetLease calibration =
                   NativeStartupSystemModuleSetLease.OpenExpected(
                       NewArtifactDeadline(),
                       CancellationToken.None))
        using (SafeFileHandle borrowed = OpenExpectedStartupSystemModule(
                   calibration,
                   NativeStartupSystemModule.Ntdll))
        {
            CaptureTimestampTimeProvider clock = new(expireOnRead: null);
            MonotonicDeadline deadline = MonotonicDeadline.Start(
                clock,
                TestTimeout);
            AssertEqual(
                NativeStartupSystemModule.Ntdll,
                calibration.CaptureNextLoadedModule(
                    borrowed,
                    (nint)0x0002_0000,
                    (nint)0x0001_0000,
                    deadline,
                    CancellationToken.None),
                "calibrated startup module capture");
            successfulTimestampReads = clock.TimestampReads;
            Assert(successfulTimestampReads > 1,
                "calibrated capture must perform deadline checks after deadline creation");
        }

        using NativeStartupSystemModuleSetLease moduleSet =
            NativeStartupSystemModuleSetLease.OpenExpected(
                NewArtifactDeadline(),
                CancellationToken.None);
        using SafeFileHandle target = OpenExpectedStartupSystemModule(
            moduleSet,
            NativeStartupSystemModule.Ntdll);
        CaptureTimestampTimeProvider expiring = new(successfulTimestampReads);
        MonotonicDeadline expiringDeadline = MonotonicDeadline.Start(
            expiring,
            TestTimeout);
        AssertThrows<TimeoutException>(() =>
            moduleSet.CaptureNextLoadedModule(
                target,
                (nint)0x0002_0000,
                (nint)0x0001_0000,
                expiringDeadline,
                CancellationToken.None));
        AssertEqual(successfulTimestampReads, expiring.TimestampReads,
            "late capture deadline-check ordinal");
        AssertBorrowedStartupSystemModuleHandleReadable(
            target,
            "borrowed handle after late aggregate capture failure");
        AssertTerminalAggregateState(
            moduleSet,
            0,
            "late post-mutation capture rollback");
    }

    private static void CaptureStartupSystemModulePrefix(
        NativeStartupSystemModuleSetLease moduleSet,
        int count)
    {
        if (count < 0 ||
            count > NativeStartupSystemModuleSetLease.RequiredModuleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            NativeStartupSystemModule module =
                GetExpectedStartupSystemModule(ordinal);
            using SafeFileHandle borrowed =
                OpenExpectedStartupSystemModule(moduleSet, module);
            AssertEqual(
                module,
                moduleSet.CaptureNextLoadedModule(
                    borrowed,
                    GetSyntheticStartupSystemModuleBase(ordinal),
                    (nint)0x0001_0000,
                    NewArtifactDeadline(),
                    CancellationToken.None),
                $"startup system-module prefix ordinal {ordinal}");
            AssertBorrowedStartupSystemModuleHandleReadable(
                borrowed,
                $"borrowed startup prefix {module} handle");
        }
    }

    private static nint GetSyntheticStartupSystemModuleBase(int ordinal)
    {
        return ordinal switch
        {
            0 => (nint)0x0002_0000,
            1 => (nint)0x0003_0000,
            2 => (nint)0x0004_0000,
            3 => (nint)0x0005_0000,
            _ => throw new ArgumentOutOfRangeException(nameof(ordinal)),
        };
    }

    private static void AssertTerminalAggregateState(
        NativeStartupSystemModuleSetLease moduleSet,
        int expectedCount,
        string description)
    {
        AssertAggregateCaptureState(moduleSet, expectedCount, description);
        NativeStartupSystemModule candidate = expectedCount <
            NativeStartupSystemModuleSetLease.RequiredModuleCount
                ? GetExpectedStartupSystemModule(expectedCount)
                : NativeStartupSystemModule.Ntdll;
        using SafeFileHandle borrowed = OpenExpectedStartupSystemModule(
            moduleSet,
            candidate);
        AssertThrows<InvalidOperationException>(() =>
            moduleSet.CaptureNextLoadedModule(
                borrowed,
                (nint)0x0005_0000,
                (nint)0x0001_0000,
                NewArtifactDeadline(),
                CancellationToken.None));
        AssertBorrowedStartupSystemModuleHandleReadable(
            borrowed,
            $"borrowed handle after terminal {description} capture rejection");
        AssertThrows<InvalidOperationException>(() =>
            moduleSet.SealAtInitialBreakpoint(
                NewArtifactDeadline(),
                CancellationToken.None));
        AssertThrows<InvalidOperationException>(() => moduleSet.Revalidate(
            NewArtifactDeadline(), CancellationToken.None));
        AssertAggregateCaptureState(
            moduleSet,
            expectedCount,
            $"terminal {description}");
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

        AssertThrows<OperationCanceledException>(() =>
            NativeStartupSystemModuleSetLease.OpenExpected(
                NewArtifactDeadline(),
                cancelled.Token));
        AssertThrows<TimeoutException>(() =>
            NativeStartupSystemModuleSetLease.OpenExpected(
                expired,
                CancellationToken.None));

        const int MainImageBase = 0x0001_0000;
        const int NtdllBase = 0x0002_0000;
        const int Kernel32Base = 0x0003_0000;

        AssertTerminalAggregateCaptureFailure(
            "unrelated file",
            0,
            null,
            fixturePath,
            (nint)NtdllBase,
            (nint)MainImageBase,
            NewArtifactDeadline(),
            CancellationToken.None,
            typeof(SecurityException));
        AssertTerminalAggregateCaptureFailure(
            "wrong-order KERNEL32",
            0,
            NativeStartupSystemModule.Kernel32,
            null,
            (nint)NtdllBase,
            (nint)MainImageBase,
            NewArtifactDeadline(),
            CancellationToken.None,
            typeof(SecurityException));
        AssertTerminalAggregateCaptureFailure(
            "zero module base",
            0,
            NativeStartupSystemModule.Ntdll,
            null,
            0,
            (nint)MainImageBase,
            NewArtifactDeadline(),
            CancellationToken.None,
            typeof(SecurityException));
        AssertTerminalAggregateCaptureFailure(
            "zero main-image base",
            0,
            NativeStartupSystemModule.Ntdll,
            null,
            (nint)NtdllBase,
            0,
            NewArtifactDeadline(),
            CancellationToken.None,
            typeof(SecurityException));
        AssertTerminalAggregateCaptureFailure(
            "main/module base alias",
            0,
            NativeStartupSystemModule.Ntdll,
            null,
            (nint)MainImageBase,
            (nint)MainImageBase,
            NewArtifactDeadline(),
            CancellationToken.None,
            typeof(SecurityException));
        AssertTerminalAggregateCaptureFailure(
            "changed main-image base",
            1,
            NativeStartupSystemModule.Kernel32,
            null,
            (nint)Kernel32Base,
            (nint)(MainImageBase + 1),
            NewArtifactDeadline(),
            CancellationToken.None,
            typeof(SecurityException));
        AssertTerminalAggregateCaptureFailure(
            "reused NTDLL base",
            1,
            NativeStartupSystemModule.Kernel32,
            null,
            (nint)NtdllBase,
            (nint)MainImageBase,
            NewArtifactDeadline(),
            CancellationToken.None,
            typeof(SecurityException));
        AssertTerminalAggregateCaptureFailure(
            "cancelled capture",
            0,
            NativeStartupSystemModule.Ntdll,
            null,
            (nint)NtdllBase,
            (nint)MainImageBase,
            NewArtifactDeadline(),
            cancelled.Token,
            typeof(OperationCanceledException));
        AssertTerminalAggregateCaptureFailure(
            "expired capture",
            0,
            NativeStartupSystemModule.Ntdll,
            null,
            (nint)NtdllBase,
            (nint)MainImageBase,
            expired,
            CancellationToken.None,
            typeof(TimeoutException));

        AssertLateTerminalAggregateCaptureFailure();
        AssertTerminalAggregateExtraCaptureFailure();
        AssertTerminalAggregateSealFailure(
            "empty seal",
            0,
            NewArtifactDeadline(),
            CancellationToken.None,
            typeof(SecurityException));
        AssertTerminalAggregateSealFailure(
            "one-member seal",
            1,
            NewArtifactDeadline(),
            CancellationToken.None,
            typeof(SecurityException));
        AssertTerminalAggregateSealFailure(
            "two-member seal",
            2,
            NewArtifactDeadline(),
            CancellationToken.None,
            typeof(SecurityException));
        AssertTerminalAggregateSealFailure(
            "three-member seal",
            3,
            NewArtifactDeadline(),
            CancellationToken.None,
            typeof(SecurityException));
        AssertTerminalAggregateSealFailure(
            "cancelled complete seal",
            NativeStartupSystemModuleSetLease.RequiredModuleCount,
            NewArtifactDeadline(),
            cancelled.Token,
            typeof(OperationCanceledException));
        AssertTerminalAggregateSealFailure(
            "expired complete seal",
            NativeStartupSystemModuleSetLease.RequiredModuleCount,
            expired,
            CancellationToken.None,
            typeof(TimeoutException));
        AssertTrustedNativeSystemModulePolicyFailuresAndBounds();
        return Task.CompletedTask;
    }

    private static void AssertTrustedNativeSystemModulePolicyRoundTrip()
    {
        byte[] wire = CreateGoldenNativeSystemModulePolicy();
        byte[] wireBackup = (byte[])wire.Clone();
        byte[] expectedPin = Convert.FromHexString(
            "DAEEEE90E6ADC4A7C6A2DBC8068395F14E4F86596546B75F898FD07EF76E8F0F");
        byte[] pinBackup = (byte[])expectedPin.Clone();
        byte[] actualPin = ComputeNativeSystemModulePolicyPin(wire);
        TrustedNativeSystemModulePolicyV1? policy = null;
        try
        {
            AssertEqual(250, wire.Length,
                "golden native system-module policy length");
            AssertEqual(
                TrustedNativeSystemModulePolicyV1.EncodedLength,
                wire.Length,
                "native system-module policy encoded length");
            Assert(actualPin.AsSpan().SequenceEqual(expectedPin),
                "the independent golden native system-module policy pin changed");

            TrustedNativeSystemModulePolicyV1 retained = policy =
                TrustedNativeSystemModulePolicyV1.Authenticate(
                    wire,
                    expectedPin,
                    NewArtifactDeadline(),
                    CancellationToken.None);
            Assert(wire.AsSpan().SequenceEqual(wireBackup),
                "policy authentication must not mutate the caller's wire");
            Assert(expectedPin.AsSpan().SequenceEqual(pinBackup),
                "policy authentication must not mutate the caller's pin");

            CryptographicOperations.ZeroMemory(wire);
            CryptographicOperations.ZeroMemory(expectedPin);
            AssertEqual(
                TrustedNativeSystemModuleConsumerProfile.SyntheticNativeFixture,
                retained.ConsumerProfile,
                "native system-module policy consumer profile");
            AssertEqual(
                TrustedNativeSystemModulePolicyV1.Amd64Machine,
                retained.Architecture,
                "native system-module policy architecture");
            AssertEqual(
                TrustedNativeSystemModulePolicyV1.WindowsNtPlatformId,
                retained.PlatformId,
                "native system-module policy platform");
            AssertEqual(10U, retained.OperatingSystemMajorVersion,
                "native system-module policy operating-system major version");
            AssertEqual(0U, retained.OperatingSystemMinorVersion,
                "native system-module policy operating-system minor version");
            AssertEqual(19_045U, retained.ExactWindowsBuild,
                "native system-module policy exact build");
            AssertEqual(4, retained.ModuleCount,
                "native system-module policy module count");
            Assert(!retained.IsEligibleForTrustedLaunch,
                "an authenticated module policy must not imply trusted launch");

            for (int ordinal = 0;
                 ordinal < TrustedNativeSystemModulePolicyV1.RequiredModuleCount;
                 ordinal++)
            {
                NativeStartupSystemModule module =
                    GetExpectedStartupSystemModule(ordinal);
                AssertEqual(module, retained.GetExpectedModule(ordinal),
                    $"policy module ordinal {ordinal}");
                AssertEqual(
                    GetExpectedStartupSystemModuleFileName(module),
                    retained.GetExpectedFileName(module),
                    $"policy module {module} exact filename");
                AssertEqual(
                    GetGoldenNativeSystemModuleLength(module),
                    retained.GetExpectedLength(module),
                    $"policy module {module} exact length");

                byte[] expectedDigest =
                    CreateGoldenNativeSystemModuleDigest(ordinal);
                byte[] digest = retained.CopyExpectedSha256Digest(module);
                byte[]? freshDigest = null;
                try
                {
                    Assert(digest.AsSpan().SequenceEqual(expectedDigest),
                        $"policy module {module} exact SHA-256");
                    digest[0] ^= 0xff;
                    freshDigest = retained.CopyExpectedSha256Digest(module);
                    Assert(freshDigest.AsSpan().SequenceEqual(expectedDigest),
                        $"policy module {module} digest copies must be independent");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(expectedDigest);
                    CryptographicOperations.ZeroMemory(digest);
                    if (freshDigest is not null)
                    {
                        CryptographicOperations.ZeroMemory(freshDigest);
                    }
                }
            }

            AssertThrows<ArgumentOutOfRangeException>(() =>
                retained.GetExpectedModule(-1));
            AssertThrows<ArgumentOutOfRangeException>(() =>
                retained.GetExpectedModule(
                    TrustedNativeSystemModulePolicyV1.RequiredModuleCount));
            AssertThrows<ArgumentOutOfRangeException>(() =>
                retained.GetExpectedFileName(
                    (NativeStartupSystemModule)99));

            byte[] retainedPin = retained.CopyPolicyPinSha256();
            byte[]? freshPin = null;
            try
            {
                Assert(retainedPin.AsSpan().SequenceEqual(pinBackup),
                    "the policy must retain the authenticated pin after caller wipe");
                retainedPin[0] ^= 0xff;
                freshPin = retained.CopyPolicyPinSha256();
                Assert(freshPin.AsSpan().SequenceEqual(pinBackup),
                    "policy pin copies must be independent");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(retainedPin);
                if (freshPin is not null)
                {
                    CryptographicOperations.ZeroMemory(freshPin);
                }
            }

            retained.RevalidateExactHost(
                CreateGoldenNativeSystemModuleHostFacts(),
                NewArtifactDeadline(),
                CancellationToken.None);
            retained.Dispose();
            retained.Dispose();
            AssertThrows<ObjectDisposedException>(() =>
                _ = retained.ConsumerProfile);
            AssertThrows<ObjectDisposedException>(() =>
                _ = retained.Architecture);
            AssertThrows<ObjectDisposedException>(() =>
                _ = retained.PlatformId);
            AssertThrows<ObjectDisposedException>(() =>
                _ = retained.OperatingSystemMajorVersion);
            AssertThrows<ObjectDisposedException>(() =>
                _ = retained.OperatingSystemMinorVersion);
            AssertThrows<ObjectDisposedException>(() =>
                _ = retained.ExactWindowsBuild);
            AssertThrows<ObjectDisposedException>(() =>
                _ = retained.ModuleCount);
            AssertThrows<ObjectDisposedException>(() =>
                _ = retained.IsEligibleForTrustedLaunch);
            AssertThrows<ObjectDisposedException>(() =>
                retained.GetExpectedModule(0));
            AssertThrows<ObjectDisposedException>(() =>
                retained.GetExpectedFileName(
                    NativeStartupSystemModule.Ntdll));
            AssertThrows<ObjectDisposedException>(() =>
                retained.GetExpectedLength(
                    NativeStartupSystemModule.Ntdll));
            AssertThrows<ObjectDisposedException>(() =>
                retained.CopyExpectedSha256Digest(
                    NativeStartupSystemModule.Ntdll));
            AssertThrows<ObjectDisposedException>(() =>
                retained.CopyPolicyPinSha256());
            AssertThrows<ObjectDisposedException>(() =>
                retained.RevalidateExactHost(
                    CreateGoldenNativeSystemModuleHostFacts(),
                    NewArtifactDeadline(),
                    CancellationToken.None));
            AssertThrows<ObjectDisposedException>(() =>
                retained.RevalidateExactHost(
                    NewArtifactDeadline(),
                    CancellationToken.None));
        }
        finally
        {
            policy?.Dispose();
            CryptographicOperations.ZeroMemory(wire);
            CryptographicOperations.ZeroMemory(wireBackup);
            CryptographicOperations.ZeroMemory(expectedPin);
            CryptographicOperations.ZeroMemory(pinBackup);
            CryptographicOperations.ZeroMemory(actualPin);
        }
    }

    private static void AssertTrustedNativeSystemModulePolicyFailuresAndBounds()
    {
        byte[] golden = CreateGoldenNativeSystemModulePolicy();
        try
        {
            AssertNativeSystemModulePolicyAuthenticationPrecedesParsing();

            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => wire[0] ^= 0x01,
                "magic");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => BinaryPrimitives.WriteUInt16LittleEndian(
                    wire.AsSpan(8, 2), 2),
                "consumer profile");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => BinaryPrimitives.WriteUInt16LittleEndian(
                    wire.AsSpan(10, 2), 0x014c),
                "architecture");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => BinaryPrimitives.WriteUInt16LittleEndian(
                    wire.AsSpan(12, 2), 1),
                "platform");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => wire[14] = 1,
                "first reserved field");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => BinaryPrimitives.WriteUInt32LittleEndian(
                    wire.AsSpan(16, 4), 11),
                "operating-system major version");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => BinaryPrimitives.WriteUInt32LittleEndian(
                    wire.AsSpan(20, 4), 1),
                "operating-system minor version");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => BinaryPrimitives.WriteUInt32LittleEndian(
                    wire.AsSpan(24, 4), 16_298),
                "minimum operating-system build");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => BinaryPrimitives.WriteUInt32LittleEndian(
                    wire.AsSpan(28, 4), 3),
                "module count");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => wire[32] = 1,
                "second reserved field");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => BinaryPrimitives.WriteUInt16LittleEndian(
                    wire.AsSpan(36, 2), 8),
                "module filename length");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => wire[38] = (byte)'N',
                "module filename case");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => wire.AsSpan(47, 8).Clear(),
                "zero module length");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => BinaryPrimitives.WriteUInt64LittleEndian(
                    wire.AsSpan(101, 8), 0x8000_0000_0000_0000UL),
                "excessive module length");
            AssertNativeSystemModulePolicyFormatMutation(
                golden,
                static wire => wire[143] = (byte)'k',
                "later module filename case");

            AssertNativeSystemModulePolicyFormatFailure(
                ReorderGoldenNativeSystemModulePolicy(golden),
                "module order");
            AssertNativeSystemModulePolicyLengthBounds(golden);
            AssertNativeSystemModulePolicyMinimumBuildBoundary(golden);
            AssertNativeSystemModulePolicyExactHostBoundary(golden);
            AssertNativeSystemModulePolicyOperationBounds(golden);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(golden);
        }
    }

    private static byte[] CreateGoldenNativeSystemModulePolicy()
    {
        return Convert.FromHexString(
            "4852434F534D303101006486020000000A00000000000000654A00000400000000000000" +
            "09006E74646C6C2E646C6C1111000000000000000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F" +
            "0C006B65726E656C33322E646C6C2222000000000000202122232425262728292A2B2C2D2E2F303132333435363738393A3B3C3D3E3F" +
            "0E004B65726E656C426173652E646C6C3333000000000000404142434445464748494A4B4C4D4E4F505152535455565758595A5B5C5D5E5F" +
            "0B0061707068656C702E646C6C4444000000000000606162636465666768696A6B6C6D6E6F707172737475767778797A7B7C7D7E7F");
    }

    private static byte[] ComputeNativeSystemModulePolicyPin(
        ReadOnlySpan<byte> policy)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        hash.AppendData(
            "HRC-BETA-OBSERVER-NATIVE-SYSTEM-MODULE-POLICY-PIN-V1\0"u8);
        hash.AppendData(policy);
        return hash.GetHashAndReset();
    }

    private static long GetGoldenNativeSystemModuleLength(
        NativeStartupSystemModule module)
    {
        return module switch
        {
            NativeStartupSystemModule.Ntdll => 0x1111,
            NativeStartupSystemModule.Kernel32 => 0x2222,
            NativeStartupSystemModule.KernelBase => 0x3333,
            NativeStartupSystemModule.Apphelp => 0x4444,
            _ => throw new ArgumentOutOfRangeException(nameof(module)),
        };
    }

    private static byte[] CreateGoldenNativeSystemModuleDigest(int ordinal)
    {
        if ((uint)ordinal >=
            TrustedNativeSystemModulePolicyV1.RequiredModuleCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        byte[] digest = new byte[32];
        for (int index = 0; index < digest.Length; index++)
        {
            digest[index] = checked((byte)((ordinal * digest.Length) + index));
        }

        return digest;
    }

    private static NativeSystemModuleHostFacts
        CreateGoldenNativeSystemModuleHostFacts(uint build = 19_045)
    {
        return new NativeSystemModuleHostFacts(
            8,
            Architecture.X64,
            Architecture.X64,
            new NativeFixtureWindowsVersion(10, 0, build, 2));
    }

    private static void
        AssertNativeSystemModulePolicyAuthenticationPrecedesParsing()
    {
        byte[] malformed = new byte[
            TrustedNativeSystemModulePolicyV1.EncodedLength];
        byte[] malformedBackup = (byte[])malformed.Clone();
        byte[] correctPin = ComputeNativeSystemModulePolicyPin(malformed);
        byte[] correctPinBackup = (byte[])correctPin.Clone();
        byte[] wrongPin = (byte[])correctPin.Clone();
        wrongPin[0] ^= 0xff;
        byte[] wrongPinBackup = (byte[])wrongPin.Clone();
        try
        {
            AssertThrows<SecurityException>(() =>
            {
                using TrustedNativeSystemModulePolicyV1 ignored =
                    TrustedNativeSystemModulePolicyV1.Authenticate(
                        malformed,
                        wrongPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            Assert(malformed.AsSpan().SequenceEqual(malformedBackup),
                "wrong-pin rejection must not mutate the caller's malformed policy");
            Assert(wrongPin.AsSpan().SequenceEqual(wrongPinBackup),
                "wrong-pin rejection must not mutate the caller's pin");

            AssertThrows<FormatException>(() =>
            {
                using TrustedNativeSystemModulePolicyV1 ignored =
                    TrustedNativeSystemModulePolicyV1.Authenticate(
                        malformed,
                        correctPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            Assert(malformed.AsSpan().SequenceEqual(malformedBackup),
                "authenticated parse rejection must not mutate the caller's policy");
            Assert(correctPin.AsSpan().SequenceEqual(correctPinBackup),
                "authenticated parse rejection must not mutate the caller's pin");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(malformed);
            CryptographicOperations.ZeroMemory(malformedBackup);
            CryptographicOperations.ZeroMemory(correctPin);
            CryptographicOperations.ZeroMemory(correctPinBackup);
            CryptographicOperations.ZeroMemory(wrongPin);
            CryptographicOperations.ZeroMemory(wrongPinBackup);
        }
    }

    private static void AssertNativeSystemModulePolicyFormatMutation(
        ReadOnlySpan<byte> golden,
        Action<byte[]> mutation,
        string description)
    {
        byte[] malformed = golden.ToArray();
        mutation(malformed);
        AssertNativeSystemModulePolicyFormatFailure(
            malformed,
            description);
    }

    private static void AssertNativeSystemModulePolicyFormatFailure(
        byte[] malformed,
        string description)
    {
        byte[] backup = (byte[])malformed.Clone();
        byte[] pin = ComputeNativeSystemModulePolicyPin(malformed);
        byte[] pinBackup = (byte[])pin.Clone();
        try
        {
            AssertThrows<FormatException>(() =>
            {
                using TrustedNativeSystemModulePolicyV1 ignored =
                    TrustedNativeSystemModulePolicyV1.Authenticate(
                        malformed,
                        pin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            Assert(malformed.AsSpan().SequenceEqual(backup),
                $"{description} rejection must not mutate the caller's policy");
            Assert(pin.AsSpan().SequenceEqual(pinBackup),
                $"{description} rejection must not mutate the caller's pin");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(malformed);
            CryptographicOperations.ZeroMemory(backup);
            CryptographicOperations.ZeroMemory(pin);
            CryptographicOperations.ZeroMemory(pinBackup);
        }
    }

    private static byte[] ReorderGoldenNativeSystemModulePolicy(
        ReadOnlySpan<byte> golden)
    {
        byte[] reordered = new byte[
            TrustedNativeSystemModulePolicyV1.EncodedLength];
        golden[..36].CopyTo(reordered);
        int destination = 36;
        golden.Slice(87, 54).CopyTo(reordered.AsSpan(destination));
        destination += 54;
        golden.Slice(36, 51).CopyTo(reordered.AsSpan(destination));
        destination += 51;
        golden.Slice(141, 56).CopyTo(reordered.AsSpan(destination));
        destination += 56;
        golden.Slice(197, 53).CopyTo(reordered.AsSpan(destination));
        AssertEqual(reordered.Length, destination + 53,
            "reordered native system-module policy length");
        return reordered;
    }

    private static void AssertNativeSystemModulePolicyLengthBounds(
        byte[] golden)
    {
        byte[] pin = ComputeNativeSystemModulePolicyPin(golden);
        byte[] truncated = golden.AsSpan(0, golden.Length - 1).ToArray();
        byte[] trailing = new byte[golden.Length + 1];
        golden.CopyTo(trailing, 0);
        byte[] shortPin = pin.AsSpan(0, pin.Length - 1).ToArray();
        byte[] longPin = new byte[pin.Length + 1];
        pin.CopyTo(longPin, 0);
        try
        {
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedNativeSystemModulePolicyV1 ignored =
                    TrustedNativeSystemModulePolicyV1.Authenticate(
                        truncated,
                        pin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedNativeSystemModulePolicyV1 ignored =
                    TrustedNativeSystemModulePolicyV1.Authenticate(
                        trailing,
                        pin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedNativeSystemModulePolicyV1 ignored =
                    TrustedNativeSystemModulePolicyV1.Authenticate(
                        golden,
                        shortPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedNativeSystemModulePolicyV1 ignored =
                    TrustedNativeSystemModulePolicyV1.Authenticate(
                        golden,
                        longPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pin);
            CryptographicOperations.ZeroMemory(truncated);
            CryptographicOperations.ZeroMemory(trailing);
            CryptographicOperations.ZeroMemory(shortPin);
            CryptographicOperations.ZeroMemory(longPin);
        }
    }

    private static void AssertNativeSystemModulePolicyMinimumBuildBoundary(
        ReadOnlySpan<byte> golden)
    {
        byte[] minimum = golden.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(
            minimum.AsSpan(24, 4),
            TrustedNativeSystemModulePolicyV1.MinimumWindowsBuild);
        byte[] pin = ComputeNativeSystemModulePolicyPin(minimum);
        try
        {
            using TrustedNativeSystemModulePolicyV1 policy =
                TrustedNativeSystemModulePolicyV1.Authenticate(
                    minimum,
                    pin,
                    NewArtifactDeadline(),
                    CancellationToken.None);
            AssertEqual(
                TrustedNativeSystemModulePolicyV1.MinimumWindowsBuild,
                policy.ExactWindowsBuild,
                "minimum admitted native system-module policy build");
            policy.RevalidateExactHost(
                CreateGoldenNativeSystemModuleHostFacts(
                    TrustedNativeSystemModulePolicyV1.MinimumWindowsBuild),
                NewArtifactDeadline(),
                CancellationToken.None);
            AssertThrows<PlatformNotSupportedException>(() =>
                policy.RevalidateExactHost(
                    CreateGoldenNativeSystemModuleHostFacts(
                        TrustedNativeSystemModulePolicyV1.MinimumWindowsBuild +
                        1),
                    NewArtifactDeadline(),
                    CancellationToken.None));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(minimum);
            CryptographicOperations.ZeroMemory(pin);
        }
    }

    private static void AssertNativeSystemModulePolicyExactHostBoundary(
        ReadOnlySpan<byte> golden)
    {
        byte[] pin = ComputeNativeSystemModulePolicyPin(golden);
        try
        {
            using TrustedNativeSystemModulePolicyV1 policy =
                TrustedNativeSystemModulePolicyV1.Authenticate(
                    golden,
                    pin,
                    NewArtifactDeadline(),
                    CancellationToken.None);
            NativeSystemModuleHostFacts exact =
                CreateGoldenNativeSystemModuleHostFacts();
            policy.RevalidateExactHost(
                exact,
                NewArtifactDeadline(),
                CancellationToken.None);
            AssertNativeSystemModulePolicyHostMismatch(
                policy,
                exact with { PointerSize = 4 });
            AssertNativeSystemModulePolicyHostMismatch(
                policy,
                exact with { ProcessArchitecture = Architecture.X86 });
            AssertNativeSystemModulePolicyHostMismatch(
                policy,
                exact with
                {
                    OperatingSystemArchitecture = Architecture.Arm64,
                });
            AssertNativeSystemModulePolicyHostMismatch(
                policy,
                exact with
                {
                    WindowsVersion = exact.WindowsVersion with
                    {
                        PlatformId = 1,
                    },
                });
            AssertNativeSystemModulePolicyHostMismatch(
                policy,
                exact with
                {
                    WindowsVersion = exact.WindowsVersion with { Major = 11 },
                });
            AssertNativeSystemModulePolicyHostMismatch(
                policy,
                exact with
                {
                    WindowsVersion = exact.WindowsVersion with { Minor = 1 },
                });
            AssertNativeSystemModulePolicyHostMismatch(
                policy,
                exact with
                {
                    WindowsVersion = exact.WindowsVersion with
                    {
                        Build = 19_044,
                    },
                });
            AssertNativeSystemModulePolicyHostMismatch(
                policy,
                exact with
                {
                    WindowsVersion = exact.WindowsVersion with
                    {
                        Build = 19_046,
                    },
                });
            policy.RevalidateExactHost(
                exact,
                NewArtifactDeadline(),
                CancellationToken.None);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pin);
        }
    }

    private static void AssertNativeSystemModulePolicyHostMismatch(
        TrustedNativeSystemModulePolicyV1 policy,
        NativeSystemModuleHostFacts mismatch)
    {
        AssertThrows<PlatformNotSupportedException>(() =>
            policy.RevalidateExactHost(
                mismatch,
                NewArtifactDeadline(),
                CancellationToken.None));
    }

    private static void AssertNativeSystemModulePolicyOperationBounds(
        byte[] golden)
    {
        byte[] pin = ComputeNativeSystemModulePolicyPin(golden);
        byte[] goldenBackup = (byte[])golden.Clone();
        byte[] pinBackup = (byte[])pin.Clone();
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        ManualTimeProvider expiredClock = new(CanonicalTestUtcNow());
        MonotonicDeadline expired = MonotonicDeadline.Start(
            expiredClock,
            TestTimeout);
        expiredClock.Advance(TestTimeout);
        try
        {
            AssertThrows<OperationCanceledException>(() =>
            {
                using TrustedNativeSystemModulePolicyV1 ignored =
                    TrustedNativeSystemModulePolicyV1.Authenticate(
                        golden,
                        pin,
                        NewArtifactDeadline(),
                        cancelled.Token);
            });
            AssertThrows<TimeoutException>(() =>
            {
                using TrustedNativeSystemModulePolicyV1 ignored =
                    TrustedNativeSystemModulePolicyV1.Authenticate(
                        golden,
                        pin,
                        expired,
                        CancellationToken.None);
            });

            int successfulAuthenticationReads;
            CaptureTimestampTimeProvider authenticationCalibration =
                new(expireOnRead: null);
            MonotonicDeadline authenticationDeadline =
                MonotonicDeadline.Start(
                    authenticationCalibration,
                    TestTimeout);
            using (TrustedNativeSystemModulePolicyV1 ignored =
                   TrustedNativeSystemModulePolicyV1.Authenticate(
                       golden,
                       pin,
                       authenticationDeadline,
                       CancellationToken.None))
            {
                successfulAuthenticationReads =
                    authenticationCalibration.TimestampReads;
            }

            Assert(successfulAuthenticationReads > 1,
                "policy authentication must check its deadline after creation");
            CaptureTimestampTimeProvider lateAuthentication =
                new(successfulAuthenticationReads);
            MonotonicDeadline lateAuthenticationDeadline =
                MonotonicDeadline.Start(
                    lateAuthentication,
                    TestTimeout);
            AssertThrows<TimeoutException>(() =>
            {
                using TrustedNativeSystemModulePolicyV1 ignored =
                    TrustedNativeSystemModulePolicyV1.Authenticate(
                        golden,
                        pin,
                        lateAuthenticationDeadline,
                        CancellationToken.None);
            });
            AssertEqual(
                successfulAuthenticationReads,
                lateAuthentication.TimestampReads,
                "late policy authentication deadline-check ordinal");

            using TrustedNativeSystemModulePolicyV1 policy =
                TrustedNativeSystemModulePolicyV1.Authenticate(
                    golden,
                    pin,
                    NewArtifactDeadline(),
                    CancellationToken.None);
            NativeSystemModuleHostFacts exact =
                CreateGoldenNativeSystemModuleHostFacts();
            AssertThrows<OperationCanceledException>(() =>
                policy.RevalidateExactHost(
                    exact,
                    NewArtifactDeadline(),
                    cancelled.Token));
            AssertThrows<TimeoutException>(() =>
                policy.RevalidateExactHost(
                    exact,
                    expired,
                    CancellationToken.None));

            CaptureTimestampTimeProvider hostCalibration =
                new(expireOnRead: null);
            MonotonicDeadline hostDeadline = MonotonicDeadline.Start(
                hostCalibration,
                TestTimeout);
            policy.RevalidateExactHost(
                exact,
                hostDeadline,
                CancellationToken.None);
            int successfulHostReads = hostCalibration.TimestampReads;
            Assert(successfulHostReads > 1,
                "host revalidation must check its deadline after creation");
            CaptureTimestampTimeProvider lateHost =
                new(successfulHostReads);
            MonotonicDeadline lateHostDeadline = MonotonicDeadline.Start(
                lateHost,
                TestTimeout);
            AssertThrows<TimeoutException>(() =>
                policy.RevalidateExactHost(
                    exact,
                    lateHostDeadline,
                    CancellationToken.None));
            AssertEqual(
                successfulHostReads,
                lateHost.TimestampReads,
                "late host revalidation deadline-check ordinal");
            policy.RevalidateExactHost(
                exact,
                NewArtifactDeadline(),
                CancellationToken.None);

            Assert(golden.AsSpan().SequenceEqual(goldenBackup),
                "bounded policy failures must not mutate the caller's wire");
            Assert(pin.AsSpan().SequenceEqual(pinBackup),
                "bounded policy failures must not mutate the caller's pin");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pin);
            CryptographicOperations.ZeroMemory(goldenBackup);
            CryptographicOperations.ZeroMemory(pinBackup);
        }
    }

    private static NativeStartupSystemModule GetExpectedStartupSystemModule(
        int ordinal)
    {
        return ordinal switch
        {
            0 => NativeStartupSystemModule.Ntdll,
            1 => NativeStartupSystemModule.Kernel32,
            2 => NativeStartupSystemModule.KernelBase,
            3 => NativeStartupSystemModule.Apphelp,
            _ => throw new ArgumentOutOfRangeException(nameof(ordinal)),
        };
    }

    private static string GetExpectedStartupSystemModuleFileName(
        NativeStartupSystemModule module)
    {
        return module switch
        {
            NativeStartupSystemModule.Ntdll => "ntdll.dll",
            NativeStartupSystemModule.Kernel32 => "kernel32.dll",
            NativeStartupSystemModule.KernelBase => "KernelBase.dll",
            NativeStartupSystemModule.Apphelp => "apphelp.dll",
            _ => throw new ArgumentOutOfRangeException(nameof(module)),
        };
    }

    private static SafeFileHandle OpenExpectedStartupSystemModule(
        NativeStartupSystemModuleSetLease moduleSet,
        NativeStartupSystemModule module)
    {
        return File.OpenHandle(
            moduleSet.GetExpectedPath(module),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
    }

    private static void AssertBorrowedStartupSystemModuleHandleReadable(
        SafeFileHandle borrowed,
        string description)
    {
        Assert(!borrowed.IsClosed && !borrowed.IsInvalid,
            $"{description} must remain open");
        Span<byte> probe = stackalloc byte[1];
        AssertEqual(1, RandomAccess.Read(borrowed, probe, 0), description);
    }

    private static void AssertAggregateCaptureState(
        NativeStartupSystemModuleSetLease moduleSet,
        int expectedCount,
        string description)
    {
        AssertEqual(expectedCount, moduleSet.CapturedCount,
            $"{description} captured count");
        Assert(!moduleSet.IsSealed, $"{description} must remain unsealed");
        for (int ordinal = 0;
             ordinal < NativeStartupSystemModuleSetLease.RequiredModuleCount;
             ordinal++)
        {
            NativeStartupSystemModule module =
                GetExpectedStartupSystemModule(ordinal);
            if (ordinal < expectedCount)
            {
                Assert(moduleSet.GetLoadedBaseAddress(module) != 0,
                    $"{description} retained {module} base");
            }
            else
            {
                AssertThrows<InvalidOperationException>(() =>
                    moduleSet.GetLoadedBaseAddress(module));
            }
        }
    }

    private static void AssertExpectedStartupSystemModuleFileIdentifiersAreDistinct(
        NativeStartupSystemModuleSetLease moduleSet)
    {
        byte[][] identifiers = new byte[
            NativeStartupSystemModuleSetLease.RequiredModuleCount][];
        try
        {
            for (int ordinal = 0; ordinal < identifiers.Length; ordinal++)
            {
                identifiers[ordinal] = moduleSet.CopyExpectedFileIdentifier(
                    GetExpectedStartupSystemModule(ordinal));
            }

            for (int current = 0; current < identifiers.Length; current++)
            {
                for (int prior = 0; prior < current; prior++)
                {
                    Assert(!identifiers[current].AsSpan().SequenceEqual(
                            identifiers[prior]),
                        "expected startup system modules must have distinct FILE_ID values");
                }
            }
        }
        finally
        {
            for (int ordinal = 0; ordinal < identifiers.Length; ordinal++)
            {
                if (identifiers[ordinal] is not null)
                {
                    CryptographicOperations.ZeroMemory(identifiers[ordinal]);
                }
            }
        }
    }

    private sealed class CaptureTimestampTimeProvider : TimeProvider
    {
        private readonly int? expireOnRead;
        private int timestampReads;

        internal CaptureTimestampTimeProvider(int? expireOnRead)
        {
            if (expireOnRead is <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(expireOnRead));
            }

            this.expireOnRead = expireOnRead;
        }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        internal int TimestampReads => Volatile.Read(ref timestampReads);

        public override long GetTimestamp()
        {
            int read = Interlocked.Increment(ref timestampReads);
            return expireOnRead is int terminalRead && read >= terminalRead
                ? TestTimeout.Ticks
                : 0;
        }
    }
}
