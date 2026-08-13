using System;
using System.Collections.Generic;
using System.Threading;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Retains all native-fixture process authority after a fully detached debug
/// session when a bounded synchronous wait cannot prove exact process exit.
/// The exact retained process handle is the only completion signal. No PID
/// lookup, cancellation, deadline, or additional termination action is used.
/// </summary>
internal static class NativeFixtureProcessReaper
{
    private const int MaximumTerminalFailures = 32;
    private static readonly object Gate = new();
    private static readonly HashSet<Retention> Active = new();
    private static readonly Queue<Exception> TerminalFailures =
        new(MaximumTerminalFailures);
    private static long terminalFailureCount;

    internal static int Count
    {
        get
        {
            lock (Gate)
            {
                return Active.Count;
            }
        }
    }

    internal static long TerminalFailureCount =>
        Interlocked.Read(ref terminalFailureCount);

    internal static Exception[] CopyTerminalFailures()
    {
        lock (Gate)
        {
            return TerminalFailures.ToArray();
        }
    }

    internal static void Retain(
        NativeMethods.SafeJobHandle? job,
        NativeMethods.SafeProcessHandle process,
        ProcessIdentityLease? identity,
        TrustedArtifactLaunchNamespaceLease launchNamespace,
        AuditedNativeFixtureReleaseLease audit,
        NativeSystemModuleIdentityLease expectedSystemModule,
        NativeSystemModuleLoadEvidence? loadedSystemModuleEvidence)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(launchNamespace);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(expectedSystemModule);
        if (process.IsInvalid || process.IsClosed)
        {
            throw new ArgumentException(
                "The native-fixture process reaper requires one live process handle.",
                nameof(process));
        }

        Retention retention = new(
            job,
            process,
            identity,
            launchNamespace,
            audit,
            expectedSystemModule,
            loadedSystemModuleEvidence);
        lock (Gate)
        {
            // Install the emergency root synchronously before the worker can
            // run or any caller can release its local references.
            if (!Active.Add(retention))
            {
                throw new InvalidOperationException(
                    "The failed-launch retention was already registered.");
            }
        }

        try
        {
            retention.Start();
        }
        catch (Exception exception)
        {
            // The root was installed first. If worker startup itself is
            // indeterminate, retain all authority forever and expose the
            // diagnostic rather than returning it to an ordinary local path.
            RecordIndeterminate(retention, exception);
        }
    }

    private static void Complete(Retention retention, Exception? failure)
    {
        lock (Gate)
        {
            if (!Active.Remove(retention))
            {
                failure = failure is null
                    ? new InvalidOperationException(
                        "The failed-launch retention root was missing at completion.")
                    : new AggregateException(
                        "Failed-launch cleanup and registry completion both failed.",
                        failure,
                        new InvalidOperationException(
                            "The failed-launch retention root was missing at completion."));
            }

            if (failure is not null)
            {
                RecordTerminalFailureUnderLock(failure);
            }
        }
    }

    private static void RecordIndeterminate(
        Retention retention,
        Exception failure)
    {
        lock (Gate)
        {
            // Do not construct an aggregate here: this path can itself be an
            // allocation failure after Active became the sole authority root.
            // The original failure and monotonically increasing counter are
            // sufficient terminal diagnostics; Active retains the resources.
            _ = Active.Contains(retention);
            RecordTerminalFailureUnderLock(failure);
        }
    }

    private static void RecordTerminalFailureUnderLock(Exception failure)
    {
        _ = Interlocked.Increment(ref terminalFailureCount);
        while (TerminalFailures.Count >= MaximumTerminalFailures)
        {
            _ = TerminalFailures.Dequeue();
        }

        TerminalFailures.Enqueue(failure);
    }

    private sealed class Retention
    {
        private readonly NativeMethods.SafeJobHandle? job;
        private readonly NativeMethods.SafeProcessHandle process;
        private readonly ProcessIdentityLease? identity;
        private readonly TrustedArtifactLaunchNamespaceLease launchNamespace;
        private readonly AuditedNativeFixtureReleaseLease audit;
        private readonly NativeSystemModuleIdentityLease expectedSystemModule;
        private readonly NativeSystemModuleLoadEvidence?
            loadedSystemModuleEvidence;
        private readonly Exception?[] cleanupFailures = new Exception?[7];
        private int cleanupFailureCount;

        internal Retention(
            NativeMethods.SafeJobHandle? job,
            NativeMethods.SafeProcessHandle process,
            ProcessIdentityLease? identity,
            TrustedArtifactLaunchNamespaceLease launchNamespace,
            AuditedNativeFixtureReleaseLease audit,
            NativeSystemModuleIdentityLease expectedSystemModule,
            NativeSystemModuleLoadEvidence? loadedSystemModuleEvidence)
        {
            this.job = job;
            this.process = process;
            this.identity = identity;
            this.launchNamespace = launchNamespace;
            this.audit = audit;
            this.expectedSystemModule = expectedSystemModule;
            this.loadedSystemModuleEvidence = loadedSystemModuleEvidence;
        }

        internal void Start()
        {
            Thread worker = new(Run)
            {
                IsBackground = true,
                Name = "HRC native-fixture process reaper",
            };
            StartWithoutExecutionContext(worker);
        }

        private void Run()
        {
            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                RunCore();
            }
            catch (Exception exception)
            {
                RecordIndeterminate(this, exception);
            }
        }

        private static void StartWithoutExecutionContext(Thread worker)
        {
            if (ExecutionContext.IsFlowSuppressed())
            {
                worker.Start();
                return;
            }

            using (ExecutionContext.SuppressFlow())
            {
                worker.Start();
            }
        }

        private void RunCore()
        {
            Exception? failure = null;
            try
            {
                WaitForExactExit(process);
            }
            catch (Exception exception)
            {
                // A wait API failure is uncertainty, not exit. Preserve the
                // rooted authority indefinitely rather than claiming cleanup.
                RecordIndeterminate(this, exception);
                return;
            }

            Capture(identity);
            Capture(process);
            Capture(job);
            Capture(launchNamespace);
            Capture(audit);
            Capture(loadedSystemModuleEvidence);
            Capture(expectedSystemModule);
            if (cleanupFailureCount == 1)
            {
                failure = cleanupFailures[0];
            }
            else if (cleanupFailureCount > 1)
            {
                List<Exception> materialized = new(cleanupFailureCount);
                for (int index = 0; index < cleanupFailureCount; index++)
                {
                    materialized.Add(cleanupFailures[index]!);
                }

                failure = new AggregateException(
                    "Failed-launch retained cleanup had multiple failures.",
                    materialized);
            }

            Complete(this, failure);
        }

        private static void WaitForExactExit(
            NativeMethods.SafeProcessHandle process)
        {
            while (true)
            {
                uint wait = NativeMethods.WaitForSingleObject(process, 50);
                if (wait == NativeMethods.WaitObject0)
                {
                    return;
                }

                if (wait != NativeMethods.WaitTimeout)
                {
                    throw NativeMethods.Win32Failure(
                        "Failed-launch retained process wait failed");
                }

            }
        }

        private void Capture(IDisposable? resource)
        {
            try
            {
                resource?.Dispose();
            }
            catch (Exception exception)
            {
                if (cleanupFailureCount < cleanupFailures.Length)
                {
                    cleanupFailures[cleanupFailureCount++] = exception;
                }
            }
        }
    }
}
