using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal static class Program
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HarnessTimeout = TimeSpan.FromSeconds(20);
    private const string ChildMode = "--cross-process-child";
    private const byte ChildConnect = 1;
    private const byte ChildExpectServerRejection = 2;
    private const byte ChildExit = 3;
    private const int DescriptorClaimPipeOffset = 118;
    private const int ProtocolHeaderLength = 16;
    private static readonly byte[] SyntheticRequest = { 0x31, 0x41, 0x59 };
    private static readonly byte[] SyntheticResponse = { 0x26, 0x53, 0x58 };
    private static readonly byte[] DescriptorAuthenticationDomain =
        Encoding.ASCII.GetBytes(
            "HRC-BETA-OBSERVER-BOOTSTRAP-DESCRIPTOR-HMAC-V1\0");
    private static readonly byte[] ReceiptAuthenticationDomain =
        Encoding.ASCII.GetBytes(
            "HRC-BETA-OBSERVER-CLAIM-RECEIPT-HMAC-V1\0");

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 &&
            string.Equals(args[0], ChildMode, StringComparison.Ordinal))
        {
            return await RunCrossProcessChild().ConfigureAwait(false);
        }

        if (args.Length != 0)
        {
            return 2;
        }

        TestCase[] tests =
        {
            new("process identity captures the current process", TestCurrentProcessIdentity),
            new("process identity rejects an invalid PID", TestInvalidProcessIdentity),
            new("secret generation and disposal", TestSecretBuffer),
            new("bootstrap binding validates exact identity", TestBootstrapBinding),
            new("bootstrap binding rejects every identity mismatch", TestBindingMismatches),
            new("protected pipe exchanges bounded frames", TestProtectedPipeRoundTrip),
            new("protected pipe rejects a second first instance", TestFirstInstanceCollision),
            new("protected pipe rejects a mismatched identity", TestMismatchedPipeIdentity),
            new("protected pipe client rejects a mismatched server", TestMismatchedServerIdentity),
            new("protected pipe accept timeout poisons the channel", TestAcceptTimeout),
            new("protected pipe client connect is bounded and cancellable", TestClientConnectBounds),
            new("protected pipe authentication stays within its operation bound", TestAuthenticationBounds),
            new("protected pipe disposal drains accept and releases its name", TestDisposeDuringAccept),
            new("protected pipe disposal drains receive on both endpoints", TestDisposeDuringReceive),
            new("protected pipe operations time out and poison the channel", TestPipeTimeout),
            new("protected pipe rejects malformed receive frames", TestMalformedReceiveFrames),
            new("protected pipe applies an exact protected DACL", TestAppliedPipeDacl),
            new("protected pipe rejects invalid frames", TestInvalidFrames),
            new("protected pipe authenticates distinct processes", TestCrossProcessIdentity),
            new("protected pipe rejects a different live process", TestCrossProcessMismatch),
            new("descriptor canonical round trip and ownership", TestDescriptorCanonicalRoundTrip),
            new("descriptor authenticates bindings and lifetime", TestDescriptorAuthentication),
            new("descriptor rejects malformed and noncanonical wires", TestDescriptorMalformedWires),
            new("protocol round trips all eight message roles", TestProtocolRoundTrips),
            new("protocol enforces canonical headers and bodies", TestProtocolCanonicalHeaders),
            new("claim receipt proof is domain separated and bound", TestClaimReceiptProof),
            new("protocol rejects malformed semantic fields", TestProtocolMalformedFields),
            new("protocol wipes owned token frames and messages", TestProtocolSecretOwnership),
        };

        using TextWriter output = new StreamWriter(System.Console.OpenStandardOutput())
        {
            AutoFlush = true,
        };
        int passed = 0;
        foreach (TestCase test in tests)
        {
            try
            {
                Task execution = test.Body();
                Task completed = await Task.WhenAny(
                    execution,
                    Task.Delay(HarnessTimeout)).ConfigureAwait(false);
                if (!ReferenceEquals(completed, execution))
                {
                    output.WriteLine(
                        $"FAIL {test.Name}: TimeoutException: Test exceeded the " +
                        $"{HarnessTimeout.TotalSeconds:0}-second harness limit.");
                    output.WriteLine($"PASS {passed}/{tests.Length}");
                    return 1;
                }

                await execution.ConfigureAwait(false);
                output.WriteLine($"PASS {test.Name}");
                passed++;
            }
            catch (Exception exception)
            {
                output.WriteLine(
                    $"FAIL {test.Name}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        output.WriteLine($"PASS {passed}/{tests.Length}");
        return passed == tests.Length ? 0 : 1;
    }

    private static Task TestCurrentProcessIdentity()
    {
        uint currentProcessId = checked((uint)Environment.ProcessId);
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(currentProcessId);

        AssertEqual(currentProcessId, identity.ProcessId, "process ID");
        Assert(identity.CreationTimeFileTime > 0, "creation time must be positive");
        Assert(Path.IsPathFullyQualified(identity.ImagePath), "image path must be absolute");
        Assert(!string.IsNullOrWhiteSpace(identity.UserSid), "user SID must be present");
        Assert(!string.IsNullOrWhiteSpace(identity.LogonSid), "logon SID must be present");
        AssertEqual(identity.ProcessSessionId, identity.TokenSessionId, "session IDs");
        identity.EnsureStillAlive();

        identity.Dispose();
        AssertThrows<ObjectDisposedException>(identity.EnsureStillAlive);
        return Task.CompletedTask;
    }

    private static Task TestInvalidProcessIdentity()
    {
        AssertThrowsAny(
            () =>
            {
                using ProcessIdentityLease _ = ProcessIdentityLease.Capture(uint.MaxValue);
            },
            typeof(Win32Exception),
            typeof(InvalidOperationException),
            typeof(ArgumentOutOfRangeException));
        return Task.CompletedTask;
    }

    private static Task TestSecretBuffer()
    {
        using SecretBuffer secret = SecretBuffer.CreateRandom32();
        AssertEqual(32, SecretBuffer.Length, "declared secret length");
        AssertEqual(SecretBuffer.Length, secret.Bytes.Length, "secret length");
        Assert(secret.Bytes.ToArray().Any(value => value != 0), "secret must not be all zero");

        byte[] copy = new byte[SecretBuffer.Length];
        secret.CopyTo(copy);
        Assert(secret.Bytes.SequenceEqual(copy), "CopyTo must preserve the generated bytes");

        ReadOnlySpan<byte> borrowed = secret.Bytes;
        secret.Dispose();
        Assert(AllZero(borrowed), "disposing the secret must wipe its backing buffer");
        AssertThrows<ObjectDisposedException>(
            () =>
            {
                _ = secret.Bytes.Length;
            });
        AssertThrows<ObjectDisposedException>(() => secret.CopyTo(copy));
        Array.Clear(copy);
        return Task.CompletedTask;
    }

    private static Task TestBootstrapBinding()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        BootstrapBinding binding = identity.Snapshot();
        Assert(binding.Matches(identity), "captured binding must match its lease");
        string alternateImageCase = binding.ImagePath.ToUpperInvariant();
        if (string.Equals(
                alternateImageCase,
                binding.ImagePath,
                StringComparison.Ordinal))
        {
            alternateImageCase = binding.ImagePath.ToLowerInvariant();
        }

        Assert(binding.SemanticallyEquals(BindingWith(
                binding,
                imagePath: alternateImageCase)),
            "binding semantics must ignore only image-path case");
        Assert(!binding.SemanticallyEquals(BindingWith(
                binding,
                creation: binding.CreationTimeFileTime + 1)),
            "binding semantics must require the exact creation identity");
        AssertThrows<ArgumentOutOfRangeException>(
            () => _ = new BootstrapBinding(
                0,
                binding.CreationTimeFileTime,
                binding.ImagePath,
                binding.UserSid,
                binding.LogonSid,
                binding.TokenSessionId,
                binding.ProcessSessionId));
        AssertThrows<ArgumentException>(
            () => _ = new BootstrapBinding(
                binding.ProcessId,
                binding.CreationTimeFileTime,
                binding.ImagePath,
                binding.UserSid,
                binding.LogonSid,
                binding.TokenSessionId,
                checked(binding.ProcessSessionId + 1)));
        AssertThrows<ArgumentException>(
            () => _ = new BootstrapBinding(
                binding.ProcessId,
                binding.CreationTimeFileTime,
                "relative.exe",
                binding.UserSid,
                binding.LogonSid,
                binding.TokenSessionId,
                binding.ProcessSessionId));
        AssertThrows<ArgumentException>(
            () => _ = new BootstrapBinding(
                binding.ProcessId,
                binding.CreationTimeFileTime,
                binding.ImagePath,
                binding.UserSid + ")(A;;FA;;;WD",
                binding.LogonSid,
                binding.TokenSessionId,
                binding.ProcessSessionId));
        AssertThrows<ArgumentException>(() =>
            ProtectedNamedPipe.ValidateName("invalid\\pipe"));
        return Task.CompletedTask;
    }

    private static Task TestBindingMismatches()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        BootstrapBinding valid = identity.Snapshot();
        Assert(!BindingWith(valid, processId: valid.ProcessId + 1).Matches(identity),
            "process ID mismatch must reject");
        Assert(!valid.SemanticallyEquals(BindingWith(
                valid,
                processId: valid.ProcessId + 1)),
            "semantic process ID mismatch must reject");
        Assert(!BindingWith(
                valid,
                creation: valid.CreationTimeFileTime + 1).Matches(identity),
            "creation mismatch must reject");
        Assert(!valid.SemanticallyEquals(BindingWith(
                valid,
                creation: valid.CreationTimeFileTime + 1)),
            "semantic creation mismatch must reject");
        Assert(!BindingWith(valid, imagePath: valid.ImagePath + ".other")
                .Matches(identity),
            "image mismatch must reject");
        Assert(!valid.SemanticallyEquals(BindingWith(
                valid,
                imagePath: valid.ImagePath + ".other")),
            "semantic image mismatch must reject");
        Assert(!BindingWith(valid, userSid: "S-1-5-18").Matches(identity),
            "user SID mismatch must reject");
        Assert(!valid.SemanticallyEquals(BindingWith(
                valid,
                userSid: "S-1-5-18")),
            "semantic user SID mismatch must reject");
        Assert(!BindingWith(valid, logonSid: "S-1-5-19").Matches(identity),
            "logon SID mismatch must reject");
        Assert(!valid.SemanticallyEquals(BindingWith(
                valid,
                logonSid: "S-1-5-19")),
            "semantic logon SID mismatch must reject");
        AssertThrows<ArgumentException>(() => _ = BindingWith(
            valid,
            tokenSession: valid.TokenSessionId + 1));
        Assert(!BindingWith(
                valid,
                tokenSession: valid.TokenSessionId + 1,
                processSession: valid.ProcessSessionId + 1).Matches(identity),
            "session mismatch must reject");
        Assert(!valid.SemanticallyEquals(BindingWith(
                valid,
                tokenSession: valid.TokenSessionId + 1,
                processSession: valid.ProcessSessionId + 1)),
            "semantic session mismatch must reject");
        return Task.CompletedTask;
    }

    private static async Task TestProtectedPipeRoundTrip()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        using ProtectedNamedPipe server = ProtectedNamedPipe.Create(identity.Snapshot());
        byte[] request = { 1, 2, 3, 4 };
        byte[] response = { 9, 8, 7 };
        Task client = Task.Run(() =>
        {
            using ProtectedNamedPipeClient connection =
                ProtectedNamedPipeClient.Connect(
                    server.Name,
                    identity.Snapshot(),
                    TestTimeout);
            connection.SendFrameAsync(request, TestTimeout).GetAwaiter().GetResult();
            AssertThrows<InvalidOperationException>(() =>
                connection.SendFrameAsync(request, TestTimeout));
            byte[] received = connection.ReceiveFrameAsync(TestTimeout)
                .GetAwaiter().GetResult();
            try
            {
                Assert(response.SequenceEqual(received), "client response mismatch");
            }
            finally
            {
                Array.Clear(received);
            }

            AssertThrows<InvalidOperationException>(() =>
                connection.ReceiveFrameAsync(TestTimeout));
            connection.Dispose();
            AssertThrows<InvalidOperationException>(() =>
                connection.SendFrameAsync(request, TestTimeout));
        });

        await server.AcceptAndAuthenticateAsync(TestTimeout).ConfigureAwait(false);
        byte[] receivedRequest = await server.ReceiveFrameAsync(TestTimeout)
            .ConfigureAwait(false);
        try
        {
            Assert(request.SequenceEqual(receivedRequest), "server request mismatch");
        }
        finally
        {
            Array.Clear(receivedRequest);
        }

        AssertThrows<InvalidOperationException>(() =>
            server.ReceiveFrameAsync(TestTimeout));

        await server.SendFrameAsync(response, TestTimeout).ConfigureAwait(false);
        AssertThrows<InvalidOperationException>(() =>
            server.SendFrameAsync(response, TestTimeout));
        await client.ConfigureAwait(false);
        Array.Clear(request);
        Array.Clear(response);
    }

    private static Task TestFirstInstanceCollision()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        string name = "hrc-job-observer-bootstrap-test-" + Guid.NewGuid().ToString("N");
        using ProtectedNamedPipe first = ProtectedNamedPipe.Create(
            name,
            identity.Snapshot());
        AssertThrows<Win32Exception>(() =>
        {
            using ProtectedNamedPipe _ = ProtectedNamedPipe.Create(
                name,
                identity.Snapshot());
        });
        return Task.CompletedTask;
    }

    private static async Task TestMismatchedPipeIdentity()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        BootstrapBinding valid = identity.Snapshot();
        BootstrapBinding wrong = new(
            valid.ProcessId,
            valid.CreationTimeFileTime + 1,
            valid.ImagePath,
            valid.UserSid,
            valid.LogonSid,
            valid.TokenSessionId,
            valid.ProcessSessionId);
        using ProtectedNamedPipe server = ProtectedNamedPipe.Create(wrong);
        using ManualResetEventSlim releaseClient = new(false);
        Task client = Task.Run(() =>
        {
            try
            {
                using ProtectedNamedPipeClient _ =
                    ProtectedNamedPipeClient.Connect(
                        server.Name,
                        identity.Snapshot(),
                        TestTimeout);
                if (!releaseClient.Wait(TestTimeout))
                {
                    throw new TimeoutException("Mismatched client was not released.");
                }
            }
            catch (IOException)
            {
                // The server closes immediately after rejecting its binding.
            }
        });
        try
        {
            await AssertThrowsAsync<SecurityException>(
                () => server.AcceptAndAuthenticateAsync(TestTimeout));
        }
        finally
        {
            releaseClient.Set();
        }

        await client.ConfigureAwait(false);
    }

    private static async Task TestMismatchedServerIdentity()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        BootstrapBinding valid = identity.Snapshot();
        BootstrapBinding wrong = new(
            valid.ProcessId,
            valid.CreationTimeFileTime + 1,
            valid.ImagePath,
            valid.UserSid,
            valid.LogonSid,
            valid.TokenSessionId,
            valid.ProcessSessionId);
        using ProtectedNamedPipe server = ProtectedNamedPipe.Create(valid);
        Task accept = server.AcceptAndAuthenticateAsync(TestTimeout);
        AssertThrows<SecurityException>(() =>
        {
            using ProtectedNamedPipeClient _ =
                ProtectedNamedPipeClient.Connect(
                    server.Name,
                    wrong,
                    TestTimeout);
        });
        await accept.ConfigureAwait(false);
    }

    private static async Task TestAcceptTimeout()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        using ProtectedNamedPipe server = ProtectedNamedPipe.Create(identity.Snapshot());
        await AssertThrowsAsync<OperationCanceledException>(() =>
            server.AcceptAndAuthenticateAsync(TimeSpan.FromMilliseconds(20)));
        await AssertThrowsAsync<ObjectDisposedException>(() =>
            server.AcceptAndAuthenticateAsync(TestTimeout));
        AssertThrows<ArgumentOutOfRangeException>(() =>
            ProtectedNamedPipe.ValidateTimeout(TimeSpan.Zero));
        AssertThrows<ArgumentOutOfRangeException>(() =>
            ProtectedNamedPipe.ValidateTimeout(TimeSpan.FromSeconds(31)));
        AssertEqual(
            TimeSpan.FromSeconds(30),
            ProtectedNamedPipe.MaximumOperationTime,
            "maximum pipe-operation duration");
    }

    private static async Task TestClientConnectBounds()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        BootstrapBinding binding = identity.Snapshot();

        string timeoutName = TestPipeName("connect-timeout");
        await AssertThrowsAsync<TimeoutException>(() =>
            ProtectedNamedPipeClient.ConnectAsync(
                timeoutName,
                binding,
                TimeSpan.FromMilliseconds(20)));
        using (ProtectedNamedPipe replacement = ProtectedNamedPipe.Create(
            timeoutName,
            binding))
        {
            AssertEqual(timeoutName, replacement.Name,
                "name after timed-out connect");
            await AssertThrowsAsync<OperationCanceledException>(() =>
                replacement.AcceptAndAuthenticateAsync(
                    TimeSpan.FromMilliseconds(20)));
        }

        string cancellationName = TestPipeName("connect-cancel");
        using CancellationTokenSource cancellation = new();
        Task<ProtectedNamedPipeClient> cancelledConnect =
            ProtectedNamedPipeClient.ConnectAsync(
                cancellationName,
                binding,
                ProtectedNamedPipe.MaximumOperationTime,
                cancellation.Token);
        Assert(!cancelledConnect.IsCompleted,
            "client connect must be pending before caller cancellation");
        cancellation.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(() =>
            cancelledConnect.WaitAsync(TestTimeout));
        using ProtectedNamedPipe cancellationReplacement =
            ProtectedNamedPipe.Create(cancellationName, binding);
        AssertEqual(cancellationName, cancellationReplacement.Name,
            "name after cancelled connect");
        await AssertThrowsAsync<OperationCanceledException>(() =>
            cancellationReplacement.AcceptAndAuthenticateAsync(
                TimeSpan.FromMilliseconds(20)));
    }

    private static async Task TestAuthenticationBounds()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        BootstrapBinding binding = identity.Snapshot();

        string acceptName = TestPipeName("accept-auth-timeout");
        using (ProtectedNamedPipe server = ProtectedNamedPipe.Create(
            acceptName,
            binding))
        using (ManualResetEventSlim authenticationEntered = new(false))
        {
            Task accept = server.AcceptAndAuthenticateAsync(
                TimeSpan.FromSeconds(1),
                CancellationToken.None,
                operationToken =>
                {
                    authenticationEntered.Set();
                    if (!operationToken.WaitHandle.WaitOne(TestTimeout))
                    {
                        throw new TimeoutException(
                            "The accept authentication bound did not expire.");
                    }
                });
            using ProtectedNamedPipeClient client =
                await ProtectedNamedPipeClient.ConnectAsync(
                        acceptName,
                        binding,
                        TestTimeout)
                    .ConfigureAwait(false);
            await AssertThrowsAsync<OperationCanceledException>(() =>
                accept.WaitAsync(TestTimeout));
            Assert(authenticationEntered.IsSet,
                "accept must reach the delayed authentication seam");
            await AssertThrowsAsync<ObjectDisposedException>(() =>
                server.AcceptAndAuthenticateAsync(TestTimeout));
        }

        string connectTimeoutName = TestPipeName("connect-auth-timeout");
        using (ProtectedNamedPipe server = ProtectedNamedPipe.Create(
            connectTimeoutName,
            binding))
        using (ManualResetEventSlim authenticationEntered = new(false))
        {
            Task accept = server.AcceptAndAuthenticateAsync(TestTimeout);
            Task<ProtectedNamedPipeClient> connect =
                ProtectedNamedPipeClient.ConnectAsync(
                    connectTimeoutName,
                    binding,
                    TimeSpan.FromSeconds(1),
                    CancellationToken.None,
                    operationToken =>
                    {
                        authenticationEntered.Set();
                        if (!operationToken.WaitHandle.WaitOne(TestTimeout))
                        {
                            throw new TimeoutException(
                                "The client authentication bound did not expire.");
                        }
                    });
            await accept.ConfigureAwait(false);
            await AssertThrowsAsync<TimeoutException>(() =>
                connect.WaitAsync(TestTimeout));
            Assert(authenticationEntered.IsSet,
                "connect must reach the delayed authentication seam");
        }

        string connectCancellationName = TestPipeName("connect-auth-cancel");
        using (ProtectedNamedPipe server = ProtectedNamedPipe.Create(
            connectCancellationName,
            binding))
        using (ManualResetEventSlim authenticationEntered = new(false))
        using (CancellationTokenSource cancellation = new())
        {
            Task accept = server.AcceptAndAuthenticateAsync(TestTimeout);
            Task<ProtectedNamedPipeClient> connect =
                ProtectedNamedPipeClient.ConnectAsync(
                    connectCancellationName,
                    binding,
                    TestTimeout,
                    cancellation.Token,
                    operationToken =>
                    {
                        authenticationEntered.Set();
                        if (!operationToken.WaitHandle.WaitOne(TestTimeout))
                        {
                            throw new TimeoutException(
                                "The client authentication was not cancelled.");
                        }
                    });
            await accept.ConfigureAwait(false);
            Assert(authenticationEntered.Wait(TestTimeout),
                "connect must enter authentication before cancellation");
            cancellation.Cancel();
            await AssertThrowsAsync<OperationCanceledException>(() =>
                connect.WaitAsync(TestTimeout));
        }
    }

    private static async Task TestDisposeDuringAccept()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        BootstrapBinding binding = identity.Snapshot();
        string name = TestPipeName("dispose-accept");
        using ProtectedNamedPipe server = ProtectedNamedPipe.Create(name, binding);

        Task accept = server.AcceptAndAuthenticateAsync(
            ProtectedNamedPipe.MaximumOperationTime);
        Assert(!accept.IsCompleted,
            "accept must be pending before the owning pipe is disposed");
        server.Dispose();
        await AssertThrowsAnyAsync(
            () => accept.WaitAsync(TestTimeout),
            typeof(OperationCanceledException),
            typeof(ObjectDisposedException),
            typeof(IOException));
        await AssertThrowsAsync<ObjectDisposedException>(() =>
            server.AcceptAndAuthenticateAsync(TestTimeout));

        using ProtectedNamedPipe replacement = ProtectedNamedPipe.Create(
            name,
            binding);
        AssertEqual(name, replacement.Name,
            "name after dispose-during-accept drain");
    }

    private static async Task TestDisposeDuringReceive()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        BootstrapBinding binding = identity.Snapshot();

        string serverName = TestPipeName("dispose-server-receive");
        using (ProtectedNamedPipe server = ProtectedNamedPipe.Create(
            serverName,
            binding))
        {
            Task accept = server.AcceptAndAuthenticateAsync(TestTimeout);
            using ProtectedNamedPipeClient client =
                await ProtectedNamedPipeClient.ConnectAsync(
                        serverName,
                        binding,
                        TestTimeout)
                    .ConfigureAwait(false);
            await accept.ConfigureAwait(false);

            Task<byte[]> receive = server.ReceiveFrameAsync(
                ProtectedNamedPipe.MaximumOperationTime);
            Assert(!receive.IsCompleted,
                "server receive must be pending before disposal");
            server.Dispose();
            await AssertThrowsAnyAsync(
                () => receive.WaitAsync(TestTimeout),
                typeof(OperationCanceledException),
                typeof(ObjectDisposedException),
                typeof(IOException));
            await AssertThrowsAsync<InvalidOperationException>(() =>
                server.ReceiveFrameAsync(TestTimeout));
        }

        using (ProtectedNamedPipe replacement = ProtectedNamedPipe.Create(
            serverName,
            binding))
        {
            AssertEqual(serverName, replacement.Name,
                "name after server receive drain");
        }

        string clientName = TestPipeName("dispose-client-receive");
        using (ProtectedNamedPipe server = ProtectedNamedPipe.Create(
            clientName,
            binding))
        {
            Task accept = server.AcceptAndAuthenticateAsync(TestTimeout);
            using ProtectedNamedPipeClient client =
                await ProtectedNamedPipeClient.ConnectAsync(
                        clientName,
                        binding,
                        TestTimeout)
                    .ConfigureAwait(false);
            await accept.ConfigureAwait(false);

            Task<byte[]> receive = client.ReceiveFrameAsync(
                ProtectedNamedPipe.MaximumOperationTime);
            Assert(!receive.IsCompleted,
                "client receive must be pending before disposal");
            client.Dispose();
            await AssertThrowsAnyAsync(
                () => receive.WaitAsync(TestTimeout),
                typeof(OperationCanceledException),
                typeof(ObjectDisposedException),
                typeof(IOException));
            await AssertThrowsAsync<InvalidOperationException>(() =>
                client.ReceiveFrameAsync(TestTimeout));
        }

        using ProtectedNamedPipe clientReplacement = ProtectedNamedPipe.Create(
            clientName,
            binding);
        AssertEqual(clientName, clientReplacement.Name,
            "name after client receive drain");
    }

    private static async Task TestPipeTimeout()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        using ProtectedNamedPipe server = ProtectedNamedPipe.Create(identity.Snapshot());
        using ManualResetEventSlim releaseClient = new(false);
        Task client = Task.Run(() =>
        {
            using ProtectedNamedPipeClient connection =
                ProtectedNamedPipeClient.Connect(
                    server.Name,
                    identity.Snapshot(),
                    TestTimeout);
            if (!releaseClient.Wait(TestTimeout))
            {
                throw new TimeoutException("The timeout client was not released.");
            }
        });
        await server.AcceptAndAuthenticateAsync(TestTimeout).ConfigureAwait(false);
        try
        {
            await AssertThrowsAsync<OperationCanceledException>(() =>
                server.ReceiveFrameAsync(TimeSpan.FromMilliseconds(20)));
        }
        finally
        {
            releaseClient.Set();
        }

        await client.ConfigureAwait(false);
        await AssertThrowsAsync<InvalidOperationException>(() =>
            server.ReceiveFrameAsync(TestTimeout));
    }

    private static async Task TestMalformedReceiveFrames()
    {
        int[] invalidLengths =
        {
            0,
            -1,
            ProtectedNamedPipe.MaximumFrameBytes + 1,
        };
        foreach (int invalidLength in invalidLengths)
        {
            using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
                checked((uint)Environment.ProcessId));
            using ProtectedNamedPipe server = ProtectedNamedPipe.Create(
                identity.Snapshot());
            Task client = Task.Run(async () =>
            {
                nint rawHandle = NativeMethods.CreateFile(
                    "\\\\.\\pipe\\" + server.Name,
                    NativeMethods.GenericRead | NativeMethods.GenericWrite,
                    0,
                    0,
                    NativeMethods.OpenExisting,
                    NativeMethods.FileFlagOverlapped |
                        NativeMethods.SecuritySqosPresent |
                        NativeMethods.SecurityIdentification,
                    0);
                using SafePipeHandle safeHandle = new(rawHandle, true);
                if (safeHandle.IsInvalid)
                {
                    throw NativeMethods.Win32Failure("Opening the test pipe failed");
                }

                using NamedPipeClientStream stream = new(
                    PipeDirection.InOut,
                    true,
                    true,
                    safeHandle);
                byte[] prefix = BitConverter.GetBytes(invalidLength);
                try
                {
                    await stream.WriteAsync(prefix).ConfigureAwait(false);
                }
                finally
                {
                    Array.Clear(prefix);
                }
            });
            await server.AcceptAndAuthenticateAsync(TestTimeout).ConfigureAwait(false);
            await AssertThrowsAsync<SecurityException>(() =>
                server.ReceiveFrameAsync(TestTimeout));
            await client.ConfigureAwait(false);
            await AssertThrowsAsync<InvalidOperationException>(() =>
                server.ReceiveFrameAsync(TestTimeout));
        }
    }

    private static Task TestAppliedPipeDacl()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        using ProtectedNamedPipe server = ProtectedNamedPipe.Create(identity.Snapshot());
        string applied = server.ReadAppliedDacl();
        Assert(applied.StartsWith("D:P", StringComparison.Ordinal),
            "the applied DACL must be protected");
        Assert(applied.Contains(";;;SY)", StringComparison.Ordinal),
            "the applied DACL must contain SYSTEM");
        Assert(applied.Contains(
                ";;;" + identity.UserSid + ")",
                StringComparison.Ordinal),
            "the applied DACL must contain the current user");
        AssertEqual(2, applied.Count(character => character == '('),
            "applied DACL ACE count");
        string expected = "D:P(A;;FA;;;SY)(A;;FA;;;" + identity.UserSid + ")";
        AssertEqual(expected, applied, "exact applied DACL");
        return Task.CompletedTask;
    }

    private static Task TestDescriptorCanonicalRoundTrip()
    {
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] nonceBefore = fixture.Nonce.ToArray();
        byte[] tokenBefore = fixture.Token.ToArray();
        BootstrapDescriptor descriptor = fixture.CreateDescriptor();

        Assert(fixture.Nonce.SequenceEqual(nonceBefore),
            "descriptor creation must not mutate the nonce source");
        Assert(fixture.Token.SequenceEqual(tokenBefore),
            "descriptor creation must not mutate the token source");
        fixture.Nonce[0] ^= 0x7F;
        Assert(descriptor.Nonce.SequenceEqual(nonceBefore),
            "the descriptor must own an independent nonce copy");
        fixture.Nonce[0] ^= 0x7F;

        byte[] canonical = descriptor.EncodeCanonical();
        byte[] canonicalBackup = canonical.ToArray();
        try
        {
            Assert(canonical.Length >= 209,
                "the shortest valid descriptor must meet the structural lower bound");
            AssertEqual(fixture.Created.ToUnixTimeMilliseconds(),
                BinaryPrimitives.ReadInt64BigEndian(canonical.AsSpan(16, 8)),
                "descriptor creation timestamp wire value");
            AssertEqual(fixture.Expires.ToUnixTimeMilliseconds(),
                BinaryPrimitives.ReadInt64BigEndian(canonical.AsSpan(24, 8)),
                "descriptor expiry timestamp wire value");
            Assert(ExpectedUuidBytes().AsSpan().SequenceEqual(
                    canonical.AsSpan(32, 16)),
                "descriptor publication UUID must use RFC byte order");
            AssertEqual(fixture.Endpoint.Port,
                BinaryPrimitives.ReadUInt16BigEndian(canonical.AsSpan(98, 2)),
                "descriptor endpoint port wire value");

            BootstrapDescriptor parsed = BootstrapDescriptor.Parse(canonical);
            AssertEqual(fixture.PublicationId, parsed.PublicationId,
                "parsed publication identifier");
            AssertEqual(fixture.BrokerInstanceId, parsed.BrokerInstanceId,
                "parsed broker instance identifier");
            AssertEqual(fixture.Endpoint, parsed.Endpoint, "parsed endpoint");
            AssertEqual(fixture.ClaimPipeName, parsed.ClaimPipeName,
                "parsed claim pipe name");
            Assert(parsed.Nonce.SequenceEqual(nonceBefore), "parsed nonce");

            byte[] reencoded = parsed.EncodeCanonical();
            try
            {
                Assert(canonical.SequenceEqual(reencoded),
                    "descriptor parse and re-encode must be byte canonical");
                reencoded[0] ^= 0xFF;
                byte[] independent = parsed.EncodeCanonical();
                try
                {
                    Assert(independent.SequenceEqual(canonicalBackup),
                        "descriptor encodings must not share mutable storage");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(independent);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(reencoded);
            }

            byte[] expectedDigest = SHA256.HashData(canonical);
            byte[] actualDigest = descriptor.ComputeDigest();
            try
            {
                Assert(expectedDigest.SequenceEqual(actualDigest),
                    "descriptor digest must cover the full canonical descriptor");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expectedDigest);
                CryptographicOperations.ZeroMemory(actualDigest);
            }

            BootstrapDescriptor independentParse = BootstrapDescriptor.Parse(canonical);
            CryptographicOperations.ZeroMemory(canonical);
            byte[] afterInputWipe = independentParse.EncodeCanonical();
            try
            {
                Assert(afterInputWipe.SequenceEqual(canonicalBackup),
                    "parsed descriptor must not borrow its encoded input");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(afterInputWipe);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
            CryptographicOperations.ZeroMemory(canonicalBackup);
            CryptographicOperations.ZeroMemory(nonceBefore);
            CryptographicOperations.ZeroMemory(tokenBefore);
        }

        return Task.CompletedTask;
    }

    private static Task TestDescriptorAuthentication()
    {
        using DescriptorFixture fixture = CreateDescriptorFixture();
        BootstrapDescriptor descriptor = fixture.CreateDescriptor();
        byte[] tokenBefore = fixture.Token.ToArray();
        Assert(descriptor.Verify(
                fixture.Token,
                fixture.Observer,
                fixture.Broker,
                fixture.Created,
                fixture.MaximumLifetime),
            "descriptor must verify at its inclusive creation boundary");
        Assert(descriptor.Verify(
                fixture.Token,
                fixture.Observer,
                fixture.Broker,
                fixture.Expires.AddMilliseconds(-1),
                fixture.MaximumLifetime),
            "descriptor must verify immediately before expiry");
        Assert(!descriptor.Verify(
                fixture.Token,
                fixture.Observer,
                fixture.Broker,
                fixture.Created.AddMilliseconds(-1),
                fixture.MaximumLifetime),
            "descriptor must reject a time before creation");
        Assert(!descriptor.Verify(
                fixture.Token,
                fixture.Observer,
                fixture.Broker,
                fixture.Expires,
                fixture.MaximumLifetime),
            "descriptor expiry boundary must be exclusive");
        Assert(!descriptor.Verify(
                fixture.Token,
                fixture.Observer,
                fixture.Broker,
                fixture.Created,
                fixture.MaximumLifetime - TimeSpan.FromMilliseconds(1)),
            "descriptor must reject a caller lifetime shorter than its own");

        byte[] wrongToken = fixture.Token.ToArray();
        wrongToken[0] ^= 0x80;
        try
        {
            Assert(!descriptor.Verify(
                    wrongToken,
                    fixture.Observer,
                    fixture.Broker,
                    fixture.Created,
                    fixture.MaximumLifetime),
                "descriptor must reject the wrong HMAC key");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrongToken);
        }

        Assert(!descriptor.Verify(
                fixture.Token,
                BindingWith(fixture.Observer, processId: fixture.Observer.ProcessId + 1),
                fixture.Broker,
                fixture.Created,
                fixture.MaximumLifetime),
            "descriptor must reject the wrong observer binding");
        Assert(!descriptor.Verify(
                fixture.Token,
                fixture.Observer,
                BindingWith(fixture.Broker, creation: fixture.Broker.CreationTimeFileTime + 1),
                fixture.Created,
                fixture.MaximumLifetime),
            "descriptor must reject the wrong broker binding");

        byte[] canonical = descriptor.EncodeCanonical();
        byte[] authenticated = new byte[
            DescriptorAuthenticationDomain.Length +
            canonical.Length - BootstrapDescriptor.AuthenticationTagLength];
        byte[] expectedTag;
        try
        {
            DescriptorAuthenticationDomain.CopyTo(authenticated, 0);
            canonical.AsSpan(
                    0,
                    canonical.Length - BootstrapDescriptor.AuthenticationTagLength)
                .CopyTo(authenticated.AsSpan(DescriptorAuthenticationDomain.Length));
            expectedTag = HMACSHA256.HashData(fixture.Token, authenticated);
            try
            {
                Assert(expectedTag.AsSpan().SequenceEqual(
                        canonical.AsSpan(
                            canonical.Length - BootstrapDescriptor.AuthenticationTagLength)),
                    "descriptor HMAC must use the documented domain and unsigned wire");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expectedTag);
            }

            int observerOffset = DescriptorObserverBindingOffset(fixture.ClaimPipeName);
            int brokerOffset = observerOffset + BindingWireLength(fixture.Observer);
            int[] semanticOffsets =
            {
                23,
                31,
                47,
                63,
                95,
                99,
                115,
                DescriptorClaimPipeOffset,
                observerOffset + 2,
                brokerOffset + 2,
                canonical.Length - 1,
            };
            foreach (int offset in semanticOffsets)
            {
                byte[] tampered = canonical.ToArray();
                try
                {
                    tampered[offset] ^= 0x01;
                    BootstrapDescriptor parsed = BootstrapDescriptor.Parse(tampered);
                    Assert(!parsed.Verify(
                            fixture.Token,
                            fixture.Observer,
                            fixture.Broker,
                            fixture.Created,
                            fixture.MaximumLifetime),
                        $"descriptor semantic tamper at offset {offset} must not verify");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(tampered);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
            CryptographicOperations.ZeroMemory(authenticated);
            Assert(fixture.Token.SequenceEqual(tokenBefore),
                "descriptor verification must not mutate the caller token");
            CryptographicOperations.ZeroMemory(tokenBefore);
        }

        return Task.CompletedTask;
    }

    private static Task TestDescriptorMalformedWires()
    {
        using DescriptorFixture fixture = CreateDescriptorFixture();
        BootstrapDescriptor descriptor = fixture.CreateDescriptor();
        byte[] canonical = descriptor.EncodeCanonical();
        try
        {
            RequireDescriptorParseFailure(canonical, 0, 0x01, "magic");
            RequireDescriptorParseFailure(canonical, 8, 0x01, "version");
            RequireDescriptorParseFailure(canonical, 9, 0x01, "reserved byte");
            RequireDescriptorParseFailure(canonical, 97, 0x01, "endpoint reserved byte");
            RequireDescriptorZeroFailure(canonical, 32, 16, "publication UUID");
            RequireDescriptorZeroFailure(canonical, 48, 16, "broker UUID");
            RequireDescriptorZeroFailure(canonical, 64, 32, "publication nonce");
            RequireDescriptorZeroFailure(canonical, 98, 2, "endpoint port");
            RequireDescriptorZeroFailure(canonical, 100, 16, "endpoint session UUID");
            RequireDescriptorParseFailure(
                canonical,
                DescriptorClaimPipeOffset,
                0xFF,
                "claim pipe encoding");

            byte[] negativeTime = canonical.ToArray();
            try
            {
                BinaryPrimitives.WriteInt64BigEndian(negativeTime.AsSpan(16, 8), -1);
                AssertThrows<FormatException>(() =>
                    BootstrapDescriptor.Parse(negativeTime));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(negativeTime);
            }

            byte[] reversedTime = canonical.ToArray();
            try
            {
                BinaryPrimitives.WriteInt64BigEndian(
                    reversedTime.AsSpan(24, 8),
                    fixture.Created.ToUnixTimeMilliseconds());
                AssertThrows<FormatException>(() =>
                    BootstrapDescriptor.Parse(reversedTime));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(reversedTime);
            }

            for (int length = 0; length < canonical.Length; length++)
            {
                byte[] truncated = canonical.Take(length).ToArray();
                try
                {
                    AssertThrows<FormatException>(() =>
                        BootstrapDescriptor.Parse(truncated));
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(truncated);
                }
            }

            byte[] trailing = new byte[canonical.Length + 1];
            try
            {
                canonical.CopyTo(trailing, 0);
                AssertThrows<FormatException>(() =>
                    BootstrapDescriptor.Parse(trailing));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(trailing);
            }

            byte[] zeroTag = canonical.ToArray();
            try
            {
                zeroTag.AsSpan(zeroTag.Length - 32).Clear();
                BootstrapDescriptor parsed = BootstrapDescriptor.Parse(zeroTag);
                Assert(!parsed.Verify(
                        fixture.Token,
                        fixture.Observer,
                        fixture.Broker,
                        fixture.Created,
                        fixture.MaximumLifetime),
                    "an all-zero authentication tag is structural but unauthenticated");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(zeroTag);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
        }

        AssertThrows<FormatException>(() => BootstrapDescriptor.Parse(
            new byte[BootstrapDescriptor.MaximumEncodedLength + 1]));
        AssertThrows<ArgumentException>(() => _ = BootstrapDescriptor.Create(
            fixture.Created,
            fixture.Expires,
            fixture.PublicationId,
            fixture.BrokerInstanceId,
            fixture.Nonce,
            fixture.Endpoint,
            fixture.ClaimPipeName,
            BindingWith(fixture.Observer, imagePath: "C:\\e\u0301.exe"),
            fixture.Broker,
            fixture.Token,
            fixture.MaximumLifetime));
        AssertThrows<ArgumentException>(() => _ = BootstrapDescriptor.Create(
            new DateTimeOffset(2026, 8, 12, 12, 0, 0, TimeSpan.FromHours(1)),
            fixture.Expires,
            fixture.PublicationId,
            fixture.BrokerInstanceId,
            fixture.Nonce,
            fixture.Endpoint,
            fixture.ClaimPipeName,
            fixture.Observer,
            fixture.Broker,
            fixture.Token,
            fixture.MaximumLifetime));
        AssertThrows<ArgumentException>(() => _ = BootstrapDescriptor.Create(
            fixture.Created.AddTicks(1),
            fixture.Expires,
            fixture.PublicationId,
            fixture.BrokerInstanceId,
            fixture.Nonce,
            fixture.Endpoint,
            fixture.ClaimPipeName,
            fixture.Observer,
            fixture.Broker,
            fixture.Token,
            fixture.MaximumLifetime));
        AssertThrows<ArgumentOutOfRangeException>(() => _ = BootstrapDescriptor.Create(
            fixture.Created,
            fixture.Created,
            fixture.PublicationId,
            fixture.BrokerInstanceId,
            fixture.Nonce,
            fixture.Endpoint,
            fixture.ClaimPipeName,
            fixture.Observer,
            fixture.Broker,
            fixture.Token,
            fixture.MaximumLifetime));
        RequireDescriptorPeerMismatchRejected(
            fixture,
            BindingWith(fixture.Broker, userSid: "S-1-5-18"),
            "account SID");
        RequireDescriptorPeerMismatchRejected(
            fixture,
            BindingWith(fixture.Broker, logonSid: "S-1-5-19"),
            "logon SID");
        RequireDescriptorPeerMismatchRejected(
            fixture,
            BindingWith(
                fixture.Broker,
                tokenSession: 1,
                processSession: 1),
            "session");
        RequireDescriptorPeerMismatchRejected(
            fixture,
            BindingWith(
                fixture.Broker,
                processId: fixture.Observer.ProcessId,
                creation: fixture.Observer.CreationTimeFileTime),
            "process identity");
        return Task.CompletedTask;
    }

    private static Task TestProtocolRoundTrips()
    {
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] descriptor = fixture.CreateDescriptor().EncodeCanonical();
        byte[] digest = SHA256.HashData(descriptor);
        byte[] controllerNonce = Sequence32(0x41);
        byte[] receiptNonce = Sequence32(0x61);
        byte[] revocationNonce = Sequence32(0x81);
        byte[] token = Sequence32(0xA1);
        Guid requestId = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
        using ClaimReceiptProof proofOwner =
            BootstrapProtocol.ComputeClaimReceiptProof(
                token,
                fixture.PublicationId,
                digest,
                controllerNonce,
                receiptNonce);
        byte[] proof = proofOwner.Bytes.ToArray();
        try
        {
            using (PublishRequest request = new(
                requestId,
                fixture.Nonce,
                fixture.Endpoint,
                token))
            {
                AssertProtocolRoundTrip(
                    BootstrapProtocol.Encode(request),
                    BootstrapMessageType.PublishRequest,
                    BootstrapRole.Observer,
                    BootstrapRole.Broker,
                    value =>
                    {
                        PublishRequest parsed = RequireType<PublishRequest>(value);
                        AssertEqual(requestId, parsed.RequestId, "publish request ID");
                        Assert(parsed.PublicationNonce.SequenceEqual(fixture.Nonce),
                            "publish publication nonce");
                        AssertEqual(fixture.Endpoint, parsed.Endpoint, "publish endpoint");
                        Assert(parsed.Token.Bytes.SequenceEqual(token), "publish token");
                    });
            }

            PublishAck publishAck = new(
                requestId,
                fixture.PublicationId,
                digest,
                descriptor,
                "revoke-pipe");
            AssertProtocolRoundTrip(
                BootstrapProtocol.Encode(publishAck),
                BootstrapMessageType.PublishAck,
                BootstrapRole.Broker,
                BootstrapRole.Observer,
                value =>
                {
                    PublishAck parsed = RequireType<PublishAck>(value);
                    AssertEqual(requestId, parsed.RequestId, "publish ACK request ID");
                    AssertEqual(fixture.PublicationId, parsed.PublicationId,
                        "publish ACK publication ID");
                    Assert(parsed.DescriptorDigest.SequenceEqual(digest),
                        "publish ACK descriptor digest");
                    Assert(parsed.Descriptor.SequenceEqual(descriptor),
                        "publish ACK descriptor");
                    AssertEqual("revoke-pipe", parsed.RevokePipeName,
                        "publish ACK revoke pipe");
                });

            ClaimRequest claimRequest = new(
                requestId,
                fixture.PublicationId,
                digest,
                controllerNonce);
            AssertProtocolRoundTrip(
                BootstrapProtocol.Encode(claimRequest),
                BootstrapMessageType.ClaimRequest,
                BootstrapRole.Controller,
                BootstrapRole.Broker,
                value => AssertClaimPrefix(
                    RequireType<ClaimRequest>(value).RequestId,
                    RequireType<ClaimRequest>(value).PublicationId,
                    RequireType<ClaimRequest>(value).DescriptorDigest,
                    RequireType<ClaimRequest>(value).ControllerNonce,
                    requestId,
                    fixture.PublicationId,
                    digest,
                    controllerNonce,
                    "claim request"));

            using (ClaimGrant grant = new(
                requestId,
                fixture.PublicationId,
                digest,
                controllerNonce,
                receiptNonce,
                "receipt-pipe",
                token))
            {
                AssertProtocolRoundTrip(
                    BootstrapProtocol.Encode(grant),
                    BootstrapMessageType.ClaimGrant,
                    BootstrapRole.Broker,
                    BootstrapRole.Controller,
                    value =>
                    {
                        ClaimGrant parsed = RequireType<ClaimGrant>(value);
                        AssertClaimPrefix(
                            parsed.RequestId,
                            parsed.PublicationId,
                            parsed.DescriptorDigest,
                            parsed.ControllerNonce,
                            requestId,
                            fixture.PublicationId,
                            digest,
                            controllerNonce,
                            "claim grant");
                        Assert(parsed.ReceiptNonce.SequenceEqual(receiptNonce),
                            "claim grant receipt nonce");
                        AssertEqual("receipt-pipe", parsed.ReceiptPipeName,
                            "claim grant receipt pipe");
                        Assert(parsed.Token.Bytes.SequenceEqual(token),
                            "claim grant token");
                    });
            }

            ClaimReceipt receipt = new(
                requestId,
                fixture.PublicationId,
                digest,
                controllerNonce,
                receiptNonce,
                proof);
            AssertProtocolRoundTrip(
                BootstrapProtocol.Encode(receipt),
                BootstrapMessageType.ClaimReceipt,
                BootstrapRole.Controller,
                BootstrapRole.Broker,
                value =>
                {
                    ClaimReceipt parsed = RequireType<ClaimReceipt>(value);
                    Assert(parsed.PossessionProof.SequenceEqual(proof),
                        "claim receipt proof");
                });

            ClaimFinalAck finalAck = new(
                requestId,
                fixture.PublicationId,
                digest,
                controllerNonce,
                receiptNonce);
            AssertProtocolRoundTrip(
                BootstrapProtocol.Encode(finalAck),
                BootstrapMessageType.ClaimFinalAck,
                BootstrapRole.Broker,
                BootstrapRole.Controller,
                value => Assert(RequireType<ClaimFinalAck>(value)
                        .ReceiptNonce.SequenceEqual(receiptNonce),
                    "claim final ACK receipt nonce"));

            RevokeRequest revoke = new(
                requestId,
                fixture.PublicationId,
                digest,
                revocationNonce);
            AssertProtocolRoundTrip(
                BootstrapProtocol.Encode(revoke),
                BootstrapMessageType.RevokeRequest,
                BootstrapRole.Observer,
                BootstrapRole.Broker,
                value => Assert(RequireType<RevokeRequest>(value)
                        .RevocationNonce.SequenceEqual(revocationNonce),
                    "revoke request nonce"));

            RevokeAck revokeAck = new(
                requestId,
                fixture.PublicationId,
                digest,
                revocationNonce);
            AssertProtocolRoundTrip(
                BootstrapProtocol.Encode(revokeAck),
                BootstrapMessageType.RevokeAck,
                BootstrapRole.Broker,
                BootstrapRole.Observer,
                value => Assert(RequireType<RevokeAck>(value)
                        .RevocationNonce.SequenceEqual(revocationNonce),
                    "revoke ACK nonce"));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(descriptor);
            CryptographicOperations.ZeroMemory(digest);
            CryptographicOperations.ZeroMemory(controllerNonce);
            CryptographicOperations.ZeroMemory(receiptNonce);
            CryptographicOperations.ZeroMemory(revocationNonce);
            CryptographicOperations.ZeroMemory(token);
            CryptographicOperations.ZeroMemory(proof);
        }

        return Task.CompletedTask;
    }

    private static Task TestProtocolCanonicalHeaders()
    {
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[][] frames = CreateEightProtocolFrames(fixture);
        try
        {
            BootstrapMessageType[] types = Enum.GetValues<BootstrapMessageType>();
            AssertEqual(types.Length, frames.Length, "protocol message count");
            for (int index = 0; index < frames.Length; index++)
            {
                byte[] frame = frames[index];
                RequiredRoles(types[index], out BootstrapRole sender, out BootstrapRole receiver);
                AssertProtocolHeader(frame, types[index], sender, receiver);
                AssertEqual(frame.Length - ProtocolHeaderLength,
                    BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(14, 2)),
                    $"{types[index]} body length");

                RequireProtocolMutationFailure(frame, 10, 0x01,
                    $"{types[index]} sender role");
                RequireProtocolMutationFailure(frame, 11, 0x01,
                    $"{types[index]} receiver role");
                RequireProtocolMutationFailure(frame, 12, 0x01,
                    $"{types[index]} flags");

                byte[] truncated = frame[..^1];
                try
                {
                    AssertThrows<FormatException>(() =>
                        BootstrapProtocol.DecodeOwned(
                            truncated,
                            types[index],
                            sender,
                            receiver));
                    Assert(AllZero(truncated),
                        $"failed truncated {types[index]} decode must wipe its frame");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(truncated);
                }

                byte[] extended = new byte[frame.Length + 1];
                try
                {
                    frame.CopyTo(extended, 0);
                    BinaryPrimitives.WriteUInt16BigEndian(
                        extended.AsSpan(14, 2),
                        checked((ushort)(frame.Length - ProtocolHeaderLength + 1)));
                    AssertThrows<FormatException>(() =>
                        BootstrapProtocol.DecodeOwned(
                            extended,
                            types[index],
                            sender,
                            receiver));
                    Assert(AllZero(extended),
                        $"failed extended {types[index]} decode must wipe its frame");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(extended);
                }
            }

            RequireExpectedPhaseFailure(
                frames[0],
                BootstrapMessageType.PublishAck,
                BootstrapRole.Broker,
                BootstrapRole.Observer,
                "message type");
            RequireExpectedPhaseFailure(
                frames[0],
                BootstrapMessageType.PublishRequest,
                BootstrapRole.Broker,
                BootstrapRole.Broker,
                "sender role");
            RequireExpectedPhaseFailure(
                frames[0],
                BootstrapMessageType.PublishRequest,
                BootstrapRole.Observer,
                BootstrapRole.Controller,
                "receiver role");

            RequireProtocolMutationFailure(frames[0], 0, 0x01, "protocol magic");
            RequireProtocolMutationFailure(frames[0], 8, 0x01, "protocol version");
            byte[] unknownType = frames[0].ToArray();
            try
            {
                unknownType[9] = 0xFF;
                AssertThrows<FormatException>(() =>
                    BootstrapProtocol.DecodeOwned(
                        unknownType,
                        BootstrapMessageType.PublishRequest,
                        BootstrapRole.Observer,
                        BootstrapRole.Broker));
                Assert(AllZero(unknownType),
                    "failed unknown-type decode must wipe its transferred frame");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(unknownType);
            }

            byte[] shortHeader = new byte[ProtocolHeaderLength - 1];
            byte[] oversized = new byte[BootstrapProtocol.MaximumFrameLength + 1];
            try
            {
                AssertThrows<FormatException>(() =>
                    BootstrapProtocol.DecodeOwned(
                        shortHeader,
                        BootstrapMessageType.PublishRequest,
                        BootstrapRole.Observer,
                        BootstrapRole.Broker));
                Assert(AllZero(shortHeader),
                    "failed short-header decode must wipe its transferred frame");
                AssertThrows<FormatException>(() =>
                    BootstrapProtocol.DecodeOwned(
                        oversized,
                        BootstrapMessageType.PublishRequest,
                        BootstrapRole.Observer,
                        BootstrapRole.Broker));
                Assert(AllZero(oversized),
                    "failed oversized decode must wipe its transferred frame");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(shortHeader);
                CryptographicOperations.ZeroMemory(oversized);
            }
        }
        finally
        {
            foreach (byte[] frame in frames)
            {
                CryptographicOperations.ZeroMemory(frame);
            }
        }

        return Task.CompletedTask;
    }

    private static Task TestClaimReceiptProof()
    {
        byte[] token = Sequence32(0x01);
        byte[] digest = Sequence32(0x21);
        byte[] controller = Sequence32(0x41);
        byte[] receipt = Sequence32(0x61);
        Guid publication = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");
        byte[] input = new byte[
            ReceiptAuthenticationDomain.Length + 16 + (3 * 32)];
        byte[] expected;
        ClaimReceiptProof? actual = null;
        try
        {
            ReceiptAuthenticationDomain.CopyTo(input, 0);
            int offset = ReceiptAuthenticationDomain.Length;
            ExpectedUuidBytes().CopyTo(input, offset);
            offset += 16;
            digest.CopyTo(input, offset);
            offset += 32;
            controller.CopyTo(input, offset);
            offset += 32;
            receipt.CopyTo(input, offset);
            expected = HMACSHA256.HashData(token, input);
            actual = BootstrapProtocol.ComputeClaimReceiptProof(
                token,
                publication,
                digest,
                controller,
                receipt);
            try
            {
                Assert(expected.AsSpan().SequenceEqual(actual.Bytes),
                    "receipt proof must use the exact domain-separated transcript");
                Assert(BootstrapProtocol.VerifyClaimReceiptProof(
                        token,
                        publication,
                        digest,
                        controller,
                        receipt,
                        actual.Bytes),
                    "valid receipt proof must verify");
                byte[] rawTokenHash = SHA256.HashData(token);
                try
                {
                    Assert(!actual.Bytes.SequenceEqual(rawTokenHash),
                        "receipt proof must not be a raw token hash");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(rawTokenHash);
                }

                byte[] wrongToken = token.ToArray();
                byte[] wrongDigest = digest.ToArray();
                byte[] wrongController = controller.ToArray();
                byte[] wrongReceipt = receipt.ToArray();
                try
                {
                    wrongToken[0] ^= 1;
                    wrongDigest[0] ^= 1;
                    wrongController[0] ^= 1;
                    wrongReceipt[0] ^= 1;
                    Assert(!BootstrapProtocol.VerifyClaimReceiptProof(
                            wrongToken, publication, digest, controller, receipt, actual.Bytes),
                        "wrong receipt key must reject");
                    Assert(!BootstrapProtocol.VerifyClaimReceiptProof(
                            token, Guid.NewGuid(), digest, controller, receipt, actual.Bytes),
                        "wrong publication must reject");
                    Assert(!BootstrapProtocol.VerifyClaimReceiptProof(
                            token, publication, wrongDigest, controller, receipt, actual.Bytes),
                        "wrong descriptor digest must reject");
                    Assert(!BootstrapProtocol.VerifyClaimReceiptProof(
                            token, publication, digest, wrongController, receipt, actual.Bytes),
                        "wrong controller nonce must reject");
                    Assert(!BootstrapProtocol.VerifyClaimReceiptProof(
                            token, publication, digest, controller, wrongReceipt, actual.Bytes),
                        "wrong receipt nonce must reject");
                    Assert(!BootstrapProtocol.VerifyClaimReceiptProof(
                            token, publication, digest, controller, receipt, actual.Bytes[..31]),
                        "wrong proof length must reject");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(wrongToken);
                    CryptographicOperations.ZeroMemory(wrongDigest);
                    CryptographicOperations.ZeroMemory(wrongController);
                    CryptographicOperations.ZeroMemory(wrongReceipt);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expected);
                ClaimReceiptProof ownedProof = actual;
                ReadOnlySpan<byte> borrowedProof = ownedProof.Bytes;
                ownedProof.Dispose();
                Assert(AllZero(borrowedProof),
                    "claim receipt proof must wipe on disposal");
                AssertThrows<ObjectDisposedException>(() =>
                {
                    _ = ownedProof.Bytes.Length;
                });
                actual = null;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(token);
            CryptographicOperations.ZeroMemory(digest);
            CryptographicOperations.ZeroMemory(controller);
            CryptographicOperations.ZeroMemory(receipt);
            CryptographicOperations.ZeroMemory(input);
            actual?.Dispose();
        }

        return Task.CompletedTask;
    }

    private static Task TestProtocolMalformedFields()
    {
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[][] frames = CreateEightProtocolFrames(fixture);
        try
        {
            RequireProtocolZeroFailure(frames[0], 16, 16, "publish request ID");
            RequireProtocolZeroFailure(frames[0], 32, 32, "publication nonce");
            RequireProtocolZeroFailure(frames[0], 64, 2, "endpoint port");
            RequireProtocolZeroFailure(frames[0], 66, 16, "endpoint session ID");
            RequireProtocolZeroFailure(frames[0], 82, 32, "publish token");

            RequireProtocolZeroFailure(frames[1], 16, 16, "publish ACK request ID");
            RequireProtocolZeroFailure(frames[1], 32, 16, "publish ACK publication ID");
            RequireProtocolZeroFailure(frames[2], 16, 16, "claim request ID");
            RequireProtocolZeroFailure(frames[2], 32, 16, "claim publication ID");
            RequireProtocolZeroFailure(frames[2], 80, 32, "controller nonce");
            RequireProtocolZeroFailure(frames[3], 112, 32, "receipt nonce");
            RequireProtocolZeroFailure(
                frames[3],
                frames[3].Length - SecretBuffer.Length,
                SecretBuffer.Length,
                "claim grant token");
            RequireProtocolZeroFailure(frames[4], 112, 32, "claim receipt nonce");
            RequireProtocolZeroFailure(frames[5], 112, 32, "final ACK receipt nonce");
            RequireProtocolZeroFailure(frames[6], 80, 32, "revocation nonce");
            RequireProtocolZeroFailure(frames[7], 80, 32, "revocation ACK nonce");

            byte[] invalidPipe = frames[3].ToArray();
            try
            {
                invalidPipe[146] = (byte)'_';
                AssertThrows<FormatException>(() =>
                    BootstrapProtocol.DecodeOwned(
                        invalidPipe,
                        BootstrapMessageType.ClaimGrant,
                        BootstrapRole.Broker,
                        BootstrapRole.Controller));
                Assert(AllZero(invalidPipe),
                    "failed invalid-pipe decode must wipe its transferred frame");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(invalidPipe);
            }

            byte[] digest = Sequence32(0x11);
            byte[] nonce = Sequence32(0x31);
            byte[] empty = Array.Empty<byte>();
            byte[] shortProof = new byte[31];
            try
            {
                byte[] receipt = Sequence32(0x51);
                try
                {
                    AssertThrows<ArgumentException>(() => _ = new ClaimReceipt(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        digest,
                        nonce,
                        receipt,
                        empty));
                    AssertThrows<ArgumentException>(() => _ = new ClaimReceipt(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        digest,
                        nonce,
                        receipt,
                        shortProof));
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(receipt);
                }

                AssertThrows<ArgumentException>(() =>
                {
                    PublishAck invalid = new(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        digest,
                        Array.Empty<byte>(),
                        "pipe");
                    using SensitiveFrame _ = BootstrapProtocol.Encode(invalid);
                });

                byte[] canonicalDescriptor = fixture.CreateDescriptor()
                    .EncodeCanonical();
                byte[] canonicalDigest = SHA256.HashData(canonicalDescriptor);
                byte[] wrongDigest = canonicalDigest.ToArray();
                try
                {
                    wrongDigest[0] ^= 0x01;
                    AssertThrows<ArgumentException>(() => _ = new PublishAck(
                        Guid.NewGuid(),
                        fixture.PublicationId,
                        wrongDigest,
                        canonicalDescriptor,
                        "pipe"));
                    AssertThrows<ArgumentException>(() => _ = new PublishAck(
                        Guid.NewGuid(),
                        Guid.Parse("20213243-5465-7687-98a9-bacbdcedfe0f"),
                        canonicalDigest,
                        canonicalDescriptor,
                        "pipe"));
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(canonicalDescriptor);
                    CryptographicOperations.ZeroMemory(canonicalDigest);
                    CryptographicOperations.ZeroMemory(wrongDigest);
                }

                AssertThrows<ArgumentException>(() =>
                {
                    RevokeRequest invalid = new(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        new byte[31],
                        nonce);
                    using SensitiveFrame _ = BootstrapProtocol.Encode(invalid);
                });
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
                CryptographicOperations.ZeroMemory(nonce);
                CryptographicOperations.ZeroMemory(shortProof);
            }
        }
        finally
        {
            foreach (byte[] frame in frames)
            {
                CryptographicOperations.ZeroMemory(frame);
            }
        }

        return Task.CompletedTask;
    }

    private static Task TestProtocolSecretOwnership()
    {
        byte[] source = Sequence32(0x31);
        byte[] expected = source.ToArray();
        SecretBuffer owned = SecretBuffer.CreateOwned(source);
        ReadOnlySpan<byte> ownedBorrow = owned.Bytes;
        source[0] ^= 0xFF;
        Assert(owned.Bytes.SequenceEqual(expected),
            "owned secret must copy its caller source");
        owned.Dispose();
        Assert(AllZero(ownedBorrow), "owned secret disposal must wipe its copy");
        AssertThrows<ArgumentException>(() => SecretBuffer.CreateOwned(new byte[31]));
        AssertThrows<ArgumentException>(() => SecretBuffer.CreateOwned(new byte[32]));

        byte[] nonce = Sequence32(0x51);
        ObserverTransportEndpoint endpoint = new(12_345, Guid.NewGuid());
        PublishRequest publish = new(Guid.NewGuid(), nonce, endpoint, expected);
        using SensitiveFrame frame = BootstrapProtocol.Encode(publish);
        ReadOnlySpan<byte> frameBorrow = frame.Bytes.Span;
        byte[] encoded = frame.Bytes.ToArray();
        PublishRequest parsed = RequireType<PublishRequest>(
            BootstrapProtocol.DecodeOwned(
                encoded,
                BootstrapMessageType.PublishRequest,
                BootstrapRole.Observer,
                BootstrapRole.Broker));
        ReadOnlySpan<byte> parsedBorrow = parsed.Token.Bytes;
        try
        {
            Assert(AllZero(encoded),
                "owned decode must wipe its complete transferred frame");
            Assert(parsed.Token.Bytes.SequenceEqual(expected),
                "decoded token must not borrow the received frame");
            parsed.Dispose();
            Assert(AllZero(parsedBorrow), "decoded publish token must wipe on disposal");

            ReadOnlySpan<byte> sourceTokenBorrow = publish.Token.Bytes;
            publish.Dispose();
            Assert(AllZero(sourceTokenBorrow), "publish request must wipe its owned token");

            frame.Dispose();
            Assert(AllZero(frameBorrow), "sensitive protocol frame must wipe on disposal");
            AssertThrows<ObjectDisposedException>(() =>
            {
                _ = frame.Bytes.Length;
            });
        }
        finally
        {
            parsed.Dispose();
            publish.Dispose();
            CryptographicOperations.ZeroMemory(encoded);
            CryptographicOperations.ZeroMemory(source);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(nonce);
        }

        return Task.CompletedTask;
    }

    private static DescriptorFixture CreateDescriptorFixture()
    {
        return new DescriptorFixture(
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000),
            TimeSpan.FromMinutes(5),
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
            Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f"),
            Sequence32(0x21),
            new ObserverTransportEndpoint(
                12_345,
                Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210")),
            "p",
            new BootstrapBinding(
                1,
                1,
                "C:\\a",
                "S-1-1-0",
                "S-1-0-0",
                0,
                0),
            new BootstrapBinding(
                2,
                2,
                "C:\\b",
                "S-1-1-0",
                "S-1-0-0",
                0,
                0),
            Sequence32(0xC1));
    }

    private static byte[] Sequence32(byte first)
    {
        byte[] value = new byte[32];
        for (int index = 0; index < value.Length; index++)
        {
            value[index] = checked((byte)(first + index));
        }

        return value;
    }

    private static byte[] ExpectedUuidBytes() =>
    [
        0x00, 0x11, 0x22, 0x33,
        0x44, 0x55,
        0x66, 0x77,
        0x88, 0x99,
        0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF,
    ];

    private static int DescriptorObserverBindingOffset(string claimPipeName)
    {
        return DescriptorClaimPipeOffset + Encoding.UTF8.GetByteCount(claimPipeName);
    }

    private static int BindingWireLength(BootstrapBinding binding)
    {
        return 20 +
            2 + Encoding.UTF8.GetByteCount(binding.ImagePath) +
            2 + Encoding.UTF8.GetByteCount(binding.UserSid) +
            2 + Encoding.UTF8.GetByteCount(binding.LogonSid);
    }

    private static void RequireDescriptorParseFailure(
        byte[] canonical,
        int offset,
        byte mask,
        string description)
    {
        byte[] malformed = canonical.ToArray();
        try
        {
            malformed[offset] ^= mask;
            AssertThrows<FormatException>(() =>
                BootstrapDescriptor.Parse(malformed));
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Malformed descriptor {description} used an invalid test mutation.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(malformed);
        }
    }

    private static void RequireDescriptorZeroFailure(
        byte[] canonical,
        int offset,
        int length,
        string description)
    {
        byte[] malformed = canonical.ToArray();
        try
        {
            malformed.AsSpan(offset, length).Clear();
            AssertThrows<FormatException>(() =>
                BootstrapDescriptor.Parse(malformed));
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Malformed descriptor {description} used an invalid test mutation.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(malformed);
        }
    }

    private static void RequireDescriptorPeerMismatchRejected(
        DescriptorFixture fixture,
        BootstrapBinding broker,
        string description)
    {
        try
        {
            _ = BootstrapDescriptor.Create(
                fixture.Created,
                fixture.Expires,
                fixture.PublicationId,
                fixture.BrokerInstanceId,
                fixture.Nonce,
                fixture.Endpoint,
                fixture.ClaimPipeName,
                fixture.Observer,
                broker,
                fixture.Token,
                fixture.MaximumLifetime);
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Descriptor creation accepted a broker {description} mismatch.");
    }

    private static void AssertProtocolRoundTrip(
        SensitiveFrame frame,
        BootstrapMessageType type,
        BootstrapRole sender,
        BootstrapRole receiver,
        Action<object> assertDecoded)
    {
        using (frame)
        {
            byte[] canonical = frame.Bytes.ToArray();
            byte[] transferred = canonical.ToArray();
            object? decoded = null;
            try
            {
                AssertProtocolHeader(canonical, type, sender, receiver);
                decoded = BootstrapProtocol.DecodeOwned(
                    transferred,
                    type,
                    sender,
                    receiver);
                Assert(AllZero(transferred),
                    $"{type} decode must wipe its transferred frame");
                assertDecoded(decoded);
                using SensitiveFrame reencoded = EncodeDecoded(decoded);
                Assert(reencoded.Bytes.Span.SequenceEqual(canonical),
                    $"{type} must re-encode to the identical canonical frame");
            }
            finally
            {
                (decoded as IDisposable)?.Dispose();
                CryptographicOperations.ZeroMemory(canonical);
                CryptographicOperations.ZeroMemory(transferred);
            }
        }
    }

    private static SensitiveFrame EncodeDecoded(object decoded)
    {
        return decoded switch
        {
            PublishRequest value => BootstrapProtocol.Encode(value),
            PublishAck value => BootstrapProtocol.Encode(value),
            ClaimRequest value => BootstrapProtocol.Encode(value),
            ClaimGrant value => BootstrapProtocol.Encode(value),
            ClaimReceipt value => BootstrapProtocol.Encode(value),
            ClaimFinalAck value => BootstrapProtocol.Encode(value),
            RevokeRequest value => BootstrapProtocol.Encode(value),
            RevokeAck value => BootstrapProtocol.Encode(value),
            _ => throw new InvalidOperationException(
                "The decoded bootstrap message type is unexpected."),
        };
    }

    private static T RequireType<T>(object value)
        where T : class
    {
        return value as T ?? throw new InvalidOperationException(
            $"Expected decoded type {typeof(T).Name}, actual {value.GetType().Name}.");
    }

    private static void AssertProtocolHeader(
        ReadOnlySpan<byte> frame,
        BootstrapMessageType type,
        BootstrapRole sender,
        BootstrapRole receiver)
    {
        Assert(frame.Length >= ProtocolHeaderLength,
            $"{type} frame must contain a complete header");
        Assert(frame[..8].SequenceEqual("HRCJOBP1"u8),
            $"{type} protocol magic");
        AssertEqual(BootstrapProtocol.ProtocolVersion, frame[8],
            $"{type} protocol version");
        AssertEqual((byte)type, frame[9], $"{type} message type");
        AssertEqual((byte)sender, frame[10], $"{type} sender role");
        AssertEqual((byte)receiver, frame[11], $"{type} receiver role");
        AssertEqual((ushort)0,
            BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(12, 2)),
            $"{type} flags");
    }

    private static byte[][] CreateEightProtocolFrames(DescriptorFixture fixture)
    {
        byte[] descriptor = fixture.CreateDescriptor().EncodeCanonical();
        byte[] digest = SHA256.HashData(descriptor);
        byte[] controller = Sequence32(0x41);
        byte[] receipt = Sequence32(0x61);
        byte[] revocation = Sequence32(0x81);
        byte[] token = Sequence32(0xA1);
        using ClaimReceiptProof proofOwner =
            BootstrapProtocol.ComputeClaimReceiptProof(
                token,
                fixture.PublicationId,
                digest,
                controller,
                receipt);
        byte[] proof = proofOwner.Bytes.ToArray();
        Guid request = Guid.Parse("10213243-5465-7687-98a9-bacbdcedfe0f");
        List<byte[]> frames = new(8);
        try
        {
            using (PublishRequest message = new(
                request,
                fixture.Nonce,
                fixture.Endpoint,
                token))
            {
                frames.Add(CopyAndDispose(BootstrapProtocol.Encode(message)));
            }

            frames.Add(CopyAndDispose(BootstrapProtocol.Encode(new PublishAck(
                request,
                fixture.PublicationId,
                digest,
                descriptor,
                "revoke-pipe"))));
            frames.Add(CopyAndDispose(BootstrapProtocol.Encode(new ClaimRequest(
                request,
                fixture.PublicationId,
                digest,
                controller))));

            using (ClaimGrant message = new(
                request,
                fixture.PublicationId,
                digest,
                controller,
                receipt,
                "receipt-pipe",
                token))
            {
                frames.Add(CopyAndDispose(BootstrapProtocol.Encode(message)));
            }

            frames.Add(CopyAndDispose(BootstrapProtocol.Encode(new ClaimReceipt(
                request,
                fixture.PublicationId,
                digest,
                controller,
                receipt,
                proof))));
            frames.Add(CopyAndDispose(BootstrapProtocol.Encode(new ClaimFinalAck(
                request,
                fixture.PublicationId,
                digest,
                controller,
                receipt))));
            frames.Add(CopyAndDispose(BootstrapProtocol.Encode(new RevokeRequest(
                request,
                fixture.PublicationId,
                digest,
                revocation))));
            frames.Add(CopyAndDispose(BootstrapProtocol.Encode(new RevokeAck(
                request,
                fixture.PublicationId,
                digest,
                revocation))));
            return frames.ToArray();
        }
        catch
        {
            foreach (byte[] frame in frames)
            {
                CryptographicOperations.ZeroMemory(frame);
            }

            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(descriptor);
            CryptographicOperations.ZeroMemory(digest);
            CryptographicOperations.ZeroMemory(controller);
            CryptographicOperations.ZeroMemory(receipt);
            CryptographicOperations.ZeroMemory(revocation);
            CryptographicOperations.ZeroMemory(token);
            CryptographicOperations.ZeroMemory(proof);
        }
    }

    private static byte[] CopyAndDispose(SensitiveFrame frame)
    {
        using (frame)
        {
            return frame.Bytes.ToArray();
        }
    }

    private static void RequiredRoles(
        BootstrapMessageType type,
        out BootstrapRole sender,
        out BootstrapRole receiver)
    {
        (sender, receiver) = type switch
        {
            BootstrapMessageType.PublishRequest =>
                (BootstrapRole.Observer, BootstrapRole.Broker),
            BootstrapMessageType.PublishAck =>
                (BootstrapRole.Broker, BootstrapRole.Observer),
            BootstrapMessageType.ClaimRequest =>
                (BootstrapRole.Controller, BootstrapRole.Broker),
            BootstrapMessageType.ClaimGrant =>
                (BootstrapRole.Broker, BootstrapRole.Controller),
            BootstrapMessageType.ClaimReceipt =>
                (BootstrapRole.Controller, BootstrapRole.Broker),
            BootstrapMessageType.ClaimFinalAck =>
                (BootstrapRole.Broker, BootstrapRole.Controller),
            BootstrapMessageType.RevokeRequest =>
                (BootstrapRole.Observer, BootstrapRole.Broker),
            BootstrapMessageType.RevokeAck =>
                (BootstrapRole.Broker, BootstrapRole.Observer),
            _ => throw new InvalidOperationException(
                "The test role matrix is incomplete."),
        };
    }

    private static void RequireProtocolMutationFailure(
        byte[] canonical,
        int offset,
        byte mask,
        string description)
    {
        BootstrapMessageType expectedType =
            (BootstrapMessageType)canonical[9];
        BootstrapRole expectedSender = (BootstrapRole)canonical[10];
        BootstrapRole expectedReceiver = (BootstrapRole)canonical[11];
        byte[] malformed = canonical.ToArray();
        try
        {
            malformed[offset] ^= mask;
            AssertThrows<FormatException>(() =>
                BootstrapProtocol.DecodeOwned(
                    malformed,
                    expectedType,
                    expectedSender,
                    expectedReceiver));
            Assert(AllZero(malformed),
                $"failed {description} decode must wipe its transferred frame");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Malformed protocol {description} used an invalid test mutation.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(malformed);
        }
    }

    private static void RequireExpectedPhaseFailure(
        byte[] canonical,
        BootstrapMessageType expectedType,
        BootstrapRole expectedSender,
        BootstrapRole expectedReceiver,
        string description)
    {
        byte[] transferred = canonical.ToArray();
        try
        {
            AssertThrows<FormatException>(() => BootstrapProtocol.DecodeOwned(
                transferred,
                expectedType,
                expectedSender,
                expectedReceiver));
            Assert(AllZero(transferred),
                $"failed expected-phase {description} must wipe its frame");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(transferred);
        }
    }

    private static void RequireProtocolZeroFailure(
        byte[] canonical,
        int offset,
        int length,
        string description)
    {
        BootstrapMessageType expectedType =
            (BootstrapMessageType)canonical[9];
        BootstrapRole expectedSender = (BootstrapRole)canonical[10];
        BootstrapRole expectedReceiver = (BootstrapRole)canonical[11];
        byte[] malformed = canonical.ToArray();
        try
        {
            malformed.AsSpan(offset, length).Clear();
            AssertThrows<FormatException>(() =>
                BootstrapProtocol.DecodeOwned(
                    malformed,
                    expectedType,
                    expectedSender,
                    expectedReceiver));
            Assert(AllZero(malformed),
                $"failed {description} decode must wipe its transferred frame");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Malformed protocol {description} used an invalid test mutation.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(malformed);
        }
    }

    private static void AssertClaimPrefix(
        Guid actualRequest,
        Guid actualPublication,
        ReadOnlySpan<byte> actualDigest,
        ReadOnlySpan<byte> actualController,
        Guid expectedRequest,
        Guid expectedPublication,
        byte[] expectedDigest,
        byte[] expectedController,
        string description)
    {
        AssertEqual(expectedRequest, actualRequest, description + " request ID");
        AssertEqual(expectedPublication, actualPublication,
            description + " publication ID");
        Assert(actualDigest.SequenceEqual(expectedDigest),
            description + " descriptor digest");
        Assert(actualController.SequenceEqual(expectedController),
            description + " controller nonce");
    }

    private static BootstrapBinding BindingWith(
        BootstrapBinding source,
        uint? processId = null,
        ulong? creation = null,
        string? imagePath = null,
        string? userSid = null,
        string? logonSid = null,
        uint? tokenSession = null,
        uint? processSession = null)
    {
        return new BootstrapBinding(
            processId ?? source.ProcessId,
            creation ?? source.CreationTimeFileTime,
            imagePath ?? source.ImagePath,
            userSid ?? source.UserSid,
            logonSid ?? source.LogonSid,
            tokenSession ?? source.TokenSessionId,
            processSession ?? source.ProcessSessionId);
    }

    private static string TestPipeName(string purpose)
    {
        return "hrc-job-observer-bootstrap-test-" + purpose + "-" +
            Guid.NewGuid().ToString("N");
    }

    private static async Task TestInvalidFrames()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        using ProtectedNamedPipe server = ProtectedNamedPipe.Create(identity.Snapshot());
        using ManualResetEventSlim releaseClient = new(false);
        Task client = Task.Run(() =>
        {
            using ProtectedNamedPipeClient connection =
                ProtectedNamedPipeClient.Connect(
                    server.Name,
                    identity.Snapshot(),
                    TestTimeout);
            if (!releaseClient.Wait(TestTimeout))
            {
                throw new TimeoutException("Invalid-frame client was not released.");
            }
        });
        await server.AcceptAndAuthenticateAsync(TestTimeout).ConfigureAwait(false);
        try
        {
            await AssertThrowsAsync<ArgumentOutOfRangeException>(() =>
                server.SendFrameAsync(Array.Empty<byte>(), TestTimeout));
            await AssertThrowsAsync<ArgumentOutOfRangeException>(() =>
                server.SendFrameAsync(
                    new byte[ProtectedNamedPipe.MaximumFrameBytes + 1],
                    TestTimeout));
        }
        finally
        {
            releaseClient.Set();
        }

        await client.ConfigureAwait(false);
        server.Dispose();
        await AssertThrowsAsync<ObjectDisposedException>(() =>
            server.AcceptAndAuthenticateAsync(TestTimeout));
    }

    private static async Task TestCrossProcessIdentity()
    {
        using ProcessIdentityLease parent = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        using TestChild child = StartChild();
        using ProcessIdentityLease childIdentity = ProcessIdentityLease.Capture(child.ProcessId);
        Assert(child.ProcessId != parent.ProcessId,
            "the child process must differ from its parent");
        Assert(childIdentity.CreationTimeFileTime > 0 &&
            parent.CreationTimeFileTime > 0,
            "process creation identities must be present");
        using ProtectedNamedPipe server = ProtectedNamedPipe.Create(
            childIdentity.Snapshot());

        Task accept = server.AcceptAndAuthenticateAsync(TestTimeout);
        await child.SendConnectAsync(parent.ProcessId, server.Name)
            .ConfigureAwait(false);
        await accept.ConfigureAwait(false);
        byte[] request = await server.ReceiveFrameAsync(TestTimeout)
            .ConfigureAwait(false);
        try
        {
            Assert(SyntheticRequest.SequenceEqual(request),
                "cross-process request mismatch");
        }
        finally
        {
            Array.Clear(request);
        }

        await server.SendFrameAsync(SyntheticResponse, TestTimeout)
            .ConfigureAwait(false);
        await child.RequireExitAsync(TestTimeout).ConfigureAwait(false);
    }

    private static async Task TestCrossProcessMismatch()
    {
        using ProcessIdentityLease parent = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        using TestChild expected = StartChild();
        using TestChild wrong = StartChild();
        using ProcessIdentityLease expectedIdentity =
            ProcessIdentityLease.Capture(expected.ProcessId);
        using ProcessIdentityLease wrongIdentity =
            ProcessIdentityLease.Capture(wrong.ProcessId);
        Assert(expected.ProcessId != wrong.ProcessId &&
            expected.ProcessId != parent.ProcessId &&
            wrong.ProcessId != parent.ProcessId,
            "the mismatch test requires three distinct processes");
        Assert(expectedIdentity.CreationTimeFileTime > 0 &&
            wrongIdentity.CreationTimeFileTime > 0 &&
            parent.CreationTimeFileTime > 0,
            "the mismatch test requires complete process identities");
        using ProtectedNamedPipe server = ProtectedNamedPipe.Create(
            expectedIdentity.Snapshot());

        Task accept = server.AcceptAndAuthenticateAsync(TestTimeout);
        await wrong.SendExpectServerRejectionAsync(
                parent.ProcessId,
                server.Name)
            .ConfigureAwait(false);
        await AssertThrowsAsync<SecurityException>(async () =>
        {
            await accept.ConfigureAwait(false);
        })
            .ConfigureAwait(false);
        await wrong.RequireExitAsync(TestTimeout).ConfigureAwait(false);
        await expected.SendExitAsync().ConfigureAwait(false);
        await expected.RequireExitAsync(TestTimeout).ConfigureAwait(false);
    }

    private static TestChild StartChild()
    {
        string executable = Environment.ProcessPath ??
            throw new InvalidOperationException("The test executable path is unavailable.");
        string? entryAssembly = System.Reflection.Assembly
            .GetEntryAssembly()?
            .Location;
        bool usesDotnetHost = string.Equals(
            Path.GetFileName(executable),
            "dotnet.exe",
            StringComparison.OrdinalIgnoreCase);
        if (usesDotnetHost &&
            (string.IsNullOrWhiteSpace(entryAssembly) ||
                !Path.IsPathFullyQualified(entryAssembly)))
        {
            throw new InvalidOperationException(
                "The test assembly path is unavailable for the dotnet host.");
        }

        ProcessStartInfo start = new(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (usesDotnetHost)
        {
            start.ArgumentList.Add(entryAssembly!);
        }

        start.ArgumentList.Add(ChildMode);
        start.Environment.Clear();
        if (usesDotnetHost)
        {
            string? runtimeRoot = Path.GetDirectoryName(executable);
            if (!string.IsNullOrWhiteSpace(runtimeRoot))
            {
                start.Environment["DOTNET_ROOT"] = runtimeRoot;
            }

            start.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
        }

        start.Environment["DOTNET_NOLOGO"] = "1";
        Process process = Process.Start(start) ??
            throw new InvalidOperationException("Starting the test child failed.");
        try
        {
            return new TestChild(process);
        }
        catch
        {
            TestChild.TerminateUnowned(process);
            throw;
        }
    }

    private static async Task<int> RunCrossProcessChild()
    {
        try
        {
            Stream input = System.Console.OpenStandardInput();
            byte[] header = new byte[sizeof(byte) + sizeof(uint) + sizeof(byte)];
            try
            {
                using CancellationTokenSource cancellation =
                    new(TestTimeout);
                await input.ReadExactlyAsync(header, cancellation.Token)
                    .ConfigureAwait(false);
                byte operation = header[0];
                if (operation == ChildExit)
                {
                    return 0;
                }

                if (operation != ChildConnect &&
                    operation != ChildExpectServerRejection)
                {
                    return 3;
                }

                uint parentProcessId = BitConverter.ToUInt32(header, 1);
                int nameLength = header[^1];
                if (parentProcessId == 0 || nameLength < 1 || nameLength > 120)
                {
                    return 4;
                }

                byte[] nameBytes = new byte[nameLength];
                try
                {
                    await input.ReadExactlyAsync(nameBytes, cancellation.Token)
                        .ConfigureAwait(false);
                    string name = System.Text.Encoding.ASCII.GetString(nameBytes);
                    ProtectedNamedPipe.ValidateName(name);
                    using ProcessIdentityLease parent =
                        ProcessIdentityLease.Capture(parentProcessId);
                    using ProtectedNamedPipeClient client =
                        ProtectedNamedPipeClient.Connect(
                            name,
                            parent.Snapshot(),
                            TestTimeout);
                    if (operation == ChildExpectServerRejection)
                    {
                        byte[] probe = { 0x7F };
                        try
                        {
                            await client.SendFrameAsync(probe, TestTimeout)
                                .ConfigureAwait(false);
                            byte[] unexpected = await client
                                .ReceiveFrameAsync(TestTimeout)
                                .ConfigureAwait(false);
                            try
                            {
                                return 7;
                            }
                            finally
                            {
                                Array.Clear(unexpected);
                            }
                        }
                        catch (IOException)
                        {
                            return 0;
                        }
                        finally
                        {
                            Array.Clear(probe);
                        }
                    }

                    await client.SendFrameAsync(
                            SyntheticRequest,
                            TestTimeout)
                        .ConfigureAwait(false);
                    byte[] response = await client
                        .ReceiveFrameAsync(TestTimeout)
                        .ConfigureAwait(false);
                    try
                    {
                        return SyntheticResponse.SequenceEqual(response)
                            ? 0
                            : 5;
                    }
                    finally
                    {
                        Array.Clear(response);
                    }
                }
                finally
                {
                    Array.Clear(nameBytes);
                }
            }
            finally
            {
                Array.Clear(header);
            }
        }
        catch
        {
            return 6;
        }
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected {typeof(TException).Name} was not thrown.");
    }

    private static async Task AssertThrowsAnyAsync(
        Func<Task> action,
        params Type[] expectedTypes)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception exception) when (expectedTypes.Any(
            type => type.IsAssignableFrom(exception.GetType())))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected one of [{string.Join(", ", expectedTypes.Select(
                type => type.Name))}] was not thrown.");
    }

    private static bool AllZero(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            if (value != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string description)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Unexpected {description}: expected '{expected}', actual '{actual}'.");
        }
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected {typeof(TException).Name} was not thrown.");
    }

    private static void AssertThrowsAny(Action action, params Type[] expectedTypes)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (expectedTypes.Contains(exception.GetType()))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected one of [{string.Join(", ", expectedTypes.Select(type => type.Name))}] " +
            "was not thrown.");
    }

    private sealed class DescriptorFixture : IDisposable
    {
        internal DescriptorFixture(
            DateTimeOffset created,
            TimeSpan maximumLifetime,
            Guid publicationId,
            Guid brokerInstanceId,
            byte[] nonce,
            ObserverTransportEndpoint endpoint,
            string claimPipeName,
            BootstrapBinding observer,
            BootstrapBinding broker,
            byte[] token)
        {
            Created = created;
            MaximumLifetime = maximumLifetime;
            Expires = created + maximumLifetime;
            PublicationId = publicationId;
            BrokerInstanceId = brokerInstanceId;
            Nonce = nonce;
            Endpoint = endpoint;
            ClaimPipeName = claimPipeName;
            Observer = observer;
            Broker = broker;
            Token = token;
        }

        internal DateTimeOffset Created { get; }
        internal DateTimeOffset Expires { get; }
        internal TimeSpan MaximumLifetime { get; }
        internal Guid PublicationId { get; }
        internal Guid BrokerInstanceId { get; }
        internal byte[] Nonce { get; }
        internal ObserverTransportEndpoint Endpoint { get; }
        internal string ClaimPipeName { get; }
        internal BootstrapBinding Observer { get; }
        internal BootstrapBinding Broker { get; }
        internal byte[] Token { get; }

        internal BootstrapDescriptor CreateDescriptor()
        {
            return BootstrapDescriptor.Create(
                Created,
                Expires,
                PublicationId,
                BrokerInstanceId,
                Nonce,
                Endpoint,
                ClaimPipeName,
                Observer,
                Broker,
                Token,
                MaximumLifetime);
        }

        public void Dispose()
        {
            CryptographicOperations.ZeroMemory(Nonce);
            CryptographicOperations.ZeroMemory(Token);
        }
    }

    private readonly record struct TestCase(string Name, Func<Task> Body);

    private sealed class TestChild : IDisposable
    {
        private readonly Process process;
        private readonly Task outputDrain;
        private readonly Task errorDrain;
        private readonly CountingSink outputSink = new();
        private readonly CountingSink errorSink = new();

        internal TestChild(Process process)
        {
            this.process = process;
            outputDrain = process.StandardOutput.BaseStream.CopyToAsync(outputSink);
            errorDrain = process.StandardError.BaseStream.CopyToAsync(errorSink);
        }

        internal uint ProcessId => checked((uint)process.Id);

        internal Task SendConnectAsync(uint parentProcessId, string pipeName)
        {
            return SendCommandAsync(ChildConnect, parentProcessId, pipeName);
        }

        internal Task SendExpectServerRejectionAsync(
            uint parentProcessId,
            string pipeName)
        {
            return SendCommandAsync(
                ChildExpectServerRejection,
                parentProcessId,
                pipeName);
        }

        internal Task SendExitAsync()
        {
            return WriteCommandAsync(new byte[] { ChildExit, 0, 0, 0, 0, 0 });
        }

        private async Task SendCommandAsync(
            byte operation,
            uint parentProcessId,
            string pipeName)
        {
            ProtectedNamedPipe.ValidateName(pipeName);
            byte[] name = System.Text.Encoding.ASCII.GetBytes(pipeName);
            byte[] command = new byte[sizeof(byte) + sizeof(uint) + sizeof(byte) + name.Length];
            try
            {
                command[0] = operation;
                BitConverter.GetBytes(parentProcessId).CopyTo(command, 1);
                command[sizeof(byte) + sizeof(uint)] = checked((byte)name.Length);
                name.CopyTo(command, sizeof(byte) + sizeof(uint) + sizeof(byte));
                await WriteCommandAsync(command).ConfigureAwait(false);
            }
            finally
            {
                Array.Clear(name);
                Array.Clear(command);
            }
        }

        internal async Task RequireExitAsync(TimeSpan timeout)
        {
            int exitCode = await WaitForExitCodeAsync(timeout).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"The test child exited with code {exitCode}.");
            }
        }

        public void Dispose()
        {
            TerminateOwned(process);
        }

        internal static void TerminateUnowned(Process process)
        {
            ArgumentNullException.ThrowIfNull(process);
            TerminateOwned(process);
        }

        private static void TerminateOwned(Process process)
        {
            bool cleanupFailed = false;
            try
            {
                try
                {
                    process.StandardInput.Close();
                }
                catch (IOException)
                {
                    // The retained process handle remains the cleanup authority.
                }
                catch (InvalidOperationException)
                {
                    // The child can exit between the stream and process checks.
                }

                try
                {
                    if (!process.HasExited)
                    {
                        try
                        {
                            process.Kill(true);
                        }
                        catch (InvalidOperationException)
                        {
                            // A concurrent clean exit is confirmed by the wait below.
                        }
                        catch (Win32Exception)
                        {
                            // A failed kill is safe only if the bounded wait confirms exit.
                        }
                    }

                    cleanupFailed = !process.WaitForExit(5_000);
                }
                catch (InvalidOperationException)
                {
                    cleanupFailed = true;
                }
                catch (Win32Exception)
                {
                    cleanupFailed = true;
                }
            }
            finally
            {
                process.Dispose();
            }

            if (cleanupFailed)
            {
                throw new InvalidOperationException(
                    "Test child cleanup did not confirm process termination.");
            }
        }

        private async Task WriteCommandAsync(byte[] command)
        {
            try
            {
                using CancellationTokenSource cancellation = new(TestTimeout);
                await process.StandardInput.BaseStream.WriteAsync(
                        command,
                        cancellation.Token)
                    .ConfigureAwait(false);
                process.StandardInput.Close();
            }
            finally
            {
                Array.Clear(command);
            }
        }

        private async Task<int> WaitForExitCodeAsync(TimeSpan timeout)
        {
            using CancellationTokenSource cancellation = new(timeout);
            await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
            await Task.WhenAll(outputDrain, errorDrain).WaitAsync(cancellation.Token)
                .ConfigureAwait(false);
            if (outputSink.BytesWritten != 0 || errorSink.BytesWritten != 0)
            {
                throw new InvalidOperationException(
                    "The silent test child wrote to stdout or stderr.");
            }

            return process.ExitCode;
        }
    }

    private sealed class CountingSink : Stream
    {
        private long bytesWritten;

        internal long BytesWritten => Interlocked.Read(ref bytesWritten);

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            Interlocked.Add(ref bytesWritten, count);
        }

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Add(ref bytesWritten, buffer.Length);
            return ValueTask.CompletedTask;
        }
    }
}
