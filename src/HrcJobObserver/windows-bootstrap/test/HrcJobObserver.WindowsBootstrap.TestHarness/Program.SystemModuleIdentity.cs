using System;
using System.IO;
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
        return Task.CompletedTask;
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
