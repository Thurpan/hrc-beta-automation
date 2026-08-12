using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Holds one process object open so its PID cannot be silently rebound to a
/// later process while the binding is in use.
/// </summary>
internal sealed class ProcessIdentityLease : IDisposable
{
    private const int MaximumImagePathChars = 32_768;
    private const uint MaximumTokenInformationBytes = 65_536;
    private NativeMethods.SafeProcessHandle? process;

    private ProcessIdentityLease(
        NativeMethods.SafeProcessHandle process,
        uint processId,
        ulong creationTimeFileTime,
        string imagePath,
        string userSid,
        string logonSid,
        uint tokenSessionId,
        uint processSessionId)
    {
        this.process = process;
        ProcessId = processId;
        CreationTimeFileTime = creationTimeFileTime;
        ImagePath = imagePath;
        UserSid = userSid;
        LogonSid = logonSid;
        TokenSessionId = tokenSessionId;
        ProcessSessionId = processSessionId;
    }

    internal uint ProcessId { get; }

    internal ulong CreationTimeFileTime { get; }

    internal string ImagePath { get; }

    internal string UserSid { get; }

    internal string LogonSid { get; }

    internal uint TokenSessionId { get; }

    internal uint ProcessSessionId { get; }

    internal static ProcessIdentityLease Capture(uint processId)
    {
        if (processId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        nint rawProcess = NativeMethods.OpenProcess(
            NativeMethods.ProcessQueryLimitedInformation | NativeMethods.Synchronize,
            0,
            processId);
        NativeMethods.SafeProcessHandle process = new(rawProcess);
        if (process.IsInvalid)
        {
            process.Dispose();
            throw NativeMethods.Win32Failure("OpenProcess failed");
        }

        try
        {
            ulong creation = ReadCreationTime(process);
            string imagePath = ReadImagePath(process);
            if (!Path.IsPathFullyQualified(imagePath))
            {
                throw new SecurityException("The process image path is not absolute.");
            }

            if (NativeMethods.OpenProcessToken(
                    process,
                    NativeMethods.TokenQuery,
                    out nint rawToken) == 0)
            {
                throw NativeMethods.Win32Failure("OpenProcessToken failed");
            }

            NativeMethods.SafeTokenHandle token = new(rawToken);
            using (token)
            {
                string userSid = ReadUserSid(token);
                string logonSid = ReadLogonSid(token);
                uint tokenSession = ReadTokenSessionId(token);
                if (NativeMethods.ProcessIdToSessionId(
                        processId,
                        out uint processSession) == 0)
                {
                    throw NativeMethods.Win32Failure(
                        "ProcessIdToSessionId failed");
                }

                if (tokenSession != processSession)
                {
                    throw new SecurityException(
                        "The token and process session identifiers differ.");
                }

                return new ProcessIdentityLease(
                    process,
                    processId,
                    creation,
                    imagePath,
                    userSid,
                    logonSid,
                    tokenSession,
                    processSession);
            }
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    internal BootstrapBinding Snapshot()
    {
        EnsureStillAlive();
        return new BootstrapBinding(
            ProcessId,
            CreationTimeFileTime,
            ImagePath,
            UserSid,
            LogonSid,
            TokenSessionId,
            ProcessSessionId);
    }

    internal void EnsureStillAlive()
    {
        NativeMethods.SafeProcessHandle handle = process ??
            throw new ObjectDisposedException(nameof(ProcessIdentityLease));
        uint wait = NativeMethods.WaitForSingleObject(handle, 0);
        if (wait == NativeMethods.WaitObject0)
        {
            throw new InvalidOperationException("The bound process has exited.");
        }

        if (wait != NativeMethods.WaitTimeout)
        {
            throw NativeMethods.Win32Failure("WaitForSingleObject failed");
        }

        if (ReadCreationTime(handle) != CreationTimeFileTime)
        {
            throw new SecurityException("The process creation identity changed.");
        }
    }

    internal bool Matches(BootstrapBinding expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        EnsureStillAlive();
        return expected.Matches(this);
    }

    public void Dispose()
    {
        NativeMethods.SafeProcessHandle? handle = process;
        process = null;
        handle?.Dispose();
    }

    private static ulong ReadCreationTime(NativeMethods.SafeProcessHandle process)
    {
        if (NativeMethods.GetProcessTimes(
                process,
                out NativeMethods.FileTime creation,
                out _,
                out _,
                out _) == 0)
        {
            throw NativeMethods.Win32Failure("GetProcessTimes failed");
        }

        ulong value = creation.ToUInt64();
        if (value == 0)
        {
            throw new SecurityException("The process creation time is invalid.");
        }

        return value;
    }

    private static unsafe string ReadImagePath(
        NativeMethods.SafeProcessHandle process)
    {
        char* buffer = stackalloc char[MaximumImagePathChars];
        uint length = MaximumImagePathChars;
        if (NativeMethods.QueryFullProcessImageName(
                process,
                0,
                buffer,
                ref length) == 0)
        {
            throw NativeMethods.Win32Failure(
                "QueryFullProcessImageName failed");
        }

        if (length == 0 || length >= MaximumImagePathChars)
        {
            throw new SecurityException("The process image path is invalid.");
        }

        return new string(buffer, 0, checked((int)length));
    }

    private static string ReadUserSid(NativeMethods.SafeTokenHandle token)
    {
        return WithTokenInformation(
            token,
            NativeMethods.TokenUser,
            pointer =>
            {
                NativeMethods.TokenUserValue user =
                    Marshal.PtrToStructure<NativeMethods.TokenUserValue>(pointer);
                return SidToString(user.User.Sid);
            });
    }

    private static string ReadLogonSid(NativeMethods.SafeTokenHandle token)
    {
        return WithTokenInformation(
            token,
            NativeMethods.TokenLogonSid,
            pointer =>
            {
                NativeMethods.TokenGroupsFirst groups =
                    Marshal.PtrToStructure<NativeMethods.TokenGroupsFirst>(pointer);
                if (groups.GroupCount != 1 || groups.FirstGroup.Sid == 0 ||
                    (groups.FirstGroup.Attributes & NativeMethods.SeGroupLogonId) !=
                    NativeMethods.SeGroupLogonId)
                {
                    throw new SecurityException("The token logon SID is invalid.");
                }

                return SidToString(groups.FirstGroup.Sid);
            });
    }

    private static uint ReadTokenSessionId(
        NativeMethods.SafeTokenHandle token)
    {
        return WithTokenInformation(
            token,
            NativeMethods.TokenSessionId,
            pointer => unchecked((uint)Marshal.ReadInt32(pointer)));
    }

    private static T WithTokenInformation<T>(
        NativeMethods.SafeTokenHandle token,
        int informationClass,
        Func<nint, T> reader)
    {
        _ = NativeMethods.GetTokenInformation(
            token,
            informationClass,
            0,
            0,
            out uint required);
        int error = Marshal.GetLastWin32Error();
        if (required == 0 || required > MaximumTokenInformationBytes || error != 122)
        {
            throw new Win32Exception(error, "GetTokenInformation sizing failed");
        }

        nint buffer = Marshal.AllocHGlobal(checked((int)required));
        try
        {
            unsafe
            {
                new Span<byte>((void*)buffer, checked((int)required)).Clear();
            }

            if (NativeMethods.GetTokenInformation(
                    token,
                    informationClass,
                    buffer,
                    required,
                    out uint written) == 0 ||
                written == 0 || written > required)
            {
                throw NativeMethods.Win32Failure(
                    "GetTokenInformation failed");
            }

            return reader(buffer);
        }
        finally
        {
            unsafe
            {
                new Span<byte>((void*)buffer, checked((int)required)).Clear();
            }

            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string SidToString(nint sid)
    {
        if (sid == 0 || NativeMethods.ConvertSidToStringSid(
                sid,
                out nint stringSid) == 0)
        {
            throw NativeMethods.Win32Failure("ConvertSidToStringSid failed");
        }

        try
        {
            string value = Marshal.PtrToStringUni(stringSid) ??
                throw new SecurityException("The SID string is null.");
            if (!value.StartsWith("S-1-", StringComparison.Ordinal))
            {
                throw new SecurityException("The SID string is invalid.");
            }

            return value;
        }
        finally
        {
            _ = NativeMethods.LocalFree(stringSid);
        }
    }
}
