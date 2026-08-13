using System;
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
        AssertThrows<PlatformNotSupportedException>(() =>
            NativeFixturePlatformPolicy.RequireWindows10Version1709OrLater(
                new NativeFixtureWindowsVersion(10, 0, 16_298, 2)));
        NativeFixturePlatformPolicy.RequireWindows10Version1709OrLater(
            new NativeFixtureWindowsVersion(10, 0, 16_299, 2));
        AssertThrows<PlatformNotSupportedException>(() =>
            NativeFixturePlatformPolicy.RequireWindows10Version1709OrLater(
                new NativeFixtureWindowsVersion(10, 0, 16_299, 1)));

        AssertEqual(8, IntPtr.Size, "native containment pointer size");
        AssertEqual(72, Marshal.SizeOf<NativeMethods.CreateProcessDebugInfo>(),
            "CREATE_PROCESS_DEBUG_INFO size");
        AssertEqual(160, Marshal.SizeOf<NativeMethods.DebugEventUnion>(),
            "DEBUG_EVENT union size");
        AssertEqual(176, Marshal.SizeOf<NativeMethods.DebugEvent>(),
            "DEBUG_EVENT size");
        AssertEqual(
            16,
            Marshal.OffsetOf<NativeMethods.DebugEvent>(
                nameof(NativeMethods.DebugEvent.Union)).ToInt32(),
            "DEBUG_EVENT union offset");
        return Task.CompletedTask;
    }

    private static async Task TestAuditedNativeFixtureContainmentExit()
    {
        using NativeReleaseFixture fixture = NativeReleaseFixture.Create();
        ContainedAuditedNativeFixtureProcess child = LaunchAuditedNativeFixture(
            fixture,
            ContainedNativeFixtureMode.Exit,
            NewNativeContainmentDeadline());
        try
        {
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
            AssertNativeFixtureDirectoryRenameDenied(
                fixture,
                "an exited wrapper must retain its audited namespace until disposal");
        }
        finally
        {
            await child.DisposeAsync().ConfigureAwait(false);
        }

        AssertNativeFixtureDirectoryRenameable(fixture);
    }

    private static async Task TestAuditedNativeFixtureContainmentAncestorPin()
    {
        using NativeReleaseFixture fixture = NativeReleaseFixture.Create();
        ContainedAuditedNativeFixtureProcess child = LaunchAuditedNativeFixture(
            fixture,
            ContainedNativeFixtureMode.Block,
            NewNativeContainmentDeadline());
        try
        {
            Assert(child.IsAlive(),
                "the native Block role must be alive before explicit Job closure");
            AssertNativeFixtureDirectoryRenameDenied(
                fixture,
                "the retained executable ancestor must deny rename for the wrapper lifetime");
        }
        finally
        {
            await child.DisposeAsync().ConfigureAwait(false);
        }

        AssertThrows<ObjectDisposedException>(() => child.IsAlive());
        AssertNativeFixtureDirectoryRenameable(fixture);
    }

    private static Task TestAuditedNativeFixtureContainmentFaultCleanup()
    {
        NativeContainmentFaultStage[] stages =
        {
            NativeContainmentFaultStage.AfterNamespacePinned,
            NativeContainmentFaultStage.AfterCreateProcessHandlesAdopted,
            NativeContainmentFaultStage.AfterInitialDebugEventOwned,
            NativeContainmentFaultStage.AfterExactJobVerified,
            NativeContainmentFaultStage.AfterImageFileIdentityVerified,
            NativeContainmentFaultStage.AfterDebugEventContinued,
            NativeContainmentFaultStage.AfterDebuggerDetached,
            NativeContainmentFaultStage.BeforeResume,
            NativeContainmentFaultStage.AfterResume,
        };
        foreach (NativeContainmentFaultStage stage in stages)
        {
            using NativeReleaseFixture fixture = NativeReleaseFixture.Create();
            using NativeContainmentFaultProbe faults = new(stage);
            AssertThrows<TestNativeContainmentFaultException>(() =>
                LaunchAuditedNativeFixture(
                    fixture,
                    ContainedNativeFixtureMode.Block,
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
            }

            AssertNativeFixtureDirectoryRenameable(fixture);
        }

        return Task.CompletedTask;
    }

    private static async Task TestAuditedNativeFixtureContainmentDeadlineAndDisposal()
    {
        using (NativeReleaseFixture lateFixture = NativeReleaseFixture.Create())
        using (LateNativeContainmentProbe late = new())
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
        AfterDebuggerDetached,
        BeforeResume,
        AfterResume,
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
