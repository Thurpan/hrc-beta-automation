using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HrcJobObserver.WindowsBootstrap;

internal enum ContainedHarnessMode
{
    Exit,
    Block,
}

internal interface IContainmentTestFaults
{
    void BeforeResume(uint processId);

    void AfterResume(uint processId);
}

/// <summary>
/// Launches only this repository's synthetic harness child in an unnamed
/// kill-on-close Job Object assigned by PROC_THREAD_ATTRIBUTE_JOB_LIST before
/// its initial thread can run. This is an offline containment primitive, not
/// a production role launcher or an artifact/runtime identity guarantee.
/// </summary>
internal sealed class ContainedHarnessProcess : IAsyncDisposable
{
    private const int MaximumJobProcessEntries = 64;
    private const string ExitArgument = "--contained-exit";
    private const string BlockArgument = "--contained-block";
    private readonly object gate = new();
    private NativeMethods.SafeJobHandle? job;
    private NativeMethods.SafeProcessHandle? process;
    private ProcessIdentityLease? identity;
    private Task? disposalTask;

    private ContainedHarnessProcess(
        NativeMethods.SafeJobHandle job,
        NativeMethods.SafeProcessHandle process,
        ProcessIdentityLease identity,
        BootstrapBinding binding)
    {
        this.job = job;
        this.process = process;
        this.identity = identity;
        ProcessId = binding.ProcessId;
        Binding = binding;
    }

    internal uint ProcessId { get; }

    internal BootstrapBinding Binding { get; }

