using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Owns one local first-instance byte pipe. Both endpoints verify the exact
/// peer process identity before they expose bounded frame operations.
/// </summary>
internal sealed partial class ProtectedNamedPipe : IDisposable
{
    internal const int MaximumFrameBytes = 8_192;
    private const int PipeBufferBytes = MaximumFrameBytes + sizeof(int);
    private const string PipePrefix = "hrc-job-observer-bootstrap-";
    private static readonly TimeSpan MaximumOperationTime = TimeSpan.FromSeconds(30);
    private NamedPipeServerStream? pipe;
    private ProcessIdentityLease? peer;
    private int sendStarted;
    private int receiveStarted;

    private ProtectedNamedPipe(
        string name,
        BootstrapBinding expectedPeer,
        NamedPipeServerStream pipe)
    {
        Name = name;
        ExpectedPeer = expectedPeer;
        this.pipe = pipe;
    }

    internal string Name { get; }

    internal BootstrapBinding ExpectedPeer { get; }

    internal static ProtectedNamedPipe Create(BootstrapBinding expectedPeer)
    {
        ArgumentNullException.ThrowIfNull(expectedPeer);
        return Create(PipePrefix + Guid.NewGuid().ToString("N"), expectedPeer);
    }

    internal static ProtectedNamedPipe Create(
        string name,
        BootstrapBinding expectedPeer)
    {
        ArgumentNullException.ThrowIfNull(expectedPeer);
        ValidateName(name);
        string descriptor = "D:P(A;;FA;;;SY)(A;;FA;;;" +
            expectedPeer.UserSid + ")";
        if (NativeMethods.ConvertStringSecurityDescriptor(
                descriptor,
                NativeMethods.SddlRevision1,
                out nint securityDescriptor,
                out uint descriptorSize) == 0 ||
            securityDescriptor == 0 || descriptorSize == 0)
        {
            throw NativeMethods.Win32Failure(
                "ConvertStringSecurityDescriptor failed");
        }

        try
        {
            string canonicalDescriptor = DescriptorToString(securityDescriptor);
            NativeMethods.SecurityAttributes attributes = new()
            {
                Length = checked((uint)Marshal.SizeOf<
                    NativeMethods.SecurityAttributes>()),
                SecurityDescriptor = securityDescriptor,
                InheritHandle = 0,
            };
            nint rawHandle;
            unsafe
            {
                rawHandle = NativeMethods.CreateNamedPipe(
                    FullName(name),
                    NativeMethods.PipeAccessDuplex |
                        NativeMethods.FileFlagFirstPipeInstance |
                        NativeMethods.FileFlagOverlapped,
                    NativeMethods.PipeTypeByte |
                        NativeMethods.PipeReadmodeByte |
                        NativeMethods.PipeWait |
                        NativeMethods.PipeRejectRemoteClients,
                    1,
                    PipeBufferBytes,
                    PipeBufferBytes,
                    0,
                    &attributes);
            }

            SafePipeHandle safeHandle = new(rawHandle, true);
            if (safeHandle.IsInvalid)
            {
                safeHandle.Dispose();
                throw NativeMethods.Win32Failure("CreateNamedPipe failed");
            }

            try
            {
                VerifyAppliedDescriptor(safeHandle, canonicalDescriptor);
                NamedPipeServerStream stream = new(
                    PipeDirection.InOut,
                    true,
                    false,
                    safeHandle);
                return new ProtectedNamedPipe(name, expectedPeer, stream);
            }
            catch
            {
                safeHandle.Dispose();
                throw;
            }
        }
        finally
        {
            _ = NativeMethods.LocalFree(securityDescriptor);
        }
    }

    internal string ReadAppliedDacl()
    {
        return ReadAppliedDescriptor(GetPipe().SafePipeHandle);
    }

