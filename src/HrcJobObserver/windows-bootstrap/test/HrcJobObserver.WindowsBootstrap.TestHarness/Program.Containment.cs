using System;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class Program
{
    private static async Task TestContainedHarnessExit()
    {
        string executable = RequireHarnessAppHost();
        await using ContainedHarnessProcess child =
            ContainedHarnessProcess.Launch(
                executable,
                ContainedHarnessMode.Exit,
                NewContainmentDeadline(),
                CancellationToken.None);
        Assert(child.ProcessId != Environment.ProcessId,
            "the contained child PID must differ from its parent");
        AssertEqual(child.ProcessId, child.Binding.ProcessId,
            "contained child binding PID");
        Assert(child.Binding.CreationTimeFileTime != 0,
            "the contained child creation identity must be nonzero");
        uint exitCode = await child.WaitForExitAsync(
                NewContainmentDeadline())
            .ConfigureAwait(false);
        AssertEqual(0U, exitCode, "contained child exit code");
    }

    private static async Task TestContainedHarnessBlock()
    {
        string executable = RequireHarnessAppHost();
        ContainedHarnessProcess child = ContainedHarnessProcess.Launch(
            executable,
            ContainedHarnessMode.Block,
            NewContainmentDeadline(),
            CancellationToken.None);
        await WaitForContainedEntryAsync(child.ProcessId).ConfigureAwait(false);
        Assert(child.IsAlive(), "the blocking child must be alive before disposal");
        await child.DisposeAsync().ConfigureAwait(false);
        AssertThrows<ObjectDisposedException>(() => child.IsAlive());
    }

    private static Task TestContainedHarnessPreResumeFailure()
    {
        string executable = RequireHarnessAppHost();
        ThrowBeforeResume faults = new();
        AssertThrows<TestContainmentFaultException>(() =>
            ContainedHarnessProcess.Launch(
                executable,
                ContainedHarnessMode.Block,
                NewContainmentDeadline(),
                CancellationToken.None,
                faults));
        AssertEqual(1, faults.Calls, "pre-resume fault call count");
        Assert(faults.ProcessId != 0,
            "the pre-resume seam must observe the exact suspended PID");
        Assert(faults.ObservedNoEntry,
            "the suspended child must not enter managed child code before resume");

        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        AssertThrows<OperationCanceledException>(() =>
            ContainedHarnessProcess.Launch(
                executable,
                ContainedHarnessMode.Block,
                NewContainmentDeadline(),
                cancelled.Token));
        AssertThrows<ArgumentException>(() =>
            ContainedHarnessProcess.Launch(
                @"relative\harness.exe",
                ContainedHarnessMode.Block,
                NewContainmentDeadline(),
                CancellationToken.None));
        AssertThrows<ArgumentOutOfRangeException>(() =>
            ContainedHarnessProcess.Launch(
                executable,
                (ContainedHarnessMode)255,
                NewContainmentDeadline(),
                CancellationToken.None));
        return Task.CompletedTask;
    }

    private static async Task TestContainedHarnessConcurrentDisposal()
    {
        string executable = RequireHarnessAppHost();
        ContainedHarnessProcess child = ContainedHarnessProcess.Launch(
            executable,
            ContainedHarnessMode.Block,
            NewContainmentDeadline(),
            CancellationToken.None);
        await WaitForContainedEntryAsync(child.ProcessId).ConfigureAwait(false);
        Task<uint> exit = child.WaitForExitAsync(NewContainmentDeadline());
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
            "concurrent containment disposal must share one task");
        await Task.WhenAll(first, second).ConfigureAwait(false);
        uint exitCode = await exit.ConfigureAwait(false);
        Assert(exitCode != NativeMethods.StillActive,
            "the pinned exact process wait must observe terminal exit");
        await child.DisposeAsync().ConfigureAwait(false);
    }

    private static Task TestContainedHarnessLateResume()
    {
        string executable = RequireHarnessAppHost();
        ManualTimeProvider clock = new(CanonicalTestUtcNow());
        MonotonicDeadline deadline = MonotonicDeadline.Start(clock, TestTimeout);
        AdvanceAfterResume faults = new(clock);
        AssertThrows<TimeoutException>(() =>
            ContainedHarnessProcess.Launch(
                executable,
                ContainedHarnessMode.Block,
                deadline,
                CancellationToken.None,
                faults));
        AssertEqual(1, faults.Calls, "post-resume fault call count");
        return Task.CompletedTask;
    }

    private static string RequireHarnessAppHost()
    {
        string executable = Environment.ProcessPath ??
            throw new InvalidOperationException(
                "The harness executable path is unavailable.");
        if (!string.Equals(
                Path.GetExtension(executable),
                ".exe",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                Path.GetFileName(executable),
                "dotnet.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Containment tests require the generated harness apphost.");
        }

        return executable;
    }

    private static MonotonicDeadline NewContainmentDeadline()
    {
        return MonotonicDeadline.Start(TimeProvider.System, TestTimeout);
    }

    private static string ContainedEntryEventName(uint processId)
    {
        if (processId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        return ContainedEntryEventPrefix + processId.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task WaitForContainedEntryAsync(uint processId)
    {
        MonotonicDeadline deadline = NewContainmentDeadline();
        while (true)
        {
            if (EventWaitHandle.TryOpenExisting(
                    ContainedEntryEventName(processId),
                    out EventWaitHandle? entered))
            {
                using (entered)
                {
                    Assert(entered.WaitOne(0),
                        "the contained entry event must be signalled");
                    return;
                }
            }

            _ = deadline.GetRemaining();
            await Task.Yield();
        }
    }

    private sealed class ThrowBeforeResume : IContainmentTestFaults
    {
        internal int Calls { get; private set; }

        internal uint ProcessId { get; private set; }

        internal bool ObservedNoEntry { get; private set; }

        public void BeforeResume(uint processId)
        {
            Calls++;
            ProcessId = processId;
            ObservedNoEntry = !EventWaitHandle.TryOpenExisting(
                ContainedEntryEventName(processId),
                out EventWaitHandle? entered);
            entered?.Dispose();
            throw new TestContainmentFaultException();
        }

        public void AfterResume(uint processId)
        {
            _ = processId;
        }
    }

    private sealed class AdvanceAfterResume : IContainmentTestFaults
    {
        private readonly ManualTimeProvider clock;

        internal AdvanceAfterResume(ManualTimeProvider clock)
        {
            this.clock = clock;
        }

        internal int Calls { get; private set; }

        public void BeforeResume(uint processId)
        {
            _ = processId;
        }

        public void AfterResume(uint processId)
        {
            _ = processId;
            Calls++;
            clock.Advance(TestTimeout);
        }
    }

    private sealed class TestContainmentFaultException : Exception
    {
    }
}