    internal static unsafe ContainedHarnessProcess Launch(
        string exactExecutablePath,
        ContainedHarnessMode mode,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken,
        IContainmentTestFaults? testFaults = null)
    {
        CheckOperation(deadline, cancellationToken);
        string executable = ValidateExecutablePath(exactExecutablePath);
        string currentDirectory = Path.GetDirectoryName(executable) ??
            throw new SecurityException("The executable directory is unavailable.");
        string argument = mode switch
        {
            ContainedHarnessMode.Exit => ExitArgument,
            ContainedHarnessMode.Block => BlockArgument,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        NativeMethods.SafeJobHandle? job = null;
        NativeMethods.SafeProcessHandle? process = null;
        NativeMethods.SafeThreadHandle? thread = null;
        ProcessIdentityLease? identity = null;
        nint attributeList = 0;
        nint jobValue = 0;
        bool attributeListInitialized = false;
        try
        {
            job = CreateConfiguredJob();
            CheckOperation(deadline, cancellationToken);
            attributeList = CreateJobAttributeList(
                job,
                out jobValue,
                out attributeListInitialized);

            string commandText = QuoteCommandPart(executable) + " " + argument;
            char[] commandLine = (commandText + '\0').ToCharArray();
            char[] environment = new[] { '\0', '\0' };
            try
            {
                fixed (char* commandPointer = commandLine)
                fixed (char* environmentPointer = environment)
                {
                    CheckOperation(deadline, cancellationToken);
                    NativeMethods.StartupInfoEx startup = default;
                    startup.StartupInfo.Size = checked((uint)Marshal.SizeOf<
                        NativeMethods.StartupInfoEx>());
                    startup.AttributeList = attributeList;
                    uint flags = NativeMethods.CreateSuspended |
                        NativeMethods.CreateDefaultErrorMode |
                        NativeMethods.CreateNoWindow |
                        NativeMethods.CreateUnicodeEnvironment |
                        NativeMethods.ExtendedStartupInfoPresent;
                    if (NativeMethods.CreateProcess(
                            executable,
                            commandPointer,
                            0,
                            0,
                            0,
                            flags,
                            environmentPointer,
                            currentDirectory,
                            ref startup,
                            out NativeMethods.ProcessInformation created) == 0)
                    {
                        throw NativeMethods.Win32Failure("CreateProcessW failed");
                    }

                    nint rawProcess = created.Process;
                    nint rawThread = created.Thread;
                    try
                    {
                        process = new NativeMethods.SafeProcessHandle(rawProcess);
                        rawProcess = 0;
                        thread = new NativeMethods.SafeThreadHandle(rawThread);
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
                            "CreateProcessW returned an invalid process identity.");
                    }

                    CheckOperation(deadline, cancellationToken);
                    RequireProcessInExactJob(job, process, created.ProcessId);
                    identity = ProcessIdentityLease.Capture(created.ProcessId);
                    BootstrapBinding binding = identity.Snapshot();
                    if (!string.Equals(
                            binding.ImagePath,
                            executable,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new SecurityException(
                            "The suspended process image path did not match.");
                    }

                    CheckOperation(deadline, cancellationToken);
                    testFaults?.BeforeResume(created.ProcessId);
                    CheckOperation(deadline, cancellationToken);
                    if (NativeMethods.ResumeThread(thread) != 1)
                    {
                        throw NativeMethods.Win32Failure("ResumeThread failed");
                    }

                    testFaults?.AfterResume(created.ProcessId);
                    CheckOperation(deadline, cancellationToken);
                    thread.Dispose();
                    thread = null;
                    ContainedHarnessProcess result = new(
                        job,
                        process,
                        identity,
                        binding);
                    job = null;
                    process = null;
                    identity = null;
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
            Exception? cleanup = CleanupFailedStart(job, process, deadline);
            identity?.Dispose();
            thread?.Dispose();
            process?.Dispose();
            job?.Dispose();
            if (cleanup is not null)
            {
                throw new AggregateException(
                    "Contained harness launch and cleanup both failed.",
                    primary,
                    cleanup);
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

    internal bool IsAlive()
    {
        NativeMethods.SafeProcessHandle current;
        lock (gate)
        {
            current = process ??
                throw new ObjectDisposedException(nameof(ContainedHarnessProcess));
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
                throw new ObjectDisposedException(nameof(ContainedHarnessProcess));
        }

        bool added = false;
        try
        {
            current.DangerousAddRef(ref added);
            await WaitForSignalAsync(current, deadline, cancellationToken)
                .ConfigureAwait(false);
            if (NativeMethods.GetExitCodeProcess(current, out uint exitCode) == 0)
            {
                throw NativeMethods.Win32Failure("GetExitCodeProcess failed");
            }

            if (exitCode == NativeMethods.StillActive)
            {
                throw new InvalidOperationException(
                    "The contained process remained active after signalling.");
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
            _ = CompleteDisposalAsync(completion);
        }

        return new ValueTask(result);
    }

    private async Task CompleteDisposalAsync(
        TaskCompletionSource<object?> completion)
    {
        try
        {
            await DisposeCoreAsync().ConfigureAwait(false);
            completion.TrySetResult(null);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private async Task DisposeCoreAsync()
    {
        NativeMethods.SafeJobHandle? ownedJob;
        NativeMethods.SafeProcessHandle? ownedProcess;
        ProcessIdentityLease? ownedIdentity;
        lock (gate)
        {
            ownedJob = job;
            job = null;
            ownedProcess = process;
            process = null;
            ownedIdentity = identity;
            identity = null;
        }

        Exception? failure = null;
        try
        {
            ownedJob?.Dispose();
            if (ownedProcess is not null && !ownedProcess.IsInvalid)
            {
                MonotonicDeadline cleanup = MonotonicDeadline.Start(
                    TimeProvider.System,
                    TimeSpan.FromSeconds(5));
                try
                {
                    await WaitForSignalAsync(
                            ownedProcess,
                            cleanup,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            }
        }
        finally
        {
            ownedIdentity?.Dispose();
            ownedProcess?.Dispose();
            ownedJob?.Dispose();
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    internal static NativeMethods.SafeJobHandle CreateConfiguredJob()
    {
        nint rawJob = NativeMethods.CreateJobObject(0, null);
        NativeMethods.SafeJobHandle? job = null;
        try
        {
            job = new NativeMethods.SafeJobHandle(rawJob);
            rawJob = 0;
        }
        finally
        {
            if (rawJob != 0 && rawJob != -1)
            {
                _ = NativeMethods.CloseRawKernelHandle(rawJob);
            }
        }

        if (job.IsInvalid)
        {
            job.Dispose();
            throw NativeMethods.Win32Failure("CreateJobObjectW failed");
        }

        if (NativeMethods.GetHandleInformation(job, out uint handleFlags) == 0)
        {
            job.Dispose();
            throw NativeMethods.Win32Failure("GetHandleInformation failed");
        }

        if (handleFlags != 0)
        {
            job.Dispose();
            throw new SecurityException(
                "The unnamed Job Object handle was unexpectedly inheritable or protected.");
        }

        int size = Marshal.SizeOf<NativeMethods.ExtendedLimitInformation>();
        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            unsafe
            {
                new Span<byte>((void*)buffer, size).Clear();
            }

            NativeMethods.ExtendedLimitInformation information = default;
            information.BasicLimitInformation.LimitFlags =
                NativeMethods.JobObjectLimitKillOnJobClose;
            Marshal.StructureToPtr(information, buffer, false);
            if (NativeMethods.SetInformationJobObject(
                    job,
                    NativeMethods.JobObjectExtendedLimitInformation,
                    buffer,
                    checked((uint)size)) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "SetInformationJobObject failed");
            }

            unsafe
            {
                new Span<byte>((void*)buffer, size).Clear();
            }

            if (NativeMethods.QueryInformationJobObject(
                    job,
                    NativeMethods.JobObjectExtendedLimitInformation,
                    buffer,
                    checked((uint)size),
                    out uint returned) == 0 || returned != size)
            {
                throw NativeMethods.Win32Failure(
                    "QueryInformationJobObject limit readback failed");
            }

            NativeMethods.ExtendedLimitInformation applied =
                Marshal.PtrToStructure<NativeMethods.ExtendedLimitInformation>(
                    buffer);
            // Only fields selected by LimitFlags are active. Exact equality
            // rejects breakaway and every other limit policy; ungated fields
            // may contain documented system defaults on readback.
            if (applied.BasicLimitInformation.LimitFlags !=
                    NativeMethods.JobObjectLimitKillOnJobClose)
            {
                throw new SecurityException(
                    "The Job Object did not retain the exact kill-on-close policy.");
            }

            return job;
        }
        catch
        {
            job.Dispose();
            throw;
        }
        finally
        {
            unsafe
            {
                new Span<byte>((void*)buffer, size).Clear();
            }

            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static nint CreateJobAttributeList(
        NativeMethods.SafeJobHandle job,
        out nint jobValue,
        out bool initialized)
    {
        initialized = false;
        jobValue = 0;
        nuint size = 0;
        _ = NativeMethods.InitializeProcThreadAttributeList(0, 1, 0, ref size);
        int sizingError = Marshal.GetLastWin32Error();
        if (size == 0 || sizingError != 122)
        {
            throw new Win32Exception(
                sizingError,
                "InitializeProcThreadAttributeList sizing failed");
        }

        nint list = Marshal.AllocHGlobal(checked((nint)size));
        try
        {
            unsafe
            {
                new Span<byte>((void*)list, checked((int)size)).Clear();
            }

            if (NativeMethods.InitializeProcThreadAttributeList(
                    list,
                    1,
                    0,
                    ref size) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "InitializeProcThreadAttributeList failed");
            }

            initialized = true;
            jobValue = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(jobValue, job.DangerousGetHandle());
            if (NativeMethods.UpdateProcThreadAttribute(
                    list,
                    0,
                    NativeMethods.ProcThreadAttributeJobList,
                    jobValue,
                    checked((nuint)IntPtr.Size),
                    0,
                    0) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "UpdateProcThreadAttribute JOB_LIST failed");
            }

            return list;
        }
        catch
        {
            if (initialized)
            {
                NativeMethods.DeleteProcThreadAttributeList(list);
                initialized = false;
            }

            Marshal.FreeHGlobal(list);
            if (jobValue != 0)
            {
                Marshal.FreeHGlobal(jobValue);
                jobValue = 0;
            }

            throw;
        }
    }

    internal static unsafe void RequireProcessInExactJob(
        NativeMethods.SafeJobHandle job,
        NativeMethods.SafeProcessHandle process,
        uint processId)
    {
        if (NativeMethods.IsProcessInJob(process, job, out int contained) == 0)
        {
            throw NativeMethods.Win32Failure("IsProcessInJob failed");
        }

        if (contained == 0)
        {
            throw new SecurityException(
                "The suspended process was not assigned to the expected Job Object.");
        }

        int size = checked(sizeof(uint) * 2 +
            IntPtr.Size * MaximumJobProcessEntries);
        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            new Span<byte>((void*)buffer, size).Clear();
            if (NativeMethods.QueryInformationJobObject(
                    job,
                    NativeMethods.JobObjectBasicProcessIdList,
                    buffer,
                    checked((uint)size),
                    out uint returned) == 0 ||
                returned < sizeof(uint) * 2 + IntPtr.Size ||
                returned > size)
            {
                throw NativeMethods.Win32Failure(
                    "QueryInformationJobObject process list failed");
            }

            uint assigned = unchecked((uint)Marshal.ReadInt32(buffer, 0));
            uint listed = unchecked((uint)Marshal.ReadInt32(buffer, sizeof(uint)));
            if (assigned != 1 || listed != 1)
            {
                throw new SecurityException(
                    "The Job Object did not contain exactly one process.");
            }

            nuint listedId = IntPtr.Size == 8
                ? checked((nuint)unchecked((ulong)Marshal.ReadInt64(
                    buffer,
                    sizeof(uint) * 2)))
                : checked((nuint)unchecked((uint)Marshal.ReadInt32(
                    buffer,
                    sizeof(uint) * 2)));
            if (listedId != processId)
            {
                throw new SecurityException(
                    "The Job Object process identity did not match.");
            }
        }
        finally
        {
            new Span<byte>((void*)buffer, size).Clear();
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static Exception? CleanupFailedStart(
        NativeMethods.SafeJobHandle? job,
        NativeMethods.SafeProcessHandle? process,
        MonotonicDeadline deadline)
    {
        job?.Dispose();
        if (process is null || process.IsInvalid)
        {
            return null;
        }

        try
        {
            // Failure cleanup is non-abandonable and has its own bound. The
            // launch deadline may be the reason this path was entered.
            _ = deadline;
            uint result = NativeMethods.WaitForSingleObject(process, 5_000);
            if (result == NativeMethods.WaitObject0)
            {
                return null;
            }

            if (result == NativeMethods.WaitTimeout)
            {
                return new TimeoutException(
                    "The failed contained process did not exit within its deadline.");
            }

            return NativeMethods.Win32Failure("WaitForSingleObject failed");
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    internal static async Task WaitForSignalAsync(
        NativeMethods.SafeProcessHandle process,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TimeSpan remaining = deadline.GetRemaining();
            uint slice = Math.Min(ToWaitMilliseconds(remaining), 50u);
            uint wait = NativeMethods.WaitForSingleObject(process, slice);
            if (wait == NativeMethods.WaitObject0)
            {
                CheckOperation(deadline, cancellationToken);
                return;
            }

            if (wait != NativeMethods.WaitTimeout)
            {
                throw NativeMethods.Win32Failure("WaitForSingleObject failed");
            }

            await Task.Yield();
        }
    }

    private static uint ToWaitMilliseconds(TimeSpan remaining)
    {
        double value = Math.Ceiling(remaining.TotalMilliseconds);
        return value >= uint.MaxValue ? uint.MaxValue - 1 :
            Math.Max(1u, checked((uint)value));
    }

    internal static void CheckOperation(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.GetRemaining();
    }

    private static string ValidateExecutablePath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        if (!Path.IsPathFullyQualified(value) ||
            value.Length < 4 ||
            !char.IsAsciiLetter(value[0]) ||
            value[1] != ':' ||
            value[2] != Path.DirectorySeparatorChar ||
            value.Contains(Path.AltDirectorySeparatorChar) ||
            value.IndexOf(':', 2) >= 0 ||
            !string.Equals(Path.GetFullPath(value), value,
                StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(value))
        {
            throw new ArgumentException(
                "The harness executable path must be one canonical absolute DOS file path.",
                nameof(value));
        }

        string currentExecutable = Environment.ProcessPath ??
            throw new InvalidOperationException(
                "The current harness executable path is unavailable.");
        if (!string.Equals(value, currentExecutable, StringComparison.Ordinal) ||
            !string.Equals(
                Path.GetExtension(value),
                ".exe",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                Path.GetFileName(value),
                "dotnet.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                "The containment primitive launches only its current harness apphost.");
        }

        return value;
    }

    internal static string QuoteCommandPart(string value)
    {
        if (value.Contains('"'))
        {
            throw new ArgumentException("The executable path contains a quote.");
        }

        return "\"" + value + "\"";
    }
}