    internal async Task AcceptAndAuthenticateAsync(TimeSpan timeout)
    {
        NamedPipeServerStream stream = GetPipe();
        if (stream.IsConnected || peer is not null)
        {
            throw new InvalidOperationException("The pipe already accepted a peer.");
        }

        using CancellationTokenSource cancellation = CreateTimeout(timeout);
        try
        {
            await stream.WaitForConnectionAsync(cancellation.Token)
                .ConfigureAwait(false);
        }
        catch
        {
            Dispose();
            throw;
        }

        ProcessIdentityLease? candidate = null;
        try
        {
            if (NativeMethods.GetNamedPipeClientProcessId(
                    stream.SafePipeHandle.DangerousGetHandle(),
                    out uint clientProcessId) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "GetNamedPipeClientProcessId failed");
            }

            candidate = ProcessIdentityLease.Capture(clientProcessId);
            if (!candidate.Matches(ExpectedPeer))
            {
                throw new SecurityException(
                    "The named-pipe peer identity does not match the binding.");
            }

            peer = candidate;
            candidate = null;
        }
        catch
        {
            stream.Dispose();
            pipe = null;
            throw;
        }
        finally
        {
            candidate?.Dispose();
        }
    }

    internal Task<byte[]> ReceiveFrameAsync(TimeSpan timeout)
    {
        EnsureAuthenticatedPeer();
        ValidateTimeout(timeout);
        if (Interlocked.Exchange(ref receiveStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The one-shot receive operation was already started.");
        }

        return ReceiveOnceAsync(timeout);
    }

    internal Task SendFrameAsync(ReadOnlyMemory<byte> frame, TimeSpan timeout)
    {
        EnsureAuthenticatedPeer();
        PipeFrames.ValidateFrame(frame);
        ValidateTimeout(timeout);
        if (Interlocked.Exchange(ref sendStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The one-shot send operation was already started.");
        }

        return SendOnceAsync(frame, timeout);
    }

    public void Dispose()
    {
        ProcessIdentityLease? peerLease = peer;
        peer = null;
        peerLease?.Dispose();
        NamedPipeServerStream? stream = pipe;
        pipe = null;
        stream?.Dispose();
    }

    internal static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 120 || !PipeNamePattern().IsMatch(name))
        {
            throw new ArgumentException("The named-pipe name is invalid.", nameof(name));
        }
    }

    internal static CancellationTokenSource CreateTimeout(TimeSpan timeout)
    {
        ValidateTimeout(timeout);
        return new CancellationTokenSource(timeout);
    }

    internal static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout > MaximumOperationTime)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
    }

    private async Task<byte[]> ReceiveOnceAsync(TimeSpan timeout)
    {
        try
        {
            return await PipeFrames.ReceiveAsync(GetPipe(), timeout)
                .ConfigureAwait(false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private async Task SendOnceAsync(ReadOnlyMemory<byte> frame, TimeSpan timeout)
    {
        try
        {
            await PipeFrames.SendAsync(GetPipe(), frame, timeout)
                .ConfigureAwait(false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void EnsureAuthenticatedPeer()
    {
        if (peer is null || pipe is null || !pipe.IsConnected)
        {
            throw new InvalidOperationException(
                "The named-pipe peer is not authenticated.");
        }

        peer.EnsureStillAlive();
        if (!peer.Matches(ExpectedPeer))
        {
            throw new SecurityException("The named-pipe peer identity changed.");
        }
    }

    private NamedPipeServerStream GetPipe()
    {
        return pipe ?? throw new ObjectDisposedException(nameof(ProtectedNamedPipe));
    }

    private static string FullName(string name) => "\\\\.\\pipe\\" + name;

    private static void VerifyAppliedDescriptor(
        SafePipeHandle pipeHandle,
        string expectedDescriptor)
    {
        string applied = ReadAppliedDescriptor(pipeHandle);
        if (!string.Equals(
                expectedDescriptor,
                applied,
                StringComparison.Ordinal))
        {
            throw new SecurityException(
                "The applied named-pipe DACL differs from the requested protected DACL.");
        }
    }

    private static string ReadAppliedDescriptor(SafePipeHandle pipeHandle)
    {
        uint result = NativeMethods.GetSecurityInfo(
            pipeHandle.DangerousGetHandle(),
            NativeMethods.SeKernelObject,
            NativeMethods.DaclSecurityInformation,
            out nint owner,
            out nint group,
            out nint dacl,
            out nint sacl,
            out nint securityDescriptor);
        _ = owner;
        _ = group;
        _ = dacl;
        _ = sacl;
        if (result != NativeMethods.ErrorSuccess || securityDescriptor == 0)
        {
            throw new Win32Exception(
                checked((int)result),
                "GetSecurityInfo failed for the named pipe.");
        }

        try
        {
            if (NativeMethods.ConvertSecurityDescriptorToString(
                    securityDescriptor,
                    NativeMethods.SddlRevision1,
                    NativeMethods.DaclSecurityInformation,
                    out nint appliedDescriptor,
                    out uint appliedLength) == 0 ||
                appliedDescriptor == 0 || appliedLength == 0)
            {
                throw NativeMethods.Win32Failure(
                    "Converting the applied pipe descriptor failed");
            }

            try
            {
                string? applied = Marshal.PtrToStringUni(appliedDescriptor);
                return applied ?? throw new SecurityException(
                    "The applied named-pipe DACL was empty.");
            }
            finally
            {
                _ = NativeMethods.LocalFree(appliedDescriptor);
            }
        }
        finally
        {
            _ = NativeMethods.LocalFree(securityDescriptor);
        }
    }

    private static string DescriptorToString(nint securityDescriptor)
    {
        if (NativeMethods.ConvertSecurityDescriptorToString(
                securityDescriptor,
                NativeMethods.SddlRevision1,
                NativeMethods.DaclSecurityInformation,
                out nint stringDescriptor,
                out uint descriptorLength) == 0 ||
            stringDescriptor == 0 || descriptorLength == 0)
        {
            throw NativeMethods.Win32Failure(
                "Canonicalising the pipe descriptor failed");
        }

        try
        {
            return Marshal.PtrToStringUni(stringDescriptor) ??
                throw new SecurityException(
                    "The canonical pipe descriptor was empty.");
        }
        finally
        {
            _ = NativeMethods.LocalFree(stringDescriptor);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PipeNamePattern();
}

internal sealed class ProtectedNamedPipeClient : IDisposable
{
    private NamedPipeClientStream? pipe;
    private ProcessIdentityLease? peer;
    private readonly BootstrapBinding expectedPeer;
    private int sendStarted;
    private int receiveStarted;

    private ProtectedNamedPipeClient(
        NamedPipeClientStream pipe,
        ProcessIdentityLease peer,
        BootstrapBinding expectedPeer)
    {
        this.pipe = pipe;
        this.peer = peer;
        this.expectedPeer = expectedPeer;
    }

    internal static ProtectedNamedPipeClient Connect(
        string pipeName,
        BootstrapBinding expectedServer)
    {
        ProtectedNamedPipe.ValidateName(pipeName);
        ArgumentNullException.ThrowIfNull(expectedServer);
        nint rawHandle = NativeMethods.CreateFile(
            "\\\\.\\pipe\\" + pipeName,
            NativeMethods.GenericRead | NativeMethods.GenericWrite,
            0,
            0,
            NativeMethods.OpenExisting,
            NativeMethods.FileFlagOverlapped |
                NativeMethods.SecuritySqosPresent |
                NativeMethods.SecurityIdentification,
            0);
        SafePipeHandle? safeHandle = new(rawHandle, true);
        if (safeHandle.IsInvalid)
        {
            safeHandle.Dispose();
            throw NativeMethods.Win32Failure("Opening the named pipe failed");
        }

        NamedPipeClientStream? stream = null;
        ProcessIdentityLease? server = null;
        try
        {
            stream = new NamedPipeClientStream(
                PipeDirection.InOut,
                true,
                true,
                safeHandle);
            safeHandle = null;
            if (NativeMethods.GetNamedPipeServerProcessId(
                    stream.SafePipeHandle.DangerousGetHandle(),
                    out uint serverProcessId) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "GetNamedPipeServerProcessId failed");
            }

            server = ProcessIdentityLease.Capture(serverProcessId);
            if (!server.Matches(expectedServer))
            {
                throw new SecurityException(
                    "The named-pipe server identity does not match the binding.");
            }

            ProtectedNamedPipeClient result = new(
                stream,
                server,
                expectedServer);
            stream = null;
            server = null;
            return result;
        }
        finally
        {
            server?.Dispose();
            stream?.Dispose();
            safeHandle?.Dispose();
        }
    }

    internal Task<byte[]> ReceiveFrameAsync(TimeSpan timeout)
    {
        EnsureAuthenticatedPeer();
        ProtectedNamedPipe.ValidateTimeout(timeout);
        if (Interlocked.Exchange(ref receiveStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The one-shot receive operation was already started.");
        }

        return ReceiveOnceAsync(timeout);
    }

    internal Task SendFrameAsync(ReadOnlyMemory<byte> frame, TimeSpan timeout)
    {
        EnsureAuthenticatedPeer();
        PipeFrames.ValidateFrame(frame);
        ProtectedNamedPipe.ValidateTimeout(timeout);
        if (Interlocked.Exchange(ref sendStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The one-shot send operation was already started.");
        }

        return SendOnceAsync(frame, timeout);
    }

    public void Dispose()
    {
        ProcessIdentityLease? peerLease = peer;
        peer = null;
        peerLease?.Dispose();
        NamedPipeClientStream? stream = pipe;
        pipe = null;
        stream?.Dispose();
    }

    private void EnsureAuthenticatedPeer()
    {
        if (peer is null || pipe is null || !pipe.IsConnected)
        {
            throw new InvalidOperationException(
                "The named-pipe peer is not authenticated.");
        }

        peer.EnsureStillAlive();
        if (!peer.Matches(expectedPeer))
        {
            throw new SecurityException("The named-pipe peer identity changed.");
        }
    }

    private NamedPipeClientStream GetPipe()
    {
        return pipe ?? throw new ObjectDisposedException(
            nameof(ProtectedNamedPipeClient));
    }

    private async Task<byte[]> ReceiveOnceAsync(TimeSpan timeout)
    {
        try
        {
            return await PipeFrames.ReceiveAsync(GetPipe(), timeout)
                .ConfigureAwait(false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private async Task SendOnceAsync(ReadOnlyMemory<byte> frame, TimeSpan timeout)
    {
        try
        {
            await PipeFrames.SendAsync(GetPipe(), frame, timeout)
                .ConfigureAwait(false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }
}

internal static class PipeFrames
{
    internal static async Task SendAsync(
        Stream pipe,
        ReadOnlyMemory<byte> frame,
        TimeSpan timeout)
    {
        ValidateFrame(frame);

        byte[] output = new byte[sizeof(int) + frame.Length];
        try
        {
            BinaryPrimitives.WriteInt32LittleEndian(output, frame.Length);
            frame.Span.CopyTo(output.AsSpan(sizeof(int)));
            using CancellationTokenSource cancellation =
                ProtectedNamedPipe.CreateTimeout(timeout);
            await pipe.WriteAsync(output, cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            Array.Clear(output);
        }
    }

    internal static void ValidateFrame(ReadOnlyMemory<byte> frame)
    {
        if (frame.Length < 1 || frame.Length > ProtectedNamedPipe.MaximumFrameBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }
    }

    internal static async Task<byte[]> ReceiveAsync(
        Stream pipe,
        TimeSpan timeout)
    {
        using CancellationTokenSource cancellation =
            ProtectedNamedPipe.CreateTimeout(timeout);
        byte[] prefix = new byte[sizeof(int)];
        try
        {
            await pipe.ReadExactlyAsync(prefix, cancellation.Token).ConfigureAwait(false);
            int length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
            if (length < 1 || length > ProtectedNamedPipe.MaximumFrameBytes)
            {
                throw new SecurityException(
                    "The named-pipe frame length is invalid.");
            }

            byte[] frame = new byte[length];
            try
            {
                await pipe.ReadExactlyAsync(frame, cancellation.Token)
                    .ConfigureAwait(false);
                return frame;
            }
            catch
            {
                Array.Clear(frame);
                throw;
            }
        }
        finally
        {
            Array.Clear(prefix);
        }
    }
}
