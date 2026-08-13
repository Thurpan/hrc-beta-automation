using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal enum ContainedNativeFixtureMode
{
    Exit = 1,
    Block = 2,
}

/// <summary>
/// Test-only fault points inside the closed native-fixture launch transaction.
/// Hooks receive no handles, paths, or authenticated artifact material.
/// </summary>
internal interface INativeFixtureContainmentTestFaults
{
    void AfterNamespacePinned();

    void AfterCreateProcessHandlesAdopted(uint processId);

    void AfterInitialDebugEventOwned(uint processId);

    void AfterExactJobVerified(uint processId);

    void AfterImageFileIdentityVerified(uint processId);

    void AfterDebugEventContinued(uint processId);

    void AfterDebuggerDetached(uint processId);

    void BeforeResume(uint processId);

    void AfterResume(uint processId);
}

/// <summary>
/// Opens and owns one audited synthetic native-fixture release, pins its full
/// local namespace, and launches only its fixed Exit or Block mode through a
/// kill-on-close Job Object assigned at process creation. The executable is
/// launched through the canonical DOS path under a retained DOS/volume-GUID
/// namespace binding. A synchronous initial debug event supplies the loader's
/// image-file handle, which is
/// authenticated directly against the retained file identity before detach and
/// the exact initial-thread resume. This remains synthetic containment
/// evidence, not release provenance, section-object identity, System32 trust,
/// or production launch eligibility.
/// </summary>
internal sealed class ContainedAuditedNativeFixtureProcess : IAsyncDisposable
{
    private const int MinimumReleaseManifestLength = 98;
    private const int Sha256Length = 32;
    private const string ExitArgument = "--native-exit";
    private const string BlockArgument = "--native-block";
    private readonly object gate = new();
    private NativeMethods.SafeJobHandle? job;
    private NativeMethods.SafeProcessHandle? process;
    private ProcessIdentityLease? identity;
    private TrustedArtifactLaunchNamespaceLease? launchNamespace;
    private AuditedNativeFixtureReleaseLease? auditedRelease;
    private readonly CleanupFailureLedger disposalFailures;
    private readonly Stopwatch disposalStopwatch;
    private Task? disposalTask;

    private ContainedAuditedNativeFixtureProcess(
        NativeMethods.SafeJobHandle job,
        NativeMethods.SafeProcessHandle process,
        ProcessIdentityLease identity,
        TrustedArtifactLaunchNamespaceLease launchNamespace,
        AuditedNativeFixtureReleaseLease auditedRelease,
        BootstrapBinding binding)
    {
        // Allocate every disposal-only resource before this object accepts the
        // live authority below. A constructor allocation failure therefore
        // leaves the launch transaction responsible for cleanup.
        disposalFailures = new CleanupFailureLedger();
        disposalStopwatch = new Stopwatch();
        this.job = job;
        this.process = process;
        this.identity = identity;
        this.launchNamespace = launchNamespace;
        this.auditedRelease = auditedRelease;
        ProcessId = binding.ProcessId;
        Binding = binding;
    }

    internal uint ProcessId { get; }

    internal BootstrapBinding Binding { get; }

    internal bool IsEligibleForTrustedLaunch => false;

    internal static ContainedAuditedNativeFixtureProcess OpenAndLaunch(
        string exactApplicationDirectory,
        ReadOnlySpan<byte> canonicalReleaseManifest,
        ReadOnlySpan<byte> expectedReleaseManifestPinSha256,
        ReadOnlySpan<byte> exactEmbeddedApplicationManifest,
        ContainedNativeFixtureMode mode,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        return OpenAndLaunch(
            exactApplicationDirectory,
            canonicalReleaseManifest,
            expectedReleaseManifestPinSha256,
            exactEmbeddedApplicationManifest,
            mode,
            deadline,
            cancellationToken,
            testFaults: null);
    }

    internal static ContainedAuditedNativeFixtureProcess OpenAndLaunch(
        string exactApplicationDirectory,
        ReadOnlySpan<byte> canonicalReleaseManifest,
        ReadOnlySpan<byte> expectedReleaseManifestPinSha256,
        ReadOnlySpan<byte> exactEmbeddedApplicationManifest,
        ContainedNativeFixtureMode mode,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken,
        INativeFixtureContainmentTestFaults? testFaults)
    {
        // Cleanup must not need to allocate after CreateProcessW has created a
        // debuggee. Allocate its bounded ledger and timer before even opening
        // the audited release so every acquired authority has a no-allocation
        // unwind path.
        CleanupFailureLedger cleanupFailures = new();
        Stopwatch cleanupStopwatch = new();
        ContainedHarnessProcess.CheckOperation(deadline, cancellationToken);
        if (canonicalReleaseManifest.Length is < MinimumReleaseManifestLength or
            > ReleaseManifestV1.MaximumEncodedLength)
        {
            throw new ArgumentException(
                "The release manifest byte length is invalid.",
                nameof(canonicalReleaseManifest));
        }

        if (expectedReleaseManifestPinSha256.Length != Sha256Length)
        {
            throw new ArgumentException(
                "The expected release-manifest pin must contain exactly 32 bytes.",
                nameof(expectedReleaseManifestPinSha256));
        }

        if (exactEmbeddedApplicationManifest.Length !=
            NativeFixturePeAudit.ExactManifestLength)
        {
            throw new ArgumentException(
                "The native fixture embedded manifest must have its exact bounded length.",
                nameof(exactEmbeddedApplicationManifest));
        }

        _ = mode switch
        {
            ContainedNativeFixtureMode.Exit => true,
            ContainedNativeFixtureMode.Block => true,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        byte[]? ownedManifest = null;
        byte[]? ownedPin = null;
        byte[]? ownedEmbeddedManifest = null;
        try
        {
            ownedManifest = canonicalReleaseManifest.ToArray();
            ownedPin = expectedReleaseManifestPinSha256.ToArray();
            ownedEmbeddedManifest = exactEmbeddedApplicationManifest.ToArray();
            byte[] launchManifest = ownedManifest;
            byte[] launchPin = ownedPin;
            byte[] launchEmbeddedManifest = ownedEmbeddedManifest;
            LaunchThreadResult holder = new();
            Thread worker = new(() =>
            {
                try
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                    holder.Result = OpenAndLaunchCore(
                        exactApplicationDirectory,
                        launchManifest,
                        launchPin,
                        launchEmbeddedManifest,
                        mode,
                        deadline,
                        cancellationToken,
                        testFaults,
                        cleanupStopwatch,
                        cleanupFailures);
                }
                catch (Exception exception)
                {
                    holder.Failure = ExceptionDispatchInfo.Capture(exception);
                }
            })
            {
                IsBackground = true,
                Name = "HRC audited native-fixture launch",
            };
            StartWithoutExecutionContext(worker);
            JoinNonAbandonably(worker);
            holder.Failure?.Throw();
            return holder.Result ?? throw new InvalidOperationException(
                "The dedicated native-fixture launch thread returned no result.");
        }
        finally
        {
            if (ownedManifest is not null)
            {
                CryptographicOperations.ZeroMemory(ownedManifest);
            }

            if (ownedPin is not null)
            {
                CryptographicOperations.ZeroMemory(ownedPin);
            }

            if (ownedEmbeddedManifest is not null)
            {
                CryptographicOperations.ZeroMemory(ownedEmbeddedManifest);
            }
        }
    }

