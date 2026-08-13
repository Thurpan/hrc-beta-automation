using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class Program
{
    private static readonly TimeSpan NativeContainmentTestTimeout =
        TimeSpan.FromSeconds(15);

    private static Task TestAuditedNativeFixtureContainmentPlatform()
    {
        NativeFixtureReaperSnapshot reaperBefore = CaptureNativeFixtureReaper();
        AssertThrows<PlatformNotSupportedException>(() =>
            NativeFixturePlatformPolicy.RequireWindows10Version1709OrLater(
                new NativeFixtureWindowsVersion(10, 0, 16_298, 2)));
        NativeFixturePlatformPolicy.RequireWindows10Version1709OrLater(
            new NativeFixtureWindowsVersion(10, 0, 16_299, 2));
        AssertThrows<PlatformNotSupportedException>(() =>
            NativeFixturePlatformPolicy.RequireWindows10Version1709OrLater(
                new NativeFixtureWindowsVersion(10, 0, 16_299, 1)));

        AssertEqual(8, IntPtr.Size, "native containment pointer size");
        AssertEqual(4, NativeStartupSystemModuleSetLease.RequiredModuleCount,
            "native containment startup system-module count");
        AssertEqual(
            NativeStartupSystemModule.Ntdll,
            (NativeStartupSystemModule)0,
            "native containment NTDLL module ordinal");
        AssertEqual(
            NativeStartupSystemModule.Kernel32,
            (NativeStartupSystemModule)1,
            "native containment KERNEL32 module ordinal");
        AssertEqual(
            NativeStartupSystemModule.KernelBase,
            (NativeStartupSystemModule)2,
            "native containment KernelBase module ordinal");
        AssertEqual(
            NativeStartupSystemModule.Apphelp,
            (NativeStartupSystemModule)3,
            "native containment Apphelp module ordinal");
        AssertEqual(72, Marshal.SizeOf<NativeMethods.CreateProcessDebugInfo>(),
            "CREATE_PROCESS_DEBUG_INFO size");
        AssertEqual(0, Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
            nameof(NativeMethods.CreateProcessDebugInfo.File)).ToInt32(),
            "CREATE_PROCESS_DEBUG_INFO file offset");
        AssertEqual(8, Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
            nameof(NativeMethods.CreateProcessDebugInfo.Process)).ToInt32(),
            "CREATE_PROCESS_DEBUG_INFO process offset");
        AssertEqual(16, Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
            nameof(NativeMethods.CreateProcessDebugInfo.Thread)).ToInt32(),
            "CREATE_PROCESS_DEBUG_INFO thread offset");
        AssertEqual(24, Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
            nameof(NativeMethods.CreateProcessDebugInfo.BaseOfImage)).ToInt32(),
            "CREATE_PROCESS_DEBUG_INFO image-base offset");
        AssertEqual(152, Marshal.SizeOf<NativeMethods.ExceptionRecord>(),
            "EXCEPTION_RECORD size");
        AssertEqual(160, Marshal.SizeOf<NativeMethods.ExceptionDebugInfo>(),
            "EXCEPTION_DEBUG_INFO size");
        AssertEqual(40, Marshal.SizeOf<NativeMethods.LoadDllDebugInfo>(),
            "LOAD_DLL_DEBUG_INFO size");
        AssertEqual(160, Marshal.SizeOf<NativeMethods.DebugEventUnion>(),
            "DEBUG_EVENT union size");
        AssertEqual(176, Marshal.SizeOf<NativeMethods.DebugEvent>(),
            "DEBUG_EVENT size");
        AssertEqual(
            16,
            Marshal.OffsetOf<NativeMethods.DebugEvent>(
                nameof(NativeMethods.DebugEvent.Union)).ToInt32(),
            "DEBUG_EVENT union offset");
        AssertEqual(0, Marshal.OffsetOf<NativeMethods.ExceptionRecord>(
            nameof(NativeMethods.ExceptionRecord.ExceptionCode)).ToInt32(),
            "EXCEPTION_RECORD code offset");
        AssertEqual(4, Marshal.OffsetOf<NativeMethods.ExceptionRecord>(
            nameof(NativeMethods.ExceptionRecord.ExceptionFlags)).ToInt32(),
            "EXCEPTION_RECORD flags offset");
        AssertEqual(8, Marshal.OffsetOf<NativeMethods.ExceptionRecord>(
            nameof(NativeMethods.ExceptionRecord.NestedExceptionRecord)).ToInt32(),
            "EXCEPTION_RECORD nested-record offset");
        AssertEqual(16, Marshal.OffsetOf<NativeMethods.ExceptionRecord>(
            nameof(NativeMethods.ExceptionRecord.ExceptionAddress)).ToInt32(),
            "EXCEPTION_RECORD address offset");
        AssertEqual(24, Marshal.OffsetOf<NativeMethods.ExceptionRecord>(
            nameof(NativeMethods.ExceptionRecord.NumberParameters)).ToInt32(),
            "EXCEPTION_RECORD parameter-count offset");
        AssertEqual(32, Marshal.OffsetOf<NativeMethods.ExceptionRecord>(
            nameof(NativeMethods.ExceptionRecord.ExceptionInformation)).ToInt32(),
            "EXCEPTION_RECORD parameter-array offset");
        AssertEqual(0, Marshal.OffsetOf<NativeMethods.ExceptionDebugInfo>(
            nameof(NativeMethods.ExceptionDebugInfo.ExceptionRecord)).ToInt32(),
            "EXCEPTION_DEBUG_INFO record offset");
        AssertEqual(152, Marshal.OffsetOf<NativeMethods.ExceptionDebugInfo>(
            nameof(NativeMethods.ExceptionDebugInfo.FirstChance)).ToInt32(),
            "EXCEPTION_DEBUG_INFO first-chance offset");
        AssertEqual(0, Marshal.OffsetOf<NativeMethods.LoadDllDebugInfo>(
            nameof(NativeMethods.LoadDllDebugInfo.File)).ToInt32(),
            "LOAD_DLL_DEBUG_INFO file offset");
        AssertEqual(8, Marshal.OffsetOf<NativeMethods.LoadDllDebugInfo>(
            nameof(NativeMethods.LoadDllDebugInfo.BaseOfDll)).ToInt32(),
            "LOAD_DLL_DEBUG_INFO base offset");
        AssertEqual(16, Marshal.OffsetOf<NativeMethods.LoadDllDebugInfo>(
            nameof(NativeMethods.LoadDllDebugInfo.DebugInfoFileOffset)).ToInt32(),
            "LOAD_DLL_DEBUG_INFO debug-file offset");
        AssertEqual(20, Marshal.OffsetOf<NativeMethods.LoadDllDebugInfo>(
            nameof(NativeMethods.LoadDllDebugInfo.DebugInfoSize)).ToInt32(),
            "LOAD_DLL_DEBUG_INFO debug-size offset");
        AssertEqual(24, Marshal.OffsetOf<NativeMethods.LoadDllDebugInfo>(
            nameof(NativeMethods.LoadDllDebugInfo.ImageName)).ToInt32(),
            "LOAD_DLL_DEBUG_INFO image-name offset");
        AssertEqual(32, Marshal.OffsetOf<NativeMethods.LoadDllDebugInfo>(
            nameof(NativeMethods.LoadDllDebugInfo.Unicode)).ToInt32(),
            "LOAD_DLL_DEBUG_INFO Unicode offset");
        AssertNativeFixtureReaperUnchanged(reaperBefore);
        return Task.CompletedTask;
    }

    private static async Task TestAuditedNativeFixtureContainmentExit()
    {
        NativeFixtureReaperSnapshot reaperBefore = CaptureNativeFixtureReaper();
        using NativeReleaseFixture fixture = NativeReleaseFixture.Create();
        NativeContainmentSequenceProbe sequence = new();
        ContainedAuditedNativeFixtureProcess child = LaunchAuditedNativeFixture(
            fixture,
            ContainedNativeFixtureMode.Exit,
            NewNativeContainmentDeadline(),
            sequence);
        try
        {
            sequence.AssertExactSuccessfulOrder();
            Assert(child.ProcessId != 0 &&
                    child.ProcessId != checked((uint)Environment.ProcessId),
                "the audited native child PID must identify another process");
            AssertEqual(child.ProcessId, child.Binding.ProcessId,
                "audited native child binding PID");
            Assert(string.Equals(
                    fixture.ExecutablePath,
                    child.Binding.ImagePath,
                    StringComparison.OrdinalIgnoreCase),
                "the canonical-DOS launch must report the exact DOS image path");
            Assert(!child.IsEligibleForTrustedLaunch,
                "synthetic native containment must remain ineligible for trusted launch");
            uint exitCode = await child.WaitForExitAsync(
                    NewNativeContainmentDeadline())
                .ConfigureAwait(false);
            AssertEqual(0U, exitCode, "audited native Exit code");
            child.RevalidateStartupSystemModuleSet(
                NewNativeContainmentDeadline(),
                CancellationToken.None);
            AssertNativeFixtureDirectoryRenameDenied(
                fixture,
                "an exited wrapper must retain its audited namespace until disposal");
        }
        finally
        {
            await child.DisposeAsync().ConfigureAwait(false);
        }

        AssertNativeFixtureDirectoryRenameable(fixture);
        AssertThrows<ObjectDisposedException>(() =>
            child.RevalidateStartupSystemModuleSet(
                NewNativeContainmentDeadline(),
                CancellationToken.None));
        AssertNativeFixtureReaperUnchanged(reaperBefore);
    }

    private static async Task TestAuditedNativeFixtureContainmentAncestorPin()
    {
        NativeFixtureReaperSnapshot reaperBefore = CaptureNativeFixtureReaper();
        using NativeReleaseFixture fixture = NativeReleaseFixture.Create();
        ContainedAuditedNativeFixtureProcess child = LaunchAuditedNativeFixture(
            fixture,
            ContainedNativeFixtureMode.Block,
            NewNativeContainmentDeadline());
        try
        {
            Assert(child.IsAlive(),
                "the native Block role must be alive before explicit Job closure");
            child.RevalidateStartupSystemModuleSet(
                NewNativeContainmentDeadline(),
                CancellationToken.None);
            AssertNativeFixtureDirectoryRenameDenied(
                fixture,
                "the retained executable ancestor must deny rename for the wrapper lifetime");
        }
        finally
        {
            await child.DisposeAsync().ConfigureAwait(false);
        }

        AssertThrows<ObjectDisposedException>(() => child.IsAlive());
        AssertThrows<ObjectDisposedException>(() =>
            child.RevalidateStartupSystemModuleSet(
                NewNativeContainmentDeadline(),
                CancellationToken.None));
        AssertNativeFixtureDirectoryRenameable(fixture);
        AssertNativeFixtureReaperUnchanged(reaperBefore);
    }

    private static Task TestAuditedNativeFixtureContainmentFaultCleanup()
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
            using NativeReleaseFixture fixture = NativeReleaseFixture.Create();
            using NativeContainmentFaultProbe faults = new(stage);
            ContainedNativeFixtureMode mode =
                stage == NativeContainmentFaultStage.AfterInitialBreakpointOwned
                    ? ContainedNativeFixtureMode.Exit
                    : ContainedNativeFixtureMode.Block;
            AssertThrows<TestNativeContainmentFaultException>(() =>
                LaunchAuditedNativeFixture(
                    fixture,
                    mode,
                    NewNativeContainmentDeadline(),
                    faults));
            AssertEqual(1, faults.Calls,
                $"{stage} native containment fault call count");
            if (stage == NativeContainmentFaultStage.AfterNamespacePinned)
            {
                Assert(faults.ExactProcess is null && faults.ProcessId == 0,
                    "the namespace-only fault must precede process creation");
            }
            else
            {
                Assert(faults.ProcessId != 0 && faults.ExactProcess is not null,
                    $"{stage} must retain the exact created process object");
                AssertExactNativeProcessExited(faults.ExactProcess!);
                if (stage ==
                    NativeContainmentFaultStage.AfterInitialBreakpointOwned)
                {
                    AssertExactNativeExitRoleWasTerminated(faults.ExactProcess!);
                }
            }

            AssertNativeFixtureDirectoryRenameable(fixture);
            AssertNativeFixtureReaperUnchanged(reaperBefore);
        }

        AssertNativeFixtureReaperUnchanged(reaperBefore);
        return Task.CompletedTask;
    }

    private static async Task TestAuditedNativeFixtureContainmentDeadlineAndDisposal()
    {
        NativeFixtureReaperSnapshot reaperBefore = CaptureNativeFixtureReaper();
        using (NativeReleaseFixture preResumeFixture =
                   NativeReleaseFixture.Create())
        using (LateNativeContainmentProbe preResume = new(
                   NativeContainmentFaultStage.AfterApphelpEvidenceCaptured))
        {
            ManualTimeProvider clock = new(CanonicalTestUtcNow());
            MonotonicDeadline deadline = MonotonicDeadline.Start(
                clock,
                NativeContainmentTestTimeout);
            preResume.Clock = clock;
            AssertThrows<TimeoutException>(() => LaunchAuditedNativeFixture(
                preResumeFixture,
                ContainedNativeFixtureMode.Block,
                deadline,
                preResume));
            AssertEqual(1, preResume.Calls,
                "late pre-resume native containment probe call count");
            Assert(preResume.ExactProcess is not null,
                "the late pre-resume probe must retain the exact process object");
            AssertExactNativeProcessExited(preResume.ExactProcess!);
            AssertNativeFixtureDirectoryRenameable(preResumeFixture);
            AssertNativeFixtureReaperUnchanged(reaperBefore);
        }

        using (NativeReleaseFixture lateFixture = NativeReleaseFixture.Create())
        using (LateNativeContainmentProbe late = new(
                   NativeContainmentFaultStage.AfterResume))
        {
            ManualTimeProvider clock = new(CanonicalTestUtcNow());
            MonotonicDeadline deadline = MonotonicDeadline.Start(
                clock,
                NativeContainmentTestTimeout);
            late.Clock = clock;
            AssertThrows<TimeoutException>(() => LaunchAuditedNativeFixture(
                lateFixture,
                ContainedNativeFixtureMode.Block,
                deadline,
                late));
            AssertEqual(1, late.Calls,
                "late post-resume native containment probe call count");
            Assert(late.ExactProcess is not null,
                "the late post-resume probe must retain the exact process object");
            AssertExactNativeProcessExited(late.ExactProcess!);
            AssertNativeFixtureDirectoryRenameable(lateFixture);
            AssertNativeFixtureReaperUnchanged(reaperBefore);
        }

        using NativeReleaseFixture fixture = NativeReleaseFixture.Create();
        ContainedAuditedNativeFixtureProcess child = LaunchAuditedNativeFixture(
            fixture,
            ContainedNativeFixtureMode.Block,
            NewNativeContainmentDeadline());
        try
        {
            Assert(child.IsAlive(),
                "the native Block role must be alive before concurrent disposal");
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
                "concurrent audited native disposal must share one task");
            await Task.WhenAll(first, second).ConfigureAwait(false);
            uint exitCode = await exit.ConfigureAwait(false);
            Assert(exitCode != NativeMethods.StillActive,
                "explicit last-Job-handle closure must terminate the exact Block process");
            AssertThrows<ObjectDisposedException>(() => child.IsAlive());
        }
        finally
        {
            await child.DisposeAsync().ConfigureAwait(false);
        }

        AssertNativeFixtureDirectoryRenameable(fixture);
        AssertNativeFixtureReaperUnchanged(reaperBefore);
    }

    private static ContainedAuditedNativeFixtureProcess LaunchAuditedNativeFixture(
        NativeReleaseFixture fixture,
        ContainedNativeFixtureMode mode,
        MonotonicDeadline deadline,
        INativeFixtureContainmentTestFaults? faults = null)
    {
        return ContainedAuditedNativeFixtureProcess.OpenAndLaunch(
            fixture.Directory.Path,
            fixture.ReleaseManifest,
            fixture.ReleasePin,
            fixture.EmbeddedManifest,
            mode,
            deadline,
            CancellationToken.None,
            faults);
    }

    private static MonotonicDeadline NewNativeContainmentDeadline()
    {
        return MonotonicDeadline.Start(
            TimeProvider.System,
            NativeContainmentTestTimeout);
    }

    private static NativeFixtureReaperSnapshot CaptureNativeFixtureReaper()
    {
        return new NativeFixtureReaperSnapshot(
            NativeFixtureProcessReaper.Count,
            NativeFixtureProcessReaper.TerminalFailureCount);
    }

    private static void AssertNativeFixtureReaperUnchanged(
        NativeFixtureReaperSnapshot expected)
    {
        AssertEqual(
            expected.Count,
            NativeFixtureProcessReaper.Count,
            "native fixture process-reaper retained count");
        AssertEqual(
            expected.TerminalFailureCount,
            NativeFixtureProcessReaper.TerminalFailureCount,
            "native fixture process-reaper terminal-failure count");
    }

    private static NativeMethods.SafeProcessHandle OpenExactNativeProcess(
        uint processId)
    {
        nint raw = NativeMethods.OpenProcess(
            NativeMethods.ProcessQueryLimitedInformation |
                NativeMethods.Synchronize,
            0,
            processId);
        NativeMethods.SafeProcessHandle process = new(raw);
        if (process.IsInvalid)
        {
            process.Dispose();
            throw NativeMethods.Win32Failure(
                "Opening the exact native containment test process failed");
        }

        return process;
    }

    private static void AssertExactNativeProcessExited(
        NativeMethods.SafeProcessHandle process)
    {
        AssertEqual(
            NativeMethods.WaitObject0,
            NativeMethods.WaitForSingleObject(process, 0),
            "failed native containment exact-process terminal state");
    }

    private static void AssertExactNativeExitRoleWasTerminated(
        NativeMethods.SafeProcessHandle process)
    {
        if (NativeMethods.GetExitCodeProcess(process, out uint exitCode) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading the failed native Exit-role process code failed");
        }

        AssertEqual(
            ContainedAuditedNativeFixtureProcess.FailedLaunchExitCode,
            exitCode,
            "failed pre-entry Exit-role Job termination code");
    }

    private static void AssertNativeFixtureDirectoryRenameDenied(
        NativeReleaseFixture fixture,
        string message)
    {
        string movedPath = fixture.Directory.Path + "-moved-" +
            Guid.NewGuid().ToString("N");
        AssertThrowsAny(
            () => Directory.Move(fixture.Directory.Path, movedPath),
            typeof(IOException),
            typeof(UnauthorizedAccessException));
        Assert(Directory.Exists(fixture.Directory.Path) &&
                !Directory.Exists(movedPath),
            message);
    }

    private static void AssertNativeFixtureDirectoryRenameable(
        NativeReleaseFixture fixture)
    {
        string movedPath = fixture.Directory.Path + "-moved-" +
            Guid.NewGuid().ToString("N");
        try
        {
            Directory.Move(fixture.Directory.Path, movedPath);
            Assert(!Directory.Exists(fixture.Directory.Path) &&
                    Directory.Exists(movedPath),
                "native containment cleanup must release the retained ancestor namespace");
        }
        finally
        {
            if (Directory.Exists(movedPath) &&
                !Directory.Exists(fixture.Directory.Path))
            {
                Directory.Move(movedPath, fixture.Directory.Path);
            }
        }
    }

    private enum NativeContainmentFaultStage
    {
        AfterNamespacePinned,
        AfterCreateProcessHandlesAdopted,
        AfterInitialDebugEventOwned,
        AfterExactJobVerified,
        AfterImageFileIdentityVerified,
        AfterDebugEventContinued,
        AfterStartupLoadEventOwned,
        AfterNtdllEvidenceCaptured,
        AfterKernel32EvidenceCaptured,
        AfterKernelBaseEvidenceCaptured,
        AfterApphelpEvidenceCaptured,
        AfterInitialBreakpointOwned,
        AfterInitialBreakpointThreadSuspended,
        AfterDebuggerDetached,
        BeforeResume,
        AfterResume,
    }

    private enum NativeContainmentSequenceStage
    {
        CreateProcessEventOwned,
        StartupLoadEventOwned,
        NtdllEvidenceCaptured,
        Kernel32EvidenceCaptured,
        KernelBaseEvidenceCaptured,
        ApphelpEvidenceCaptured,
        InitialBreakpointOwned,
        InitialBreakpointThreadSuspended,
        DebuggerDetached,
        BeforeResume,
        AfterResume,
    }

    private static NativeContainmentFaultStage
        GetStartupSystemModuleCaptureFaultStage(
            NativeStartupSystemModule module)
    {
        return module switch
        {
            NativeStartupSystemModule.Ntdll =>
                NativeContainmentFaultStage.AfterNtdllEvidenceCaptured,
            NativeStartupSystemModule.Kernel32 =>
                NativeContainmentFaultStage.AfterKernel32EvidenceCaptured,
            NativeStartupSystemModule.KernelBase =>
                NativeContainmentFaultStage.AfterKernelBaseEvidenceCaptured,
            NativeStartupSystemModule.Apphelp =>
                NativeContainmentFaultStage.AfterApphelpEvidenceCaptured,
            _ => throw new ArgumentOutOfRangeException(
                nameof(module),
                module,
                "The captured startup system module is unsupported."),
        };
    }

    private readonly record struct NativeFixtureReaperSnapshot(
        int Count,
        long TerminalFailureCount);

    private sealed class NativeContainmentSequenceProbe :
        INativeFixtureContainmentTestFaults
    {
        private readonly List<NativeContainmentSequenceStage> observed = new();

        public void AfterNamespacePinned()
        {
        }

        public void AfterCreateProcessHandlesAdopted(uint processId)
        {
            _ = processId;
        }

        public void AfterInitialDebugEventOwned(uint processId)
        {
            Record(
                NativeContainmentSequenceStage.CreateProcessEventOwned,
                processId);
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

        public void AfterStartupLoadEventOwned(uint processId)
        {
            Record(
                NativeContainmentSequenceStage.StartupLoadEventOwned,
                processId);
        }

        public void AfterStartupSystemModuleCaptured(
            uint processId,
            NativeStartupSystemModule module)
        {
            Record(
                module switch
                {
                    NativeStartupSystemModule.Ntdll =>
                        NativeContainmentSequenceStage.NtdllEvidenceCaptured,
                    NativeStartupSystemModule.Kernel32 =>
                        NativeContainmentSequenceStage.Kernel32EvidenceCaptured,
                    NativeStartupSystemModule.KernelBase =>
                        NativeContainmentSequenceStage.KernelBaseEvidenceCaptured,
                    NativeStartupSystemModule.Apphelp =>
                        NativeContainmentSequenceStage.ApphelpEvidenceCaptured,
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(module),
                        module,
                        "The captured startup system module is unsupported."),
                },
                processId);
        }

        public void AfterInitialBreakpointOwned(uint processId)
        {
            Record(
                NativeContainmentSequenceStage.InitialBreakpointOwned,
                processId);
        }

        public void AfterInitialBreakpointThreadSuspended(uint processId)
        {
            Record(
                NativeContainmentSequenceStage.InitialBreakpointThreadSuspended,
                processId);
        }

        public void AfterDebuggerDetached(uint processId)
        {
            Record(NativeContainmentSequenceStage.DebuggerDetached, processId);
        }

        public void BeforeResume(uint processId)
        {
            Record(NativeContainmentSequenceStage.BeforeResume, processId);
        }

        public void AfterResume(uint processId)
        {
            Record(NativeContainmentSequenceStage.AfterResume, processId);
        }

        internal void AssertExactSuccessfulOrder()
        {
            NativeContainmentSequenceStage[] expected =
            {
                NativeContainmentSequenceStage.CreateProcessEventOwned,
                NativeContainmentSequenceStage.StartupLoadEventOwned,
                NativeContainmentSequenceStage.NtdllEvidenceCaptured,
                NativeContainmentSequenceStage.StartupLoadEventOwned,
                NativeContainmentSequenceStage.Kernel32EvidenceCaptured,
                NativeContainmentSequenceStage.StartupLoadEventOwned,
                NativeContainmentSequenceStage.KernelBaseEvidenceCaptured,
                NativeContainmentSequenceStage.StartupLoadEventOwned,
                NativeContainmentSequenceStage.ApphelpEvidenceCaptured,
                NativeContainmentSequenceStage.InitialBreakpointOwned,
                NativeContainmentSequenceStage.InitialBreakpointThreadSuspended,
                NativeContainmentSequenceStage.DebuggerDetached,
                NativeContainmentSequenceStage.BeforeResume,
                NativeContainmentSequenceStage.AfterResume,
            };
            AssertEqual(expected.Length, observed.Count,
                "successful native startup sequence length");
            for (int index = 0; index < expected.Length; index++)
            {
                AssertEqual(
                    expected[index],
                    observed[index],
                    $"successful native startup sequence stage {index}");
            }
        }

        private void Record(
            NativeContainmentSequenceStage stage,
            uint processId)
        {
            Assert(processId != 0,
                $"{stage} must identify the created native process");
            observed.Add(stage);
        }

    }

    private sealed class NativeContainmentFaultProbe :
        INativeFixtureContainmentTestFaults,
        IDisposable
    {
        private readonly NativeContainmentFaultStage target;

        internal NativeContainmentFaultProbe(NativeContainmentFaultStage target)
        {
            this.target = target;
        }

        internal int Calls { get; private set; }

        internal uint ProcessId { get; private set; }

        internal NativeMethods.SafeProcessHandle? ExactProcess { get; private set; }

        public void AfterNamespacePinned()
        {
            Visit(NativeContainmentFaultStage.AfterNamespacePinned, 0);
        }

        public void AfterCreateProcessHandlesAdopted(uint processId)
        {
            Visit(
                NativeContainmentFaultStage.AfterCreateProcessHandlesAdopted,
                processId);
        }

        public void AfterInitialDebugEventOwned(uint processId)
        {
            Visit(NativeContainmentFaultStage.AfterInitialDebugEventOwned, processId);
        }

        public void AfterExactJobVerified(uint processId)
        {
            Visit(NativeContainmentFaultStage.AfterExactJobVerified, processId);
        }

        public void AfterImageFileIdentityVerified(uint processId)
        {
            Visit(
                NativeContainmentFaultStage.AfterImageFileIdentityVerified,
                processId);
        }

        public void AfterDebugEventContinued(uint processId)
        {
            Visit(NativeContainmentFaultStage.AfterDebugEventContinued, processId);
        }

        public void AfterStartupLoadEventOwned(uint processId)
        {
            Visit(NativeContainmentFaultStage.AfterStartupLoadEventOwned, processId);
        }

        public void AfterStartupSystemModuleCaptured(
            uint processId,
            NativeStartupSystemModule module)
        {
            Visit(
                GetStartupSystemModuleCaptureFaultStage(module),
                processId);
        }

        public void AfterInitialBreakpointOwned(uint processId)
        {
            Visit(NativeContainmentFaultStage.AfterInitialBreakpointOwned, processId);
        }

        public void AfterInitialBreakpointThreadSuspended(uint processId)
        {
            Visit(
                NativeContainmentFaultStage.AfterInitialBreakpointThreadSuspended,
                processId);
        }

        public void AfterDebuggerDetached(uint processId)
        {
            Visit(NativeContainmentFaultStage.AfterDebuggerDetached, processId);
        }

        public void BeforeResume(uint processId)
        {
            Visit(NativeContainmentFaultStage.BeforeResume, processId);
        }

        public void AfterResume(uint processId)
        {
            Visit(NativeContainmentFaultStage.AfterResume, processId);
        }

        public void Dispose()
        {
            ExactProcess?.Dispose();
            ExactProcess = null;
        }

        private void Visit(NativeContainmentFaultStage stage, uint processId)
        {
            if (stage != target)
            {
                return;
            }

            Calls++;
            ProcessId = processId;
            if (processId != 0)
            {
                ExactProcess = OpenExactNativeProcess(processId);
            }

            throw new TestNativeContainmentFaultException();
        }
    }

    private sealed class LateNativeContainmentProbe :
        INativeFixtureContainmentTestFaults,
        IDisposable
    {
        private readonly NativeContainmentFaultStage target;

        internal LateNativeContainmentProbe(NativeContainmentFaultStage target)
        {
            this.target = target;
        }

        internal ManualTimeProvider? Clock { get; set; }

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

        public void AfterStartupLoadEventOwned(uint processId)
        {
            _ = processId;
        }

        public void AfterStartupSystemModuleCaptured(
            uint processId,
            NativeStartupSystemModule module)
        {
            Expire(
                GetStartupSystemModuleCaptureFaultStage(module),
                processId);
        }

        public void AfterInitialBreakpointOwned(uint processId)
        {
            _ = processId;
        }

        public void AfterInitialBreakpointThreadSuspended(uint processId)
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
            Expire(NativeContainmentFaultStage.AfterResume, processId);
        }

        private void Expire(
            NativeContainmentFaultStage stage,
            uint processId)
        {
            if (stage != target)
            {
                return;
            }

            Calls++;
            ExactProcess = OpenExactNativeProcess(processId);
            (Clock ?? throw new InvalidOperationException(
                "The late native containment clock was not configured."))
                .Advance(NativeContainmentTestTimeout);
        }

        public void Dispose()
        {
            ExactProcess?.Dispose();
            ExactProcess = null;
        }
    }

    private sealed class TestNativeContainmentFaultException : Exception
    {
    }
}
