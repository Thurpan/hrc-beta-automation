using System;
using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
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
    internal static readonly TimeSpan MaximumOperationTime =
        TimeSpan.FromSeconds(30);
    private const int PipeBufferBytes = MaximumFrameBytes + sizeof(int);
    private const string PipePrefix = "hrc-job-observer-bootstrap-";
    private readonly object lifecycleGate = new();
    private CancellationTokenSource? lifetimeCancellation = new();
    private NamedPipeServerStream? pipe;
    private ProcessIdentityLease? peer;
    private bool disposed;
    private int acceptStarted;
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

    internal Task AcceptAndAuthenticateAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return AcceptAndAuthenticateAsync(
            timeout,
            cancellationToken,
            authenticationTestHook: null);
    }

    internal async Task AcceptAndAuthenticateAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<CancellationToken>? authenticationTestHook)
    {
        ValidateTimeout(timeout);
        (NamedPipeServerStream stream, CancellationToken lifetimeToken) =
            GetAcceptState();
        if (Interlocked.Exchange(ref acceptStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The one-shot accept operation was already started.");
        }

        using CancellationTokenSource operation = CreateTimeout(
            timeout,
            cancellationToken,
            lifetimeToken);
        ProcessIdentityLease? candidate = null;
        try
        {
            await stream.WaitForConnectionAsync(operation.Token)
                .ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();

            uint clientProcessId = GetClientProcessId(stream.SafePipeHandle);
            operation.Token.ThrowIfCancellationRequested();

            candidate = ProcessIdentityLease.Capture(clientProcessId);
            operation.Token.ThrowIfCancellationRequested();
            if (!candidate.Matches(ExpectedPeer))
            {
                throw new SecurityException(
                    "The named-pipe peer identity does not match the binding.");
            }

            operation.Token.ThrowIfCancellationRequested();
            authenticationTestHook?.Invoke(operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            lock (lifecycleGate)
            {
                if (disposed || !ReferenceEquals(pipe, stream))
                {
                    throw new ObjectDisposedException(nameof(ProtectedNamedPipe));
                }

                if (peer is not null)
                {
                    throw new InvalidOperationException(
                        "The pipe already accepted a peer.");
                }

                operation.Token.ThrowIfCancellationRequested();
                peer = candidate;
                candidate = null;
            }
        }
        catch
        {
            Dispose();
            throw;
        }
        finally
        {
            candidate?.Dispose();
        }
    }

    internal Task<byte[]> ReceiveFrameAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ValidateTimeout(timeout);
        (NamedPipeServerStream stream, CancellationToken lifetimeToken) =
            GetAuthenticatedState();
        if (Interlocked.Exchange(ref receiveStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The one-shot receive operation was already started.");
        }

        return ReceiveOnceAsync(
            stream,
            timeout,
            cancellationToken,
            lifetimeToken);
    }

    internal Task SendFrameAsync(
        ReadOnlyMemory<byte> frame,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        PipeFrames.ValidateFrame(frame);
        ValidateTimeout(timeout);
        (NamedPipeServerStream stream, CancellationToken lifetimeToken) =
            GetAuthenticatedState();
        if (Interlocked.Exchange(ref sendStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The one-shot send operation was already started.");
        }

        return SendOnceAsync(
            stream,
            frame,
            timeout,
            cancellationToken,
            lifetimeToken);
    }

    public void Dispose()
    {
        CancellationTokenSource? cancellation;
        ProcessIdentityLease? peerLease;
        NamedPipeServerStream? stream;
        lock (lifecycleGate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            cancellation = lifetimeCancellation;
            lifetimeCancellation = null;
            peerLease = peer;
            peer = null;
            stream = pipe;
            pipe = null;
        }

        try
        {
            cancellation?.Cancel();
        }
        finally
        {
            stream?.Dispose();
            peerLease?.Dispose();
            cancellation?.Dispose();
        }
    }

    internal static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 120 || !PipeNamePattern().IsMatch(name))
        {
            throw new ArgumentException("The named-pipe name is invalid.", nameof(name));
        }
    }

    internal static CancellationTokenSource CreateTimeout(
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        CancellationToken lifetimeToken = default)
    {
        ValidateTimeout(timeout);
        CancellationTokenSource result = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, lifetimeToken);
        result.CancelAfter(timeout);
        return result;
    }

    internal static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout > MaximumOperationTime)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
    }

    internal static uint GetClientProcessId(SafePipeHandle pipeHandle)
    {
        ArgumentNullException.ThrowIfNull(pipeHandle);
        bool handleAdded = false;
        try
        {
            pipeHandle.DangerousAddRef(ref handleAdded);
            if (NativeMethods.GetNamedPipeClientProcessId(
                    pipeHandle.DangerousGetHandle(),
                    out uint clientProcessId) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "GetNamedPipeClientProcessId failed");
            }

            return clientProcessId;
        }
        finally
        {
            if (handleAdded)
            {
                pipeHandle.DangerousRelease();
            }
        }
    }

    internal static uint GetServerProcessId(SafePipeHandle pipeHandle)
    {
        ArgumentNullException.ThrowIfNull(pipeHandle);
        bool handleAdded = false;
        try
        {
            pipeHandle.DangerousAddRef(ref handleAdded);
            if (NativeMethods.GetNamedPipeServerProcessId(
                    pipeHandle.DangerousGetHandle(),
                    out uint serverProcessId) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "GetNamedPipeServerProcessId failed");
            }

            return serverProcessId;
        }
        finally
        {
            if (handleAdded)
            {
                pipeHandle.DangerousRelease();
            }
        }
    }

    private async Task<byte[]> ReceiveOnceAsync(
        NamedPipeServerStream stream,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        CancellationToken lifetimeToken)
    {
        try
        {
            return await PipeFrames.ReceiveAsync(
                    stream,
                    timeout,
                    cancellationToken,
                    lifetimeToken)
                .ConfigureAwait(false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private async Task SendOnceAsync(
        NamedPipeServerStream stream,
        ReadOnlyMemory<byte> frame,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        CancellationToken lifetimeToken)
    {
        try
        {
            await PipeFrames.SendAsync(
                    stream,
                    frame,
                    timeout,
                    cancellationToken,
                    lifetimeToken)
                .ConfigureAwait(false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private (NamedPipeServerStream Stream, CancellationToken LifetimeToken)
        GetAcceptState()
    {
        lock (lifecycleGate)
        {
            if (disposed || pipe is null || lifetimeCancellation is null)
            {
                throw new ObjectDisposedException(nameof(ProtectedNamedPipe));
            }

            if (pipe.IsConnected || peer is not null)
            {
                throw new InvalidOperationException(
                    "The pipe already accepted a peer.");
            }

            return (pipe, lifetimeCancellation.Token);
        }
    }

    private (NamedPipeServerStream Stream, CancellationToken LifetimeToken)
        GetAuthenticatedState()
    {
        lock (lifecycleGate)
        {
            if (disposed || pipe is null || lifetimeCancellation is null)
            {
                throw new InvalidOperationException(
                    "The named-pipe peer is not authenticated.");
            }

            if (peer is null || !pipe.IsConnected)
            {
                throw new InvalidOperationException(
                    "The named-pipe peer is not authenticated.");
            }

            peer.EnsureStillAlive();
            if (!peer.Matches(ExpectedPeer))
            {
                throw new SecurityException(
                    "The named-pipe peer identity changed.");
            }

            return (pipe, lifetimeCancellation.Token);
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
        ArgumentNullException.ThrowIfNull(pipeHandle);
        uint result;
        nint owner;
        nint group;
        nint dacl;
        nint sacl;
        nint securityDescriptor;
        bool handleAdded = false;
        try
        {
            pipeHandle.DangerousAddRef(ref handleAdded);
            result = NativeMethods.GetSecurityInfo(
                pipeHandle.DangerousGetHandle(),
                NativeMethods.SeKernelObject,
                NativeMethods.DaclSecurityInformation,
                out owner,
                out group,
                out dacl,
                out sacl,
                out securityDescriptor);
        }
        finally
        {
            if (handleAdded)
            {
                pipeHandle.DangerousRelease();
            }
        }

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
    private readonly object lifecycleGate = new();
    private CancellationTokenSource? lifetimeCancellation = new();
    private NamedPipeClientStream? pipe;
    private ProcessIdentityLease? peer;
    private readonly BootstrapBinding expectedPeer;
    private bool disposed;
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
        BootstrapBinding expectedServer,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return ConnectAsync(
                pipeName,
                expectedServer,
                timeout,
                cancellationToken)
            .GetAwaiter()
            .GetResult();
    }

    internal static async Task<ProtectedNamedPipeClient> ConnectAsync(
        string pipeName,
        BootstrapBinding expectedServer,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        return await ConnectAsync(
                pipeName,
                expectedServer,
                timeout,
                cancellationToken,
                authenticationTestHook: null)
            .ConfigureAwait(false);
    }

    internal static async Task<ProtectedNamedPipeClient> ConnectAsync(
        string pipeName,
        BootstrapBinding expectedServer,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        Action<CancellationToken>? authenticationTestHook)
    {
        ProtectedNamedPipe.ValidateName(pipeName);
        ArgumentNullException.ThrowIfNull(expectedServer);
        ProtectedNamedPipe.ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();

        NamedPipeClientStream? stream = new(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Identification,
            HandleInheritability.None);
        ProcessIdentityLease? server = null;
        using CancellationTokenSource operation = ProtectedNamedPipe.CreateTimeout(
            timeout,
            cancellationToken);
        try
        {
            await stream.ConnectAsync(Timeout.Infinite, operation.Token)
                .ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            uint serverProcessId = ProtectedNamedPipe.GetServerProcessId(
                stream.SafePipeHandle);
            operation.Token.ThrowIfCancellationRequested();

            server = ProcessIdentityLease.Capture(serverProcessId);
            operation.Token.ThrowIfCancellationRequested();
            if (!server.Matches(expectedServer))
            {
                throw new SecurityException(
                    "The named-pipe server identity does not match the binding.");
            }

            operation.Token.ThrowIfCancellationRequested();
            authenticationTestHook?.Invoke(operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            ProtectedNamedPipeClient result = new(
                stream,
                server,
                expectedServer);
            stream = null;
            server = null;
            return result;
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested &&
                operation.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Connecting and authenticating the named pipe timed out.",
                exception);
        }
        finally
        {
            server?.Dispose();
            stream?.Dispose();
        }
    }

    internal Task<byte[]> ReceiveFrameAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ProtectedNamedPipe.ValidateTimeout(timeout);
        (NamedPipeClientStream stream, CancellationToken lifetimeToken) =
            GetAuthenticatedState();
        if (Interlocked.Exchange(ref receiveStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The one-shot receive operation was already started.");
        }

        return ReceiveOnceAsync(
            stream,
            timeout,
            cancellationToken,
            lifetimeToken);
    }

    internal Task SendFrameAsync(
        ReadOnlyMemory<byte> frame,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        PipeFrames.ValidateFrame(frame);
        ProtectedNamedPipe.ValidateTimeout(timeout);
        (NamedPipeClientStream stream, CancellationToken lifetimeToken) =
            GetAuthenticatedState();
        if (Interlocked.Exchange(ref sendStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The one-shot send operation was already started.");
        }

        return SendOnceAsync(
            stream,
            frame,
            timeout,
            cancellationToken,
            lifetimeToken);
    }

    public void Dispose()
    {
        CancellationTokenSource? cancellation;
        ProcessIdentityLease? peerLease;
        NamedPipeClientStream? stream;
        lock (lifecycleGate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            cancellation = lifetimeCancellation;
            lifetimeCancellation = null;
            peerLease = peer;
            peer = null;
            stream = pipe;
            pipe = null;
        }

        try
        {
            cancellation?.Cancel();
        }
        finally
        {
            stream?.Dispose();
            peerLease?.Dispose();
            cancellation?.Dispose();
        }
    }

    private (NamedPipeClientStream Stream, CancellationToken LifetimeToken)
        GetAuthenticatedState()
    {
        lock (lifecycleGate)
        {
            if (disposed || peer is null || pipe is null ||
                lifetimeCancellation is null || !pipe.IsConnected)
            {
                throw new InvalidOperationException(
                    "The named-pipe peer is not authenticated.");
            }

            peer.EnsureStillAlive();
            if (!peer.Matches(expectedPeer))
            {
                throw new SecurityException(
                    "The named-pipe peer identity changed.");
            }

            return (pipe, lifetimeCancellation.Token);
        }
    }

    private NamedPipeClientStream GetPipe()
    {
        return pipe ?? throw new ObjectDisposedException(
            nameof(ProtectedNamedPipeClient));
    }

    private async Task<byte[]> ReceiveOnceAsync(
        NamedPipeClientStream stream,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        CancellationToken lifetimeToken)
    {
        try
        {
            return await PipeFrames.ReceiveAsync(
                    stream,
                    timeout,
                    cancellationToken,
                    lifetimeToken)
                .ConfigureAwait(false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private async Task SendOnceAsync(
        NamedPipeClientStream stream,
        ReadOnlyMemory<byte> frame,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        CancellationToken lifetimeToken)
    {
        try
        {
            await PipeFrames.SendAsync(
                    stream,
                    frame,
                    timeout,
                    cancellationToken,
                    lifetimeToken)
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
        TimeSpan timeout,
        CancellationToken cancellationToken,
        CancellationToken lifetimeToken)
    {
        ValidateFrame(frame);

        byte[] output = new byte[sizeof(int) + frame.Length];
        try
        {
            BinaryPrimitives.WriteInt32LittleEndian(output, frame.Length);
            frame.Span.CopyTo(output.AsSpan(sizeof(int)));
            using CancellationTokenSource cancellation =
                ProtectedNamedPipe.CreateTimeout(
                    timeout,
                    cancellationToken,
                    lifetimeToken);
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
        TimeSpan timeout,
        CancellationToken cancellationToken,
        CancellationToken lifetimeToken)
    {
        using CancellationTokenSource cancellation =
            ProtectedNamedPipe.CreateTimeout(
                timeout,
                cancellationToken,
                lifetimeToken);
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