    private static unsafe ContainedAuditedNativeFixtureProcess OpenAndLaunchCore(
        string exactApplicationDirectory,
        ReadOnlySpan<byte> canonicalReleaseManifest,
        ReadOnlySpan<byte> expectedReleaseManifestPinSha256,
        ReadOnlySpan<byte> exactEmbeddedApplicationManifest,
        ContainedNativeFixtureMode mode,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken,
        INativeFixtureContainmentTestFaults? testFaults,
        Stopwatch cleanupStopwatch,
        CleanupFailureLedger cleanupFailures)
    {
        ContainedHarnessProcess.CheckOperation(deadline, cancellationToken);
        NativeFixturePlatformPolicy.RequireWindows10Version1709OrLater();
        RequireExactDebugAbi();
        ContainedHarnessProcess.CheckOperation(deadline, cancellationToken);
        string argument = mode switch
        {
            ContainedNativeFixtureMode.Exit => ExitArgument,
            ContainedNativeFixtureMode.Block => BlockArgument,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        AuditedNativeFixtureReleaseLease? audit = null;
        TrustedArtifactLaunchNamespaceLease? namespaceLease = null;
        NativeMethods.SafeJobHandle? job = null;
        NativeMethods.SafeProcessHandle? process = null;
        NativeMethods.SafeThreadHandle? thread = null;
        ProcessIdentityLease? identity = null;
        nint attributeList = 0;
        nint jobValue = 0;
        nint debugImageFile = 0;
        bool attributeListInitialized = false;
        bool debugAttached = false;
        bool debugEventOutstanding = false;
        uint debugEventProcessId = 0;
        uint debugEventThreadId = 0;
        uint debugEventCode = 0;
        uint createdProcessId = 0;
        try
        {
            audit = AuditedNativeFixtureReleaseLease.Open(
                exactApplicationDirectory,
                canonicalReleaseManifest,
                expectedReleaseManifestPinSha256,
                exactEmbeddedApplicationManifest,
                deadline,
                cancellationToken);
            if (audit.IsEligibleForTrustedLaunch)
            {
                throw new SecurityException(
                    "The synthetic audited release crossed its launch-eligibility boundary.");
            }

            namespaceLease = audit.OpenLaunchNamespaceLease(
                deadline,
                cancellationToken);
            RequireExactAuditedPaths(audit, namespaceLease);
            RevalidatePreResume(
                audit,
                namespaceLease,
                deadline,
                cancellationToken);
            testFaults?.AfterNamespacePinned();
            RevalidatePreResume(
                audit,
                namespaceLease,
                deadline,
                cancellationToken);

            job = ContainedHarnessProcess.CreateConfiguredJob();
            ContainedHarnessProcess.CheckOperation(
                deadline,
                cancellationToken);
            attributeList = ContainedHarnessProcess.CreateJobAttributeList(
                job,
                out jobValue,
                out attributeListInitialized);

            string commandText = ContainedHarnessProcess.QuoteCommandPart(
                namespaceLease.CanonicalDosExecutablePath) + " " + argument;
            char[] commandLine = (commandText + '\0').ToCharArray();
            char[] environment = new[] { '\0', '\0' };
            process = new NativeMethods.SafeProcessHandle();
            thread = new NativeMethods.SafeThreadHandle();
            try
            {
                fixed (char* commandPointer = commandLine)
                fixed (char* environmentPointer = environment)
                {
                    RevalidatePreResume(
                        audit,
                        namespaceLease,
                        deadline,
                        cancellationToken);
                    NativeFixturePlatformPolicy
                        .RequireWindows10Version1709OrLater();
                    ContainedHarnessProcess.CheckOperation(
                        deadline,
                        cancellationToken);
                    NativeMethods.StartupInfoEx startup = default;
                    startup.StartupInfo.Size = checked((uint)Marshal.SizeOf<
                        NativeMethods.StartupInfoEx>());
                    startup.AttributeList = attributeList;
                    uint flags = NativeMethods.DebugOnlyThisProcess |
                        NativeMethods.CreateDefaultErrorMode |
                        NativeMethods.CreateNoWindow |
                        NativeMethods.CreateUnicodeEnvironment |
                        NativeMethods.ExtendedStartupInfoPresent;
                    if (NativeMethods.CreateProcess(
                            namespaceLease.CanonicalDosExecutablePath,
                            commandPointer,
                            0,
                            0,
                            0,
                            flags,
                            environmentPointer,
                            namespaceLease.CanonicalDosDirectory,
                            ref startup,
                            out NativeMethods.ProcessInformation created) == 0)
                    {
                        throw NativeMethods.Win32Failure(
                            "Canonical-DOS CreateProcessW failed");
                    }

                    createdProcessId = created.ProcessId;
                    debugAttached = true;

                    nint rawProcess = created.Process;
                    nint rawThread = created.Thread;
                    try
                    {
                        process.Initialize(rawProcess);
                        rawProcess = 0;
                        thread.Initialize(rawThread);
                        rawThread = 0;
                    }
                    finally
                    {
                        if (rawThread != 0)
                        {
                            _ = NativeMethods.CloseRawKernelHandle(rawThread);
                        }

                        if (rawProcess != 0)
                        {
                            _ = NativeMethods.CloseRawKernelHandle(rawProcess);
                        }
                    }

                    if (process.IsInvalid || thread.IsInvalid ||
                        created.ProcessId == 0 || created.ThreadId == 0)
                    {
                        throw new SecurityException(
                            "CreateProcessW returned an invalid native-fixture process identity.");
                    }

                    testFaults?.AfterCreateProcessHandlesAdopted(
                        created.ProcessId);
                    ContainedHarnessProcess.CheckOperation(
                        deadline,
                        cancellationToken);

                    NativeMethods.DebugEvent initialEvent =
                        WaitForInitialCreateProcessDebugEvent(
                            deadline,
                            cancellationToken);
                    debugEventOutstanding = true;
                    debugEventCode = initialEvent.Code;
                    debugEventProcessId = initialEvent.ProcessId;
                    debugEventThreadId = initialEvent.ThreadId;
                    debugImageFile = AdoptTypedDebugEventFile(initialEvent);
                    RequireExactInitialDebugEvent(
                        initialEvent,
                        created.ProcessId,
                        created.ThreadId,
                        process,
                        thread,
                        ref debugImageFile);

                    uint previousSuspendCount =
                        NativeMethods.SuspendThread(thread);
                    if (previousSuspendCount == uint.MaxValue)
                    {
                        throw NativeMethods.Win32Failure(
                            "Native-fixture SuspendThread failed");
                    }

                    if (previousSuspendCount != 0)
                    {
                        throw new SecurityException(
                            "The native-fixture initial thread had an unexpected prior suspend count.");
                    }

                    testFaults?.AfterInitialDebugEventOwned(created.ProcessId);
                    RevalidatePreResume(
                        audit,
                        namespaceLease,
                        deadline,
                        cancellationToken);
                    ContainedHarnessProcess.RequireProcessInExactJob(
                        job,
                        process,
                        created.ProcessId);
                    RequireExactAmd64Process(process);
                    testFaults?.AfterExactJobVerified(created.ProcessId);
                    RevalidatePreResume(
                        audit,
                        namespaceLease,
                        deadline,
                        cancellationToken);

                    using (SafeFileHandle borrowedDebugImage = new(
                               debugImageFile,
                               ownsHandle: false))
                    {
                        namespaceLease.ValidateDebugImageFileHandle(
                            borrowedDebugImage,
                            deadline,
                            cancellationToken);
                    }

                    identity = ProcessIdentityLease.Capture(created.ProcessId);
                    BootstrapBinding binding = identity.Snapshot();
                    namespaceLease.ValidateReportedImagePath(
                        binding.ImagePath,
                        deadline,
                        cancellationToken);
                    audit.RevalidateExactSet(deadline, cancellationToken);
                    testFaults?.AfterImageFileIdentityVerified(
                        created.ProcessId);
                    RevalidatePreResume(
                        audit,
                        namespaceLease,
                        deadline,
                        cancellationToken);
                    CloseDebugImageFile(ref debugImageFile);
                    ContinueOutstandingDebugEvent(
                        ref debugEventOutstanding,
                        debugEventProcessId,
                        debugEventThreadId);
                    testFaults?.AfterDebugEventContinued(created.ProcessId);
                    ContainedHarnessProcess.CheckOperation(
                        deadline,
                        cancellationToken);
                    DetachDebugger(ref debugAttached, created.ProcessId);
                    ContainedHarnessProcess.CheckOperation(
                        deadline,
                        cancellationToken);
                    RequireNoRemoteDebugger(process);
                    testFaults?.AfterDebuggerDetached(created.ProcessId);
                    RevalidatePreResume(
                        audit,
                        namespaceLease,
                        deadline,
                        cancellationToken);

                    testFaults?.BeforeResume(created.ProcessId);
                    RevalidatePreResume(
                        audit,
                        namespaceLease,
                        deadline,
                        cancellationToken);
                    RequireExactAmd64Process(process);
                    RequireNoRemoteDebugger(process);
                    NativeFixturePlatformPolicy
                        .RequireWindows10Version1709OrLater();
                    identity.EnsureStillAlive();
                    ContainedHarnessProcess.CheckOperation(
                        deadline,
                        cancellationToken);
                    if (NativeMethods.ResumeThread(thread) != 1)
                    {
                        throw NativeMethods.Win32Failure(
                            "Native-fixture ResumeThread failed");
                    }

                    testFaults?.AfterResume(created.ProcessId);
                    ContainedHarnessProcess.CheckOperation(
                        deadline,
                        cancellationToken);
                    thread.Dispose();
                    thread = null;
                    ContainedAuditedNativeFixtureProcess result = new(
                        job,
                        process,
                        identity,
                        namespaceLease,
                        audit,
                        binding);
                    job = null;
                    process = null;
                    identity = null;
                    namespaceLease = null;
                    audit = null;
                    return result;
                }
            }
            finally
            {
                Array.Clear(commandLine);
                Array.Clear(environment);
            }
        }
        catch (Exception primary)
        {
            CleanupFailedLaunch(
                ref debugImageFile,
                ref debugEventOutstanding,
                debugEventCode,
                debugEventProcessId,
                debugEventThreadId,
                ref debugAttached,
                createdProcessId,
                ref job,
                ref process,
                ref identity,
                ref thread,
                ref namespaceLease,
                ref audit,
                cleanupStopwatch,
                cleanupFailures);
            if (cleanupFailures.HasFailures)
            {
                List<Exception> combined = new(
                    cleanupFailures.MaterializedCount + 1)
                {
                    primary,
                };
                cleanupFailures.AppendMaterialized(combined);
                throw new AggregateException(
                    "Audited native-fixture launch and cleanup both failed.",
                    combined);
            }

            ExceptionDispatchInfo.Capture(primary).Throw();
            throw;
        }
        finally
        {
            if (attributeListInitialized)
            {
                NativeMethods.DeleteProcThreadAttributeList(attributeList);
            }

            if (attributeList != 0)
            {
                Marshal.FreeHGlobal(attributeList);
            }

            if (jobValue != 0)
            {
                Marshal.FreeHGlobal(jobValue);
            }
        }
    }

    private sealed class LaunchThreadResult
    {
        internal ContainedAuditedNativeFixtureProcess? Result { get; set; }

        internal ExceptionDispatchInfo? Failure { get; set; }
    }

    private static void JoinNonAbandonably(Thread worker)
    {
        while (true)
        {
            try
            {
                worker.Join();
                return;
            }
            catch (ThreadInterruptedException)
            {
                // The launch core owns the absolute deadline and cleanup.
                // Never abandon it or wipe its input copies while it is live.
            }
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

    internal bool IsAlive()
    {
        NativeMethods.SafeProcessHandle current;
        lock (gate)
        {
            current = process ??
                throw new ObjectDisposedException(
                    nameof(ContainedAuditedNativeFixtureProcess));
        }

        bool added = false;
        try
        {
            current.DangerousAddRef(ref added);
            uint wait = NativeMethods.WaitForSingleObject(current, 0);
            if (wait == NativeMethods.WaitTimeout)
            {
                return true;
            }

            if (wait == NativeMethods.WaitObject0)
            {
                return false;
            }

            throw NativeMethods.Win32Failure("WaitForSingleObject failed");
        }
        finally
        {
            if (added)
            {
                current.DangerousRelease();
            }
        }
    }

    internal async Task<uint> WaitForExitAsync(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        NativeMethods.SafeProcessHandle current;
        lock (gate)
        {
            current = process ??
                throw new ObjectDisposedException(
                    nameof(ContainedAuditedNativeFixtureProcess));
        }

        bool added = false;
        try
        {
            current.DangerousAddRef(ref added);
            await ContainedHarnessProcess.WaitForSignalAsync(
                    current,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (NativeMethods.GetExitCodeProcess(current, out uint exitCode) == 0)
            {
                throw NativeMethods.Win32Failure("GetExitCodeProcess failed");
            }

            if (exitCode == NativeMethods.StillActive)
            {
                throw new InvalidOperationException(
                    "The contained native fixture remained active after signalling.");
            }

            return exitCode;
        }
        finally
        {
            if (added)
            {
                current.DangerousRelease();
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource<object?>? completion = null;
        Task result;
        lock (gate)
        {
            if (disposalTask is null)
            {
                completion = new TaskCompletionSource<object?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                disposalTask = completion.Task;
            }

            result = disposalTask;
        }

        if (completion is not null)
        {
            CompleteDisposal(completion);
        }

        return new ValueTask(result);
    }

    private void CompleteDisposal(
        TaskCompletionSource<object?> completion)
    {
        try
        {
            DisposeCore();
            completion.TrySetResult(null);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }

    }

    private void DisposeCore()
    {
        CleanupFailureLedger failures = disposalFailures;
        failures.Reset();
        disposalStopwatch.Restart();
        NativeMethods.SafeJobHandle? ownedJob;
        NativeMethods.SafeProcessHandle? ownedProcess;
        ProcessIdentityLease? ownedIdentity;
        TrustedArtifactLaunchNamespaceLease? ownedNamespace;
        AuditedNativeFixtureReleaseLease? ownedAudit;
        lock (gate)
        {
            ownedJob = job;
            job = null;
            ownedProcess = process;
            process = null;
            ownedIdentity = identity;
            identity = null;
            ownedNamespace = launchNamespace;
            launchNamespace = null;
            ownedAudit = auditedRelease;
            auditedRelease = null;
        }

        DisposeCleanupResource(failures, ownedJob);
        bool signalled = ownedProcess is null || ownedProcess.IsInvalid;
        if (ownedProcess is not null && !ownedProcess.IsInvalid)
        {
            try
            {
                uint wait = NativeMethods.WaitForSingleObject(
                    ownedProcess,
                    5_000);
                if (wait == NativeMethods.WaitObject0)
                {
                    signalled = true;
                }
                else if (wait == NativeMethods.WaitTimeout)
                {
                    failures.Record(
                        CleanupFailureKind.FailedProcessExitTimedOut);
                }
                else
                {
                    failures.RecordWin32(
                        CleanupFailureKind.FailedProcessWaitFailed,
                        Marshal.GetLastWin32Error());
                }
            }
            catch (Exception exception)
            {
                failures.Record(exception);
            }
        }

        if (!signalled && ownedProcess is not null &&
            ownedNamespace is not null && ownedAudit is not null)
        {
            try
            {
                NativeFixtureProcessReaper.Retain(
                    ownedJob,
                    ownedProcess,
                    ownedIdentity,
                    ownedNamespace,
                    ownedAudit);
                ownedJob = null;
                ownedProcess = null;
                ownedIdentity = null;
                ownedNamespace = null;
                ownedAudit = null;
            }
            catch (Exception exception)
            {
                failures.Record(exception);
            }

        }

        if (!signalled && ownedProcess is not null)
        {
            disposalStopwatch.Restart();
            bool crossedBound = false;
            WaitForExactExitNonAbandonably(
                ownedProcess,
                disposalStopwatch,
                ref crossedBound,
                failures);
            signalled = true;
            if (crossedBound)
            {
                failures.Record(
                    CleanupFailureKind.DisposalFallbackBoundCrossed);
            }
        }

        if (signalled)
        {
            DisposeCleanupResource(failures, ownedIdentity);
            DisposeCleanupResource(failures, ownedProcess);
            DisposeCleanupResource(failures, ownedJob);
            DisposeCleanupResource(failures, ownedNamespace);
            DisposeCleanupResource(failures, ownedAudit);
        }
        if (failures.MaterializedCount == 1)
        {
            ExceptionDispatchInfo.Capture(
                failures.MaterializeSingle()).Throw();
        }

        if (failures.HasFailures)
        {
            List<Exception> materialized = new(failures.MaterializedCount);
            failures.AppendMaterialized(materialized);
            throw new AggregateException(
                "Contained native-fixture disposal had multiple failures.",
                materialized);
        }
    }

    private static void RequireExactAuditedPaths(
        AuditedNativeFixtureReleaseLease audit,
        TrustedArtifactLaunchNamespaceLease launchNamespace)
    {
        if (!string.Equals(
                audit.ExecutablePath,
                launchNamespace.CanonicalDosExecutablePath,
                StringComparison.Ordinal) ||
            !string.Equals(
                audit.ApplicationDirectory,
                launchNamespace.CanonicalDosDirectory,
                StringComparison.Ordinal) ||
            !string.Equals(
                Path.GetExtension(launchNamespace.VolumeGuidExecutablePath),
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                "The audited native-fixture launch paths are inconsistent.");
        }
    }

    private static void RevalidatePreResume(
        AuditedNativeFixtureReleaseLease audit,
        TrustedArtifactLaunchNamespaceLease launchNamespace,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        ContainedHarnessProcess.CheckOperation(deadline, cancellationToken);
        launchNamespace.Revalidate(deadline, cancellationToken);
        audit.RevalidateExactSet(deadline, cancellationToken);
        ContainedHarnessProcess.CheckOperation(deadline, cancellationToken);
    }

    private static void RequireExactDebugAbi()
    {
        if (IntPtr.Size != 8 ||
            Marshal.SizeOf<NativeMethods.CreateProcessDebugInfo>() != 72 ||
            Marshal.SizeOf<NativeMethods.DebugEventUnion>() != 160 ||
            Marshal.SizeOf<NativeMethods.DebugEvent>() != 176 ||
            Marshal.OffsetOf<NativeMethods.DebugEvent>(
                nameof(NativeMethods.DebugEvent.Code)).ToInt32() != 0 ||
            Marshal.OffsetOf<NativeMethods.DebugEvent>(
                nameof(NativeMethods.DebugEvent.ProcessId)).ToInt32() != 4 ||
            Marshal.OffsetOf<NativeMethods.DebugEvent>(
                nameof(NativeMethods.DebugEvent.ThreadId)).ToInt32() != 8 ||
            Marshal.OffsetOf<NativeMethods.DebugEvent>(
                nameof(NativeMethods.DebugEvent.Union)).ToInt32() != 16 ||
            Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
                nameof(NativeMethods.CreateProcessDebugInfo.File)).ToInt32() != 0 ||
            Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
                nameof(NativeMethods.CreateProcessDebugInfo.Process)).ToInt32() != 8 ||
            Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
                nameof(NativeMethods.CreateProcessDebugInfo.Thread)).ToInt32() != 16 ||
            Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
                nameof(NativeMethods.CreateProcessDebugInfo.BaseOfImage)).ToInt32() != 24 ||
            Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
                nameof(NativeMethods.CreateProcessDebugInfo.DebugInfoFileOffset)).ToInt32() != 32 ||
            Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
                nameof(NativeMethods.CreateProcessDebugInfo.DebugInfoSize)).ToInt32() != 36 ||
            Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
                nameof(NativeMethods.CreateProcessDebugInfo.ThreadLocalBase)).ToInt32() != 40 ||
            Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
                nameof(NativeMethods.CreateProcessDebugInfo.StartAddress)).ToInt32() != 48 ||
            Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
                nameof(NativeMethods.CreateProcessDebugInfo.ImageName)).ToInt32() != 56 ||
            Marshal.OffsetOf<NativeMethods.CreateProcessDebugInfo>(
                nameof(NativeMethods.CreateProcessDebugInfo.Unicode)).ToInt32() != 64)
        {
            throw new PlatformNotSupportedException(
                "The native-fixture debug-event ABI is not the exact AMD64 layout.");
        }
    }

    private static unsafe NativeMethods.DebugEvent
        WaitForInitialCreateProcessDebugEvent(
            MonotonicDeadline deadline,
            CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            uint slice = Math.Min(
                ToWaitMilliseconds(deadline.GetRemaining()),
                50u);
            NativeMethods.DebugEvent value = default;
            if (NativeMethods.WaitForDebugEvent(&value, slice) != 0)
            {
                // The caller records this event as outstanding before applying
                // any late deadline or cancellation rejection.
                return value;
            }

            int error = Marshal.GetLastWin32Error();
            if (error != NativeMethods.ErrorSemTimeout)
            {
                throw new System.ComponentModel.Win32Exception(
                    error,
                    "WaitForDebugEvent failed");
            }
        }
    }

    private static void RequireExactInitialDebugEvent(
        NativeMethods.DebugEvent value,
        uint createdProcessId,
        uint createdThreadId,
        NativeMethods.SafeProcessHandle createdProcess,
        NativeMethods.SafeThreadHandle createdThread,
        ref nint ownedDebugImageFile)
    {
        if (value.Code != NativeMethods.CreateProcessDebugEvent)
        {
            throw new SecurityException(
                "The initial native-fixture debug event was not the exact create-process event.");
        }

        NativeMethods.CreateProcessDebugInfo information =
            value.Union.CreateProcess;
        if (ownedDebugImageFile != information.File ||
            ownedDebugImageFile == 0 || ownedDebugImageFile == -1 ||
            information.Process == 0 || information.Process == -1 ||
            information.Thread == 0 || information.Thread == -1)
        {
            if (ownedDebugImageFile == -1)
            {
                ownedDebugImageFile = 0;
            }

            throw new SecurityException(
                "The initial native-fixture debug event was not the exact create-process event.");
        }

        if (value.ProcessId != createdProcessId ||
            value.ThreadId != createdThreadId ||
            NativeMethods.CompareObjectHandles(
                information.Process,
                createdProcess.DangerousGetHandle()) == 0 ||
            NativeMethods.CompareObjectHandles(
                information.Thread,
                createdThread.DangerousGetHandle()) == 0)
        {
            throw new SecurityException(
                "The debug-event handles did not identify the created process and thread.");
        }
    }

    private static void RequireExactAmd64Process(
        NativeMethods.SafeProcessHandle process)
    {
        if (NativeMethods.IsWow64Process2(
                process,
                out ushort processMachine,
                out ushort nativeMachine) == 0)
        {
            throw NativeMethods.Win32Failure("IsWow64Process2 failed");
        }

        if (processMachine != NativeMethods.ImageFileMachineUnknown ||
            nativeMachine != NativeMethods.ImageFileMachineAmd64)
        {
            throw new PlatformNotSupportedException(
                "The contained native fixture is not an exact native AMD64 process.");
        }
    }

    private static void RequireNoRemoteDebugger(
        NativeMethods.SafeProcessHandle process)
    {
        if (NativeMethods.CheckRemoteDebuggerPresent(
                process,
                out int debuggerPresent) == 0)
        {
            throw NativeMethods.Win32Failure(
                "CheckRemoteDebuggerPresent failed");
        }

        if (debuggerPresent != 0)
        {
            throw new SecurityException(
                "The contained native fixture remained debug-attached.");
        }
    }

    private static void CloseDebugImageFile(ref nint debugImageFile)
    {
        nint owned = debugImageFile;
        debugImageFile = 0;
        if (owned == 0 || owned == -1)
        {
            return;
        }

        if (NativeMethods.CloseRawKernelHandle(owned) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Closing the create-process debug image file failed");
        }

    }

    private static void ContinueOutstandingDebugEvent(
        ref bool outstanding,
        uint processId,
        uint threadId)
    {
        if (!outstanding)
        {
            return;
        }

        if (NativeMethods.ContinueDebugEvent(
                processId,
                threadId,
                NativeMethods.DbgContinue) == 0)
        {
            throw NativeMethods.Win32Failure("ContinueDebugEvent failed");
        }

        outstanding = false;
    }

    private static void DetachDebugger(ref bool attached, uint processId)
    {
        if (!attached)
        {
            return;
        }

        if (processId == 0 || NativeMethods.DebugActiveProcessStop(processId) == 0)
        {
            throw NativeMethods.Win32Failure("DebugActiveProcessStop failed");
        }

        attached = false;
    }

    private static void CleanupFailedLaunch(
        ref nint debugImageFile,
        ref bool debugEventOutstanding,
        uint debugEventCode,
        uint debugEventProcessId,
        uint debugEventThreadId,
        ref bool debugAttached,
        uint processId,
        ref NativeMethods.SafeJobHandle? job,
        ref NativeMethods.SafeProcessHandle? process,
        ref ProcessIdentityLease? identity,
        ref NativeMethods.SafeThreadHandle? thread,
        ref TrustedArtifactLaunchNamespaceLease? launchNamespace,
        ref AuditedNativeFixtureReleaseLease? audit,
        Stopwatch cleanupStopwatch,
        CleanupFailureLedger failures)
    {
        cleanupStopwatch.Restart();
        bool crossedBound = false;
        ResolveDebugSessionNonAbandonably(
            ref debugImageFile,
            ref debugEventOutstanding,
            debugEventCode,
            debugEventProcessId,
            debugEventThreadId,
            ref debugAttached,
            processId,
            ref job,
            process,
            cleanupStopwatch,
            ref crossedBound,
            failures);

        NativeMethods.SafeThreadHandle? ownedThread = thread;
        thread = null;
        DisposeCleanupResource(failures, ownedThread);
        NativeMethods.SafeJobHandle? ownedJobForClose = job;
        DisposeCleanupResource(failures, ownedJobForClose);
        bool signalled = process is null || process.IsInvalid;
        if (process is not null && !process.IsInvalid)
        {
            try
            {
                uint wait = NativeMethods.WaitForSingleObject(process, 5_000);
                if (wait == NativeMethods.WaitObject0)
                {
                    signalled = true;
                }
                else if (wait == NativeMethods.WaitTimeout)
                {
                    failures.Record(
                        CleanupFailureKind.FailedProcessExitTimedOut);
                }
                else
                {
                    failures.RecordWin32(
                        CleanupFailureKind.FailedProcessWaitFailed,
                        Marshal.GetLastWin32Error());
                }
            }
            catch (Exception exception)
            {
                failures.Record(exception);
            }
        }

        if (!signalled && process is not null && !process.IsInvalid)
        {
            if (launchNamespace is not null && audit is not null)
            {
                try
                {
                    NativeFixtureProcessReaper.Retain(
                        job,
                        process,
                        identity,
                        launchNamespace,
                        audit);
                    job = null;
                    process = null;
                    identity = null;
                    launchNamespace = null;
                    audit = null;
                }
                catch (Exception exception)
                {
                    failures.Record(exception);
                }
            }

            if (process is not null)
            {
                WaitForExactExitNonAbandonably(
                    process,
                    cleanupStopwatch,
                    ref crossedBound,
                    failures);
                signalled = true;
            }
        }

        if (signalled)
        {
            ProcessIdentityLease? ownedIdentity = identity;
            identity = null;
            DisposeCleanupResource(failures, ownedIdentity);
            NativeMethods.SafeProcessHandle? ownedProcess = process;
            process = null;
            DisposeCleanupResource(failures, ownedProcess);
            NativeMethods.SafeJobHandle? ownedJob = job;
            job = null;
            DisposeCleanupResource(failures, ownedJob);
            TrustedArtifactLaunchNamespaceLease? ownedNamespace =
                launchNamespace;
            launchNamespace = null;
            DisposeCleanupResource(failures, ownedNamespace);
            AuditedNativeFixtureReleaseLease? ownedAudit = audit;
            audit = null;
            DisposeCleanupResource(failures, ownedAudit);
        }

        if (crossedBound)
        {
            failures.Record(CleanupFailureKind.DebugCleanupBoundCrossed);
        }
    }

    private static unsafe void ResolveDebugSessionNonAbandonably(
        ref nint ownedDebugImageFile,
        ref bool outstanding,
        uint outstandingCode,
        uint outstandingProcessId,
        uint outstandingThreadId,
        ref bool attached,
        uint processId,
        ref NativeMethods.SafeJobHandle? job,
        NativeMethods.SafeProcessHandle? process,
        Stopwatch cleanupStopwatch,
        ref bool crossedBound,
        CleanupFailureLedger failures)
    {
        bool exitEventContinued = false;
        while (attached || outstanding || ownedDebugImageFile != 0)
        {
            UpdateCleanupOverrun(cleanupStopwatch, ref crossedBound);
            if (ownedDebugImageFile != 0 && ownedDebugImageFile != -1)
            {
                try
                {
                    CloseDebugImageFile(ref ownedDebugImageFile);
                }
                catch (Exception exception)
                {
                    failures.Record(exception);
                }
            }

            if (outstanding)
            {
                try
                {
                    if (NativeMethods.ContinueDebugEvent(
                            outstandingProcessId,
                            outstandingThreadId,
                            NativeMethods.DbgContinue) == 0)
                    {
                        throw NativeMethods.Win32Failure(
                            "Cleanup ContinueDebugEvent failed");
                    }

                    exitEventContinued |=
                        outstandingCode == NativeMethods.ExitProcessDebugEvent &&
                        outstandingProcessId == processId;
                    outstanding = false;
                    outstandingCode = 0;
                }
                catch (Exception exception)
                {
                    failures.Record(exception);
                    PauseCleanupRetry(failures);
                    continue;
                }

                if (exitEventContinued)
                {
                    attached = false;
                    continue;
                }
            }

            if (!attached)
            {
                continue;
            }

            if (process is not null &&
                TryConfirmNoRemoteDebugger(process, failures))
            {
                attached = false;
                continue;
            }

            if (job is not null)
            {
                NativeMethods.SafeJobHandle ownedJob = job;
                job = null;
                try
                {
                    ownedJob.Dispose();
                }
                catch (Exception exception)
                {
                    failures.Record(exception);
                }
            }

            NativeMethods.DebugEvent value = default;
            try
            {
                if (NativeMethods.WaitForDebugEvent(&value, 50) == 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == NativeMethods.ErrorSemTimeout)
                    {
                        continue;
                    }

                    throw new System.ComponentModel.Win32Exception(
                        error,
                        "Cleanup WaitForDebugEvent failed");
                }
            }
            catch (Exception exception)
            {
                failures.Record(exception);
                PauseCleanupRetry(failures);
                continue;
            }

            outstanding = true;
            outstandingCode = value.Code;
            outstandingProcessId = value.ProcessId;
            outstandingThreadId = value.ThreadId;
            ownedDebugImageFile = AdoptTypedDebugEventFile(value);
        }
    }

    private static nint AdoptTypedDebugEventFile(
        NativeMethods.DebugEvent value)
    {
        nint file = value.Code switch
        {
            NativeMethods.ExceptionDebugEvent => 0,
            NativeMethods.CreateThreadDebugEvent => 0,
            NativeMethods.CreateProcessDebugEvent =>
                value.Union.CreateProcess.File,
            NativeMethods.ExitThreadDebugEvent => 0,
            NativeMethods.ExitProcessDebugEvent => 0,
            NativeMethods.LoadDllDebugEvent => value.Union.LoadDll.File,
            NativeMethods.UnloadDllDebugEvent => 0,
            NativeMethods.OutputDebugStringEvent => 0,
            NativeMethods.RipEvent => 0,
            _ => 0,
        };
        return file == -1 ? 0 : file;
    }

    private static bool TryConfirmNoRemoteDebugger(
        NativeMethods.SafeProcessHandle process,
        CleanupFailureLedger failures)
    {
        try
        {
            if (NativeMethods.CheckRemoteDebuggerPresent(
                    process,
                    out int debuggerPresent) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "Cleanup CheckRemoteDebuggerPresent failed");
            }

            return debuggerPresent == 0;
        }
        catch (Exception exception)
        {
            failures.Record(exception);
            return false;
        }
    }

    private static void WaitForExactExitNonAbandonably(
        NativeMethods.SafeProcessHandle process,
        Stopwatch cleanupStopwatch,
        ref bool crossedBound,
        CleanupFailureLedger failures)
    {
        while (true)
        {
            UpdateCleanupOverrun(cleanupStopwatch, ref crossedBound);
            uint wait;
            try
            {
                wait = NativeMethods.WaitForSingleObject(process, 50);
            }
            catch (Exception exception)
            {
                failures.Record(exception);
                PauseCleanupRetry(failures);
                continue;
            }

            if (wait == NativeMethods.WaitObject0)
            {
                return;
            }

            if (wait != NativeMethods.WaitTimeout)
            {
                failures.RecordWin32(
                    CleanupFailureKind.ExactProcessWaitFailed,
                    Marshal.GetLastWin32Error());
                PauseCleanupRetry(failures);
            }
        }
    }

    private static void PauseCleanupRetry(CleanupFailureLedger failures)
    {
        while (true)
        {
            try
            {
                Thread.Sleep(50);
                return;
            }
            catch (Exception exception)
            {
                failures.Record(exception);
            }
        }
    }

    private static void UpdateCleanupOverrun(
        Stopwatch cleanupStopwatch,
        ref bool crossedBound)
    {
        crossedBound |= cleanupStopwatch.Elapsed > TimeSpan.FromSeconds(5);
    }

    private static void DisposeCleanupResource(
        CleanupFailureLedger failures,
        IDisposable? resource)
    {
        try
        {
            resource?.Dispose();
        }
        catch (Exception exception)
        {
            failures.Record(exception);
        }
    }

    [Flags]
    private enum CleanupFailureKind
    {
        None = 0,
        FailedProcessExitTimedOut = 1 << 0,
        FailedProcessWaitFailed = 1 << 1,
        DebugCleanupBoundCrossed = 1 << 2,
        DisposalFallbackBoundCrossed = 1 << 3,
        ExactProcessWaitFailed = 1 << 4,
    }

    /// <summary>
    /// Fixed cleanup diagnostics allocated before live authority is acquired
    /// or transferred. Recording never constructs an exception, grows a
    /// collection, or reads an exception message. Diagnostics are materialized
    /// only after exact exit or successful transfer to the static reaper.
    /// </summary>
    private sealed class CleanupFailureLedger
    {
        private const int MaximumExceptionDetails = 32;
        private readonly Exception?[] details =
            new Exception?[MaximumExceptionDetails];
        private int detailCount;
        private CleanupFailureKind kinds;
        private int failedProcessWaitError;
        private int exactProcessWaitError;
        private bool droppedDetails;

        internal bool HasFailures =>
            detailCount != 0 || kinds != CleanupFailureKind.None ||
            droppedDetails;

        internal int MaterializedCount
        {
            get
            {
                int count = detailCount + (droppedDetails ? 1 : 0);
                uint remaining = (uint)kinds;
                while (remaining != 0)
                {
                    count += (int)(remaining & 1);
                    remaining >>= 1;
                }

                return count;
            }
        }

        internal void Reset()
        {
            Array.Clear(details, 0, detailCount);
            detailCount = 0;
            kinds = CleanupFailureKind.None;
            failedProcessWaitError = 0;
            exactProcessWaitError = 0;
            droppedDetails = false;
        }

        internal void Record(Exception exception)
        {
            for (int index = 0; index < detailCount; index++)
            {
                if (ReferenceEquals(details[index], exception))
                {
                    return;
                }
            }

            if (detailCount < details.Length)
            {
                details[detailCount++] = exception;
                return;
            }

            droppedDetails = true;
        }

        internal void Record(CleanupFailureKind kind)
        {
            kinds |= kind;
        }

        internal void RecordWin32(CleanupFailureKind kind, int error)
        {
            if ((kinds & kind) == 0)
            {
                if (kind == CleanupFailureKind.FailedProcessWaitFailed)
                {
                    failedProcessWaitError = error;
                }
                else if (kind == CleanupFailureKind.ExactProcessWaitFailed)
                {
                    exactProcessWaitError = error;
                }
            }

            kinds |= kind;
        }

        internal Exception MaterializeSingle()
        {
            List<Exception> materialized = new(1);
            AppendMaterialized(materialized);
            return materialized[0];
        }

        internal void AppendMaterialized(List<Exception> destination)
        {
            for (int index = 0; index < detailCount; index++)
            {
                destination.Add(details[index]!);
            }

            if ((kinds & CleanupFailureKind.FailedProcessExitTimedOut) != 0)
            {
                destination.Add(new TimeoutException(
                    "The failed native-fixture process did not exit within its cleanup bound; authority was retained by the process reaper."));
            }

            if ((kinds & CleanupFailureKind.FailedProcessWaitFailed) != 0)
            {
                destination.Add(new System.ComponentModel.Win32Exception(
                    failedProcessWaitError,
                    "Waiting for the failed native-fixture process failed"));
            }

            if ((kinds & CleanupFailureKind.DebugCleanupBoundCrossed) != 0)
            {
                destination.Add(new TimeoutException(
                    "Non-abandonable native-fixture debug cleanup crossed its five-second observation bound before resolving."));
            }

            if ((kinds & CleanupFailureKind.DisposalFallbackBoundCrossed) != 0)
            {
                destination.Add(new TimeoutException(
                    "Native-fixture disposal fallback crossed its five-second observation bound."));
            }

            if ((kinds & CleanupFailureKind.ExactProcessWaitFailed) != 0)
            {
                destination.Add(new System.ComponentModel.Win32Exception(
                    exactProcessWaitError,
                    "Non-abandonable exact process wait failed"));
            }

            if (droppedDetails)
            {
                destination.Add(new InvalidOperationException(
                    "Additional native-fixture cleanup failures exceeded the fixed diagnostic capacity."));
            }
        }
    }

    private static uint ToWaitMilliseconds(TimeSpan remaining)
    {
        double value = Math.Ceiling(remaining.TotalMilliseconds);
        return value >= uint.MaxValue ? uint.MaxValue - 1 :
            Math.Max(1u, checked((uint)value));
    }
}
