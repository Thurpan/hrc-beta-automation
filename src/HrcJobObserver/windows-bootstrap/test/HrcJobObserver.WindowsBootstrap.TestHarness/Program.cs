using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class Program
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HarnessTimeout = TimeSpan.FromSeconds(20);
    private const string ChildMode = "--cross-process-child";
    private const string BrokerObserverChildMode = "--broker-observer-child";
    private const string BrokerControllerChildMode = "--broker-controller-child";
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

        if (args.Length == 1 &&
            string.Equals(
                args[0],
                BrokerObserverChildMode,
                StringComparison.Ordinal))
        {
            return await RunBrokerObserverChild().ConfigureAwait(false);
        }

        if (args.Length == 1 &&
            string.Equals(
                args[0],
                BrokerControllerChildMode,
                StringComparison.Ordinal))
        {
            return await RunBrokerControllerChild().ConfigureAwait(false);
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
            new("publication store owns snapshots and prevents ABA removal", TestPublicationStore),
            new("async publication leases stay store-affine and ABA-safe", TestAsyncPublicationStore),
            new("publication lease removal coalesces success and synchronous failure", TestPublicationLeaseCoalescing),
            new("file publication round trips exact public bytes and removal", TestFilePublicationRoundTrip),
            new("file publication preserves capacity and collision sentinels", TestFilePublicationCollisions),
            new("file publication rejects malformed and wrongly secured state", TestFilePublicationInvalidState),
            new("file publication leases remain ABA-safe across readers", TestFilePublicationAbaAndReaderRace),
            new("file publication cleans deadline and cancellation failures", TestFilePublicationFailureCleanup),
            new("file publication bounds occupied and collision paths", TestFilePublicationCollisionBounds),
            new("file publication pins its exact directory namespace", TestFilePublicationDirectoryPinning),
            new("file publication bounds multi-page directory enumeration", TestFilePublicationMultiPageEnumeration),
            new("file publication rejects file-identity replacement", TestFilePublicationIdentityReplacement),
            new("file publication rejects a real directory junction", TestFilePublicationReparsePoint),
            new("file publication rename honours its retained root", TestFilePublicationRetainedRootRename),
            new("trusted artifact retains exact path, identity, length, and digest", TestTrustedArtifactIdentity),
            new("trusted artifact rejects invalid paths and mismatched files", TestTrustedArtifactInvalidInputs),
            new("trusted artifact rejects reparse and multi-link paths", TestTrustedArtifactPathGuards),
            new("trusted artifact rejects a pre-existing writable mapping", TestTrustedArtifactWritableMapping),
            new("trusted artifact identity does not bind mutable siblings", TestTrustedArtifactSiblingBoundary),
            new("broker enforces role identity and security context", TestBrokerRoleBindings),
            new("broker disposal before run is coalesced and releases its name", TestBrokerDisposeBeforeRun),
            new("broker completes a cross-process claim and receipt", TestBrokerClaim),
            new("broker completes a cross-process revocation", TestBrokerRevoke),
            new("broker serialises claim and revoke races", TestBrokerRaces),
            new("broker rejects an already-completed semantic loser", TestBrokerSemanticLoser),
            new("broker fails closed on a mismatched transcript", TestBrokerTranscriptMismatch),
            new("broker fails closed on a bad receipt proof", TestBrokerBadProof),
            new("broker does not reset its absolute deadline", TestBrokerDeadline),
            new("broker caps publication by the remaining session deadline", TestBrokerCombinedDeadlineCap),
            new("broker cleans a start-bound deadline setup failure", TestBrokerDeadlineSetupFailure),
            new("broker cancellation cleans publication and pipes", TestBrokerCancellation),
            new("broker cancellation awaits one exact coalesced removal", TestBrokerCancellationAwaitsRemoval),
            new("broker disposal cancels a blocked publication", TestBrokerDisposeDuringPublish),
            new("broker disposal preserves a throwing cancellation callback", TestBrokerDisposalCancellationCallbackFailure),
            new("broker publisher can synchronously reenter disposal", TestBrokerPublisherReentrantDisposal),
            new("broker rolls back a commit returned after disposal", TestBrokerCommitBeforeReturnRollback),
            new("broker exposes an asynchronous publisher fault", TestBrokerPublisherFault),
            new("broker removal fault suppresses terminal success", TestBrokerRemovalFault),
            new("broker rejects an unknown removal result before terminal acknowledgement", TestBrokerUnknownRemovalStatus),
            new("broker disposal exposes a post-commit removal fault", TestBrokerDisposeCommitRemovalFault),
            new("broker preserves protocol and removal failures", TestBrokerPrimaryAndRemovalFailure),
            new("broker rejects an occupied store and cleans its publication", TestBrokerOccupiedStore),
            new("broker releases every one-shot pipe name", TestBrokerNameRelease),
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

    private static Task TestPublicationStore()
    {
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] descriptor = fixture.CreateDescriptor().EncodeCanonical();
        try
        {
            using InMemoryBootstrapPublicationStore store = new();
            Assert(store.TryPublish(descriptor, out BootstrapPublicationRegistration? first) &&
                first is not null, "the empty store must accept one publication");
            BootstrapPublicationRegistration firstOwner = first ??
                throw new InvalidOperationException("The first owner is missing.");
            byte[] storedBorrow = GetStoredDescriptorBacking(store);
            Assert(!store.TryPublish(descriptor, out _),
                "the occupied store must reject a second publication");
            Assert(store.TryRead(out BootstrapPublicationSnapshot? snapshot) &&
                snapshot is not null, "the publication must be visible");
            BootstrapPublicationSnapshot firstSnapshot = snapshot ??
                throw new InvalidOperationException("The first snapshot is missing.");
            ReadOnlySpan<byte> borrowed = firstSnapshot.Descriptor;
            descriptor.AsSpan().Clear();
            Assert(!AllZero(borrowed),
                "the store snapshot must not alias the caller's descriptor");
            firstSnapshot.Dispose();
            Assert(AllZero(borrowed), "disposing a snapshot must wipe it");
            Assert(store.TryRemove(firstOwner),
                "the exact registration must remove its publication");
            Assert(AllZero(storedBorrow),
                "exact removal must wipe the stored descriptor backing array");

            byte[] replacement = fixture.CreateDescriptor().EncodeCanonical();
            try
            {
                Assert(store.TryPublish(replacement, out BootstrapPublicationRegistration? second) &&
                    second is not null, "the empty store must accept a replacement");
                BootstrapPublicationRegistration secondOwner = second ??
                    throw new InvalidOperationException(
                        "The replacement owner is missing.");
                Assert(!store.Owns(firstOwner),
                    "an old registration must not remove a replacement");
                Assert(store.TryRead(out BootstrapPublicationSnapshot? current) &&
                    current is not null, "the replacement must remain visible");
                (current ?? throw new InvalidOperationException(
                    "The replacement snapshot is missing.")).Dispose();
                Assert(store.TryRemove(secondOwner),
                    "the replacement's registration must remove it");
                Assert(!store.TryRead(out _), "the store must be empty after removal");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(replacement);
            }

            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(descriptor);
        }
    }

    private static async Task TestAsyncPublicationStore()
    {
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] descriptor = fixture.CreateDescriptor().EncodeCanonical();
        try
        {
            using InMemoryBootstrapPublicationStore firstStore = new();
            using InMemoryBootstrapPublicationStore secondStore = new();
            MonotonicDeadline deadline = MonotonicDeadline.Start(
                TimeProvider.System,
                TestTimeout);
            BootstrapPublishResult first = await firstStore.TryPublishAsync(
                    descriptor,
                    deadline,
                    CancellationToken.None)
                .ConfigureAwait(false);
            BootstrapPublicationRegistration firstLease =
                first.Lease as BootstrapPublicationRegistration ??
                throw new InvalidOperationException(
                    "The first asynchronous publication lease is missing.");
            AssertEqual(BootstrapPublishStatus.Published, first.Status,
                "first asynchronous publication status");

            BootstrapPublishResult occupied = await firstStore.TryPublishAsync(
                    descriptor,
                    deadline,
                    CancellationToken.None)
                .ConfigureAwait(false);
            AssertEqual(BootstrapPublishStatus.Occupied, occupied.Status,
                "occupied asynchronous publication status");
            Assert(occupied.Lease is null,
                "an occupied asynchronous publication must not return a lease");

            BootstrapPublishResult second = await secondStore.TryPublishAsync(
                    descriptor,
                    deadline,
                    CancellationToken.None)
                .ConfigureAwait(false);
            BootstrapPublicationRegistration secondLease =
                second.Lease as BootstrapPublicationRegistration ??
                throw new InvalidOperationException(
                    "The second store publication lease is missing.");
            Assert(!secondStore.TryRemove(firstLease),
                "the second store must reject the first store's registration");
            Assert(firstStore.TryRead(
                    out BootstrapPublicationSnapshot? firstSurvivor) &&
                firstSurvivor is not null,
                "a cross-store legacy removal must retain the first publication");
            (firstSurvivor ?? throw new InvalidOperationException(
                "The first cross-store survivor snapshot is missing.")).Dispose();
            _ = await firstLease.RemoveExactAsync(deadline)
                .ConfigureAwait(false);
            Assert(secondStore.TryRead(out BootstrapPublicationSnapshot? crossStore) &&
                crossStore is not null,
                "removing one store-affine lease must not affect another store");
            (crossStore ?? throw new InvalidOperationException(
                "The cross-store snapshot is missing.")).Dispose();

            BootstrapPublishResult replacement = await firstStore.TryPublishAsync(
                    descriptor,
                    deadline,
                    CancellationToken.None)
                .ConfigureAwait(false);
            BootstrapPublicationLease replacementLease = replacement.Lease ??
                throw new InvalidOperationException(
                    "The ABA replacement lease is missing.");
            Assert(!firstStore.TryRemove(secondLease),
                "a legacy registration from another store must be rejected");
            Assert(firstStore.TryRead(out BootstrapPublicationSnapshot? ownEntry) &&
                ownEntry is not null,
                "a foreign legacy registration must not remove the local entry");
            (ownEntry ?? throw new InvalidOperationException(
                "The local cross-store-affinity snapshot is missing.")).Dispose();
            Assert(secondStore.TryRead(out BootstrapPublicationSnapshot? foreignEntry) &&
                foreignEntry is not null,
                "a rejected foreign registration must retain its own entry");
            (foreignEntry ?? throw new InvalidOperationException(
                "The foreign cross-store-affinity snapshot is missing.")).Dispose();
            BootstrapPublicationRemovalStatus repeated =
                await firstLease.RemoveExactAsync(deadline)
                    .ConfigureAwait(false);
            AssertEqual(BootstrapPublicationRemovalStatus.Removed, repeated,
                "coalesced stale-lease removal status");
            Assert(firstStore.TryRead(out BootstrapPublicationSnapshot? afterAba) &&
                afterAba is not null,
                "a stale lease must not remove an equal replacement");
            (afterAba ?? throw new InvalidOperationException(
                "The ABA replacement snapshot is missing.")).Dispose();

            _ = await secondLease.RemoveExactAsync(deadline)
                .ConfigureAwait(false);
            _ = await replacementLease.RemoveExactAsync(deadline)
                .ConfigureAwait(false);
            Assert(!firstStore.TryRead(out _) && !secondStore.TryRead(out _),
                "exact lease removal must leave both stores empty");

            using CancellationTokenSource cancelled = new();
            cancelled.Cancel();
            await AssertThrowsAsync<OperationCanceledException>(async () =>
            {
                _ = await firstStore.TryPublishAsync(
                        descriptor,
                        MonotonicDeadline.Start(TimeProvider.System, TestTimeout),
                        cancelled.Token)
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
            Assert(!firstStore.TryRead(out _),
                "a cancelled asynchronous publish must not mutate the store");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(descriptor);
        }
    }

    private static async Task TestPublicationLeaseCoalescing()
    {
        MonotonicDeadline deadline = MonotonicDeadline.Start(
            TimeProvider.System,
            TestTimeout);
        LatchPublicationLease successful = new();
        TaskCompletionSource<bool> firstCalled = NewSignal();
        TaskCompletionSource<bool> secondCalled = NewSignal();
        Task<BootstrapPublicationRemovalStatus> first = Task.Run(async () =>
        {
            ValueTask<BootstrapPublicationRemovalStatus> pending =
                successful.RemoveExactAsync(deadline);
            firstCalled.TrySetResult(true);
            return await pending.ConfigureAwait(false);
        });
        Task<BootstrapPublicationRemovalStatus> second = Task.Run(async () =>
        {
            ValueTask<BootstrapPublicationRemovalStatus> pending =
                successful.RemoveExactAsync(deadline);
            secondCalled.TrySetResult(true);
            return await pending.ConfigureAwait(false);
        });
        await Task.WhenAll(firstCalled.Task, secondCalled.Task)
            .WaitAsync(TestTimeout)
            .ConfigureAwait(false);
        await successful.RemovalStarted.WaitAsync(TestTimeout)
            .ConfigureAwait(false);
        AssertEqual(1, successful.RemovalCalls,
            "concurrent direct lease removal call count while blocked");
        Assert(!first.IsCompleted && !second.IsCompleted,
            "concurrent direct lease callers must await the same core removal");
        successful.ReleaseRemoval();
        BootstrapPublicationRemovalStatus[] results = await Task.WhenAll(
                first,
                second)
            .ConfigureAwait(false);
        Assert(results.All(
                result => result == BootstrapPublicationRemovalStatus.Removed),
            "coalesced direct lease callers must observe verified removal");
        AssertEqual(1, successful.RemovalCalls,
            "completed direct lease removal call count");

        SynchronouslyThrowingPublicationLease faulting = new();
        Task<BootstrapPublicationRemovalStatus> firstFault = faulting
            .RemoveExactAsync(deadline)
            .AsTask();
        Task<BootstrapPublicationRemovalStatus> secondFault = faulting
            .RemoveExactAsync(deadline)
            .AsTask();
        Assert(ReferenceEquals(firstFault, secondFault),
            "a synchronous core throw must publish one cached fault task");
        Exception observedFirst = await CaptureExceptionAsync(firstFault)
            .ConfigureAwait(false);
        Exception observedSecond = await CaptureExceptionAsync(secondFault)
            .ConfigureAwait(false);
        Assert(ContainsException<TestRemovalException>(observedFirst) &&
            ContainsException<TestRemovalException>(observedSecond),
            "every cached synchronous removal fault must remain observable");
        AssertEqual(1, faulting.RemovalCalls,
            "a synchronous removal throw must be invoked once");
    }

    private static async Task TestFilePublicationRoundTrip()
    {
        using FilePublicationTestDirectory directory = new();
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] descriptor = fixture.CreateDescriptor().EncodeCanonical();
        byte[] expected = descriptor.ToArray();
        byte[] sibling = { 0x73, 0x69, 0x62, 0x6c, 0x69, 0x6e, 0x67 };
        string siblingPath = Path.Combine(directory.Path, "unrelated.keep");
        File.WriteAllBytes(siblingPath, sibling);
        try
        {
            using FileBootstrapPublicationStore publisher = new(
                directory.Path,
                directory.OwnerSid);
            using FileBootstrapPublicationReader reader = new(
                directory.Path,
                directory.OwnerSid);
            Assert(!reader.TryRead(out _),
                "an empty protected directory must report no publication");

            BootstrapPublishResult published = await publisher.TryPublishAsync(
                    descriptor,
                    MonotonicDeadline.Start(TimeProvider.System, TestTimeout),
                    CancellationToken.None)
                .ConfigureAwait(false);
            AssertEqual(BootstrapPublishStatus.Published, published.Status,
                "file publication status");
            BootstrapPublicationLease lease = published.Lease ??
                throw new InvalidOperationException(
                    "The file publication lease is missing.");

            CryptographicOperations.ZeroMemory(descriptor);
            string finalPath = directory.FinalPath;
            byte[] persisted;
            Assert(reader.TryRead(out BootstrapPublicationSnapshot? persistedSnapshot) &&
                persistedSnapshot is not null,
                "the fixed descriptor must be readable through the guarded reader");
            using BootstrapPublicationSnapshot persistedOwned =
                persistedSnapshot ?? throw new InvalidOperationException(
                    "The guarded persisted snapshot is missing.");
            {
                persisted = persistedOwned.Descriptor.ToArray();
            }
            try
            {
                Assert(persisted.AsSpan().SequenceEqual(expected),
                    "the fixed file must contain exactly the canonical descriptor");
                Assert(!ContainsSequence(persisted, fixture.Token),
                    "the public descriptor file must not contain the bearer token");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(persisted);
            }

            Assert(reader.TryRead(out BootstrapPublicationSnapshot? snapshot) &&
                snapshot is not null,
                "the independent reader must observe the publication");
            BootstrapPublicationSnapshot owned = snapshot ??
                throw new InvalidOperationException(
                    "The file publication snapshot is missing.");
            byte[] borrowed = GetPrivateField(owned, "descriptor") as byte[] ??
                throw new InvalidOperationException(
                    "The file snapshot backing is unavailable.");
            Assert(borrowed.AsSpan().SequenceEqual(expected),
                "the reader snapshot must equal the canonical descriptor");
            owned.Dispose();
            Assert(AllZero(borrowed),
                "disposing a file reader snapshot must wipe its backing buffer");

            BootstrapPublicationRemovalStatus removed =
                await lease.RemoveExactAsync(
                        MonotonicDeadline.Start(
                            TimeProvider.System,
                            TestTimeout))
                    .ConfigureAwait(false);
            AssertEqual(BootstrapPublicationRemovalStatus.Removed, removed,
                "file publication removal status");
            Assert(!File.Exists(finalPath),
                "exact removal must make the fixed name absent");
            Assert(!reader.TryRead(out _),
                "the independent reader must report absence after removal");
            Assert(File.ReadAllBytes(siblingPath).AsSpan().SequenceEqual(sibling),
                "exact removal must preserve unrelated sibling files");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(descriptor);
            CryptographicOperations.ZeroMemory(expected);
            CryptographicOperations.ZeroMemory(sibling);
        }
    }

    private static async Task TestFilePublicationCollisions()
    {
        using FilePublicationTestDirectory directory = new();
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] descriptor = fixture.CreateDescriptor().EncodeCanonical();
        try
        {
            using FileBootstrapPublicationStore owner = new(
                directory.Path,
                directory.OwnerSid);
            BootstrapPublishResult first = await owner.TryPublishAsync(
                    descriptor,
                    MonotonicDeadline.Start(TimeProvider.System, TestTimeout),
                    CancellationToken.None)
                .ConfigureAwait(false);
            BootstrapPublicationLease ownerLease = first.Lease ??
                throw new InvalidOperationException(
                    "The capacity owner lease is missing.");

            int occupiedFactoryCalls = 0;
            using (FileBootstrapPublicationStore contender = new(
                directory.Path,
                directory.OwnerSid,
                () =>
                {
                    occupiedFactoryCalls++;
                    return "must-not-be-created.tmp";
                },
                testHook: null))
            {
                BootstrapPublishResult occupied =
                    await contender.TryPublishAsync(
                            descriptor,
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                AssertEqual(BootstrapPublishStatus.Occupied, occupied.Status,
                    "occupied fixed-file publication status");
                Assert(occupied.Lease is null,
                    "an occupied fixed file must not return a lease");
                AssertEqual(0, occupiedFactoryCalls,
                    "occupied detection must not allocate a temporary name");
            }

            _ = await ownerLease.RemoveExactAsync(
                    MonotonicDeadline.Start(TimeProvider.System, TestTimeout))
                .ConfigureAwait(false);

            const string TempCollision = "endpoint-v1-collision.tmp";
            string tempPath = Path.Combine(directory.Path, TempCollision);
            byte[] tempSentinel = { 0x54, 0x45, 0x4d, 0x50 };
            File.WriteAllBytes(tempPath, tempSentinel);
            using (FileBootstrapPublicationStore collidingTemps = new(
                directory.Path,
                directory.OwnerSid,
                () => TempCollision,
                testHook: null))
            {
                await AssertThrowsAsync<IOException>(async () =>
                {
                    _ = await collidingTemps.TryPublishAsync(
                            descriptor,
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }

            Assert(File.ReadAllBytes(tempPath).AsSpan().SequenceEqual(tempSentinel),
                "temporary-name exhaustion must not overwrite its sentinel");
            Assert(!File.Exists(directory.FinalPath),
                "temporary-name exhaustion must not publish a final file");
            File.Delete(tempPath);

            bool injectedFinal = false;
            const string RaceTemp = "endpoint-v1-race.tmp";
            using (FileBootstrapPublicationStore renameCollision = new(
                directory.Path,
                directory.OwnerSid,
                () => RaceTemp,
                stage =>
                {
                    if (stage == FilePublicationStage.BeforeRename &&
                        !injectedFinal)
                    {
                        injectedFinal = true;
                        CreateProtectedTestFile(
                            directory.FinalPath,
                            directory.OwnerSid,
                            descriptor,
                            includeSystem: true);
                    }
                }))
            {
                BootstrapPublishResult collision =
                    await renameCollision.TryPublishAsync(
                            descriptor,
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                AssertEqual(BootstrapPublishStatus.Occupied, collision.Status,
                    "atomic final-name collision status");
                Assert(collision.Lease is null,
                    "an atomic final-name collision must not return a lease");
            }

            Assert(injectedFinal,
                "the final-name collision hook must run");
            Assert(!File.Exists(Path.Combine(directory.Path, RaceTemp)),
                "a final-name collision must clean only its owned temp file");
            byte[] finalSentinel = File.ReadAllBytes(directory.FinalPath);
            try
            {
                Assert(finalSentinel.AsSpan().SequenceEqual(descriptor),
                    "a final-name collision must preserve the winning file");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(finalSentinel);
            }

            File.Delete(directory.FinalPath);
            CryptographicOperations.ZeroMemory(tempSentinel);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(descriptor);
        }
    }

    private static async Task TestFilePublicationInvalidState()
    {
        using FilePublicationTestDirectory directory = new();
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] canonical = fixture.CreateDescriptor().EncodeCanonical();
        try
        {
            AssertThrows<SecurityException>(() =>
            {
                using FileBootstrapPublicationReader ignored = new(
                    directory.Path,
                    "S-1-1-0");
            });
            AssertThrows<SecurityException>(() =>
            {
                using FileBootstrapPublicationStore ignored = new(
                    directory.Path,
                    "S-1-1-0");
            });
            AssertThrows<ArgumentException>(() =>
            {
                using FileBootstrapPublicationReader ignored = new(
                    @"\\server\share\hrc-bootstrap-test",
                    directory.OwnerSid);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using FileBootstrapPublicationReader ignored = new(
                    @"\\?\C:\hrc-bootstrap-test",
                    directory.OwnerSid);
            });

            using (FilePublicationTestDirectory wrongDirectory = new(
                includeSystem: false))
            {
                AssertThrows<SecurityException>(() =>
                {
                    using FileBootstrapPublicationReader ignored = new(
                        wrongDirectory.Path,
                        wrongDirectory.OwnerSid);
                });
            }

            byte[][] invalidBodies =
            {
                new byte[] { 0x01, 0x02, 0x03 },
                canonical.Concat(new byte[] { 0x00 }).ToArray(),
                Enumerable.Repeat(
                        (byte)0x41,
                        BootstrapDescriptor.MaximumEncodedLength + 1)
                    .ToArray(),
            };
            foreach (byte[] invalid in invalidBodies)
            {
                CreateProtectedTestFile(
                    directory.FinalPath,
                    directory.OwnerSid,
                    invalid,
                    includeSystem: true);
                using FileBootstrapPublicationReader reader = new(
                    directory.Path,
                    directory.OwnerSid);
                using FileBootstrapPublicationStore publisher = new(
                    directory.Path,
                    directory.OwnerSid);
                AssertThrowsAny(
                    () => reader.TryRead(out _),
                    typeof(FormatException),
                    typeof(InvalidDataException));
                await AssertThrowsAnyAsync(async () =>
                {
                    _ = await publisher.TryPublishAsync(
                            canonical,
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }, typeof(FormatException), typeof(InvalidDataException))
                    .ConfigureAwait(false);
                Assert(File.ReadAllBytes(directory.FinalPath)
                        .AsSpan()
                        .SequenceEqual(invalid),
                    "invalid occupied state must remain unchanged");
                File.Delete(directory.FinalPath);
                CryptographicOperations.ZeroMemory(invalid);
            }

            CreateProtectedTestFile(
                directory.FinalPath,
                directory.OwnerSid,
                canonical,
                includeSystem: false);
            using (FileBootstrapPublicationReader reader = new(
                directory.Path,
                directory.OwnerSid))
            {
                AssertThrows<SecurityException>(() => reader.TryRead(out _));
            }

            Assert(File.ReadAllBytes(directory.FinalPath)
                    .AsSpan()
                    .SequenceEqual(canonical),
                "wrongly secured final state must not be adopted or deleted");
            File.Delete(directory.FinalPath);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
        }
    }

    private static async Task TestFilePublicationAbaAndReaderRace()
    {
        using FilePublicationTestDirectory directory = new();
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] descriptor = fixture.CreateDescriptor().EncodeCanonical();
        BootstrapPublicationSnapshot? preRemoval = null;
        string removalMovedPath = directory.Path + "-after-removal-aba";
        try
        {
            using FileBootstrapPublicationReader reader = new(
                directory.Path,
                directory.OwnerSid);
            using (FileBootstrapPublicationStore firstStore = new(
                directory.Path,
                directory.OwnerSid,
                () => "endpoint-v1-first.tmp",
                stage =>
                {
                    if (stage == FilePublicationStage.BeforeDisposition)
                    {
                        Assert(reader.TryRead(out preRemoval) &&
                            preRemoval is not null,
                            "a reader admitted before disposition must get a snapshot");
                    }
                }))
            {
                BootstrapPublishResult first = await firstStore.TryPublishAsync(
                        descriptor,
                        MonotonicDeadline.Start(
                            TimeProvider.System,
                            TestTimeout),
                        CancellationToken.None)
                    .ConfigureAwait(false);
                BootstrapPublicationLease stale = first.Lease ??
                    throw new InvalidOperationException(
                        "The first file lease is missing.");
                _ = await stale.RemoveExactAsync(
                        MonotonicDeadline.Start(
                            TimeProvider.System,
                            TestTimeout))
                    .ConfigureAwait(false);
                Assert(preRemoval is not null &&
                    preRemoval.Descriptor.SequenceEqual(descriptor),
                    "an admitted reader snapshot must survive exact removal");

                using FileBootstrapPublicationStore replacementStore = new(
                    directory.Path,
                    directory.OwnerSid);
                BootstrapPublishResult replacement =
                    await replacementStore.TryPublishAsync(
                            descriptor,
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                BootstrapPublicationLease replacementLease = replacement.Lease ??
                    throw new InvalidOperationException(
                        "The replacement file lease is missing.");

                BootstrapPublicationRemovalStatus cached =
                    await stale.RemoveExactAsync(
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout))
                        .ConfigureAwait(false);
                AssertEqual(BootstrapPublicationRemovalStatus.Removed, cached,
                    "stale file lease cached status");
                Assert(reader.TryRead(out BootstrapPublicationSnapshot? survivor) &&
                    survivor is not null,
                    "a stale lease must not remove an equal replacement");
                (survivor ?? throw new InvalidOperationException(
                    "The ABA survivor snapshot is missing.")).Dispose();
                _ = await replacementLease.RemoveExactAsync(
                        MonotonicDeadline.Start(
                            TimeProvider.System,
                            TestTimeout))
                    .ConfigureAwait(false);
            }

            bool removalReplacementCreated = false;
            FileBootstrapPublicationStore faultedStore = new(
                directory.Path,
                directory.OwnerSid,
                () => "endpoint-v1-removal-aba.tmp",
                stage =>
                {
                    if (stage == FilePublicationStage.RemovalHandleClosed &&
                        !removalReplacementCreated)
                    {
                        removalReplacementCreated = true;
                        CreateProtectedTestFile(
                            directory.FinalPath,
                            directory.OwnerSid,
                            descriptor,
                            includeSystem: true);
                    }
                });
            try
            {
                BootstrapPublishResult published =
                    await faultedStore.TryPublishAsync(
                            descriptor,
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                BootstrapPublicationLease removalLease = published.Lease ??
                    throw new InvalidOperationException(
                        "The removal-ABA lease is missing.");
                Task<BootstrapPublicationRemovalStatus> firstRemoval =
                    removalLease.RemoveExactAsync(
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout))
                        .AsTask();
                Task<BootstrapPublicationRemovalStatus> repeatedRemoval =
                    removalLease.RemoveExactAsync(
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout))
                        .AsTask();
                Assert(ReferenceEquals(firstRemoval, repeatedRemoval),
                    "a removal ABA failure must publish one cached task");
                Exception firstFailure = await CaptureExceptionAsync(
                        firstRemoval)
                    .ConfigureAwait(false);
                Exception repeatedFailure = await CaptureExceptionAsync(
                        repeatedRemoval)
                    .ConfigureAwait(false);
                Assert(ContainsException<SecurityException>(firstFailure) &&
                    ContainsException<SecurityException>(repeatedFailure),
                    "every cached removal ABA failure must remain observable");
                Assert(removalReplacementCreated,
                    "the post-handle-close replacement hook must run");
                Assert(File.ReadAllBytes(directory.FinalPath)
                        .AsSpan()
                        .SequenceEqual(descriptor),
                    "failed exact removal must preserve the replacement bytes");
                await AssertThrowsAsync<InvalidOperationException>(async () =>
                {
                    _ = await faultedStore.TryPublishAsync(
                            descriptor,
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            finally
            {
                faultedStore.Dispose();
            }

            reader.Dispose();
            Directory.Move(directory.Path, removalMovedPath);
            Assert(Directory.Exists(removalMovedPath) &&
                !Directory.Exists(directory.Path),
                "faulted publisher disposal must release the directory namespace");
            Directory.Move(removalMovedPath, directory.Path);
            File.Delete(directory.FinalPath);
            Directory.Delete(directory.Path);
            Assert(!Directory.Exists(directory.Path),
                "faulted publisher disposal must permit directory deletion");

            byte[] borrowed = GetPrivateField(preRemoval ??
                throw new InvalidOperationException(
                    "The pre-removal snapshot is missing."),
                    "descriptor") as byte[] ??
                throw new InvalidOperationException(
                    "The pre-removal snapshot backing is unavailable.");
            preRemoval.Dispose();
            Assert(AllZero(borrowed),
                "the independent pre-removal snapshot must remain wipeable");
        }
        finally
        {
            preRemoval?.Dispose();
            CryptographicOperations.ZeroMemory(descriptor);
            if (Directory.Exists(removalMovedPath))
            {
                DeleteTestDirectoryTree(removalMovedPath);
            }
        }
    }

    private static async Task TestFilePublicationFailureCleanup()
    {
        using FilePublicationTestDirectory directory = new();
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] descriptor = fixture.CreateDescriptor().EncodeCanonical();
        try
        {
            ManualTimeProvider clock = new(CanonicalTestUtcNow());
            const string DeadlineTemp = "endpoint-v1-deadline.tmp";
            using (FileBootstrapPublicationStore deadlineStore = new(
                directory.Path,
                directory.OwnerSid,
                () => DeadlineTemp,
                stage =>
                {
                    if (stage == FilePublicationStage.AfterRename)
                    {
                        clock.Advance(TestTimeout);
                    }
                }))
            {
                await AssertThrowsAsync<TimeoutException>(async () =>
                {
                    _ = await deadlineStore.TryPublishAsync(
                            descriptor,
                            MonotonicDeadline.Start(clock, TestTimeout),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }

            Assert(!File.Exists(directory.FinalPath) &&
                !File.Exists(Path.Combine(directory.Path, DeadlineTemp)),
                "a post-rename deadline failure must remove its exact owned file");

            using CancellationTokenSource cancellation = new();
            const string CancellationTemp = "endpoint-v1-cancel.tmp";
            using (FileBootstrapPublicationStore cancellationStore = new(
                directory.Path,
                directory.OwnerSid,
                () => CancellationTemp,
                stage =>
                {
                    if (stage == FilePublicationStage.TempCreated)
                    {
                        cancellation.Cancel();
                    }
                }))
            {
                await AssertThrowsAsync<OperationCanceledException>(async () =>
                {
                    _ = await cancellationStore.TryPublishAsync(
                            descriptor,
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout),
                            cancellation.Token)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }

            Assert(!File.Exists(directory.FinalPath) &&
                !File.Exists(Path.Combine(directory.Path, CancellationTemp)),
                "a cancelled publish must remove its exact owned temp file");

            ManualTimeProvider removalClock = new(CanonicalTestUtcNow());
            bool removalAdvanced = false;
            using FileBootstrapPublicationStore removalStore = new(
                directory.Path,
                directory.OwnerSid,
                () => "endpoint-v1-removal-deadline.tmp",
                stage =>
                {
                    if (stage == FilePublicationStage.BeforeDisposition &&
                        !removalAdvanced)
                    {
                        removalAdvanced = true;
                        removalClock.Advance(TestTimeout);
                    }
                });
            BootstrapPublishResult published =
                await removalStore.TryPublishAsync(
                        descriptor,
                        MonotonicDeadline.Start(
                            TimeProvider.System,
                            TestTimeout),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            BootstrapPublicationLease lease = published.Lease ??
                throw new InvalidOperationException(
                    "The removal-deadline lease is missing.");
            BootstrapPublicationRemovalStatus lateRemoval =
                await lease.RemoveExactAsync(
                        MonotonicDeadline.Start(removalClock, TestTimeout))
                    .ConfigureAwait(false);
            Assert(removalAdvanced,
                "the removal deadline hook must run");
            AssertEqual(
                BootstrapPublicationRemovalStatus.RemovedAfterDeadline,
                lateRemoval,
                "verified late file removal status");
            Assert(!File.Exists(directory.FinalPath),
                "late removal must still verify fixed-name absence");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(descriptor);
        }
    }

    private static async Task TestFilePublicationCollisionBounds()
    {
        const int StatusObjectNameCollision = unchecked((int)0xC0000035);
        const int StatusPending = 0x00000103;
        const int StatusUnsuccessful = unchecked((int)0xC0000001);
        Assert(GuardedDescriptorDirectory.ClassifyRenameResult(0),
            "successful rename status classification");
        Assert(!GuardedDescriptorDirectory.ClassifyRenameResult(
                StatusObjectNameCollision),
            "immediate name collision classification");
        AssertThrows<IOException>(() =>
            GuardedDescriptorDirectory.ClassifyRenameResult(StatusPending));
        AssertThrows<IOException>(() =>
            GuardedDescriptorDirectory.ClassifyRenameResult(
                StatusUnsuccessful));

        using FilePublicationTestDirectory directory = new();
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] descriptor = fixture.CreateDescriptor().EncodeCanonical();
        try
        {
            using FileBootstrapPublicationStore owner = new(
                directory.Path,
                directory.OwnerSid);
            BootstrapPublishResult first = await owner.TryPublishAsync(
                    descriptor,
                    MonotonicDeadline.Start(TimeProvider.System, TestTimeout),
                    CancellationToken.None)
                .ConfigureAwait(false);
            BootstrapPublicationLease ownerLease = first.Lease ??
                throw new InvalidOperationException(
                    "The occupied-bound owner lease is missing.");

            using CancellationTokenSource occupiedCancellation = new();
            occupiedCancellation.Cancel();
            using (FileBootstrapPublicationStore cancelledContender = new(
                directory.Path,
                directory.OwnerSid))
            {
                await AssertThrowsAsync<OperationCanceledException>(async () =>
                {
                    _ = await cancelledContender.TryPublishAsync(
                            descriptor,
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout),
                            occupiedCancellation.Token)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }

            ManualTimeProvider occupiedClock = new(CanonicalTestUtcNow());
            MonotonicDeadline occupiedDeadline = MonotonicDeadline.Start(
                occupiedClock,
                TestTimeout);
            occupiedClock.Advance(TestTimeout);
            using (FileBootstrapPublicationStore expiredContender = new(
                directory.Path,
                directory.OwnerSid))
            {
                await AssertThrowsAsync<TimeoutException>(async () =>
                {
                    _ = await expiredContender.TryPublishAsync(
                            descriptor,
                            occupiedDeadline,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }

            _ = await ownerLease.RemoveExactAsync(
                    MonotonicDeadline.Start(TimeProvider.System, TestTimeout))
                .ConfigureAwait(false);

            const string TempCollision = "endpoint-v1-bounded-collision.tmp";
            byte[] sentinel = { 0x62, 0x6f, 0x75, 0x6e, 0x64 };
            string tempPath = Path.Combine(directory.Path, TempCollision);
            File.WriteAllBytes(tempPath, sentinel);
            ManualTimeProvider tempClock = new(CanonicalTestUtcNow());
            int attempts = 0;
            using (FileBootstrapPublicationStore tempDeadline = new(
                directory.Path,
                directory.OwnerSid,
                () =>
                {
                    attempts++;
                    if (attempts == 1)
                    {
                        tempClock.Advance(TestTimeout);
                    }

                    return TempCollision;
                },
                testHook: null))
            {
                await AssertThrowsAsync<TimeoutException>(async () =>
                {
                    _ = await tempDeadline.TryPublishAsync(
                            descriptor,
                            MonotonicDeadline.Start(tempClock, TestTimeout),
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }

            AssertEqual(1, attempts,
                "temp-collision deadline check attempt count");
            Assert(File.ReadAllBytes(tempPath).AsSpan().SequenceEqual(sentinel),
                "temp-collision deadline must preserve its sentinel");
            File.Delete(tempPath);

            bool cancellationInjected = false;
            using CancellationTokenSource renameCancellation = new();
            using (FileBootstrapPublicationStore renameCollision = new(
                directory.Path,
                directory.OwnerSid,
                () => "endpoint-v1-bounded-rename.tmp",
                stage =>
                {
                    if (stage == FilePublicationStage.BeforeRename &&
                        !cancellationInjected)
                    {
                        cancellationInjected = true;
                        CreateProtectedTestFile(
                            directory.FinalPath,
                            directory.OwnerSid,
                            descriptor,
                            includeSystem: true);
                        renameCancellation.Cancel();
                    }
                }))
            {
                await AssertThrowsAsync<OperationCanceledException>(async () =>
                {
                    _ = await renameCollision.TryPublishAsync(
                            descriptor,
                            MonotonicDeadline.Start(
                                TimeProvider.System,
                                TestTimeout),
                            renameCancellation.Token)
                        .ConfigureAwait(false);
                }).ConfigureAwait(false);
            }

            Assert(cancellationInjected,
                "rename-collision cancellation hook must run");
            Assert(File.Exists(directory.FinalPath),
                "rename-collision cancellation must preserve the winning final");
            Assert(!File.Exists(Path.Combine(
                directory.Path,
                "endpoint-v1-bounded-rename.tmp")),
                "rename-collision cancellation must remove its owned temp");
            File.Delete(directory.FinalPath);
            CryptographicOperations.ZeroMemory(sentinel);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(descriptor);
        }
    }

    private static async Task TestFilePublicationDirectoryPinning()
    {
        using FilePublicationTestDirectory directory = new();
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] descriptor = fixture.CreateDescriptor().EncodeCanonical();
        string movedPath = directory.Path + "-moved";
        bool moveAttempted = false;
        bool moveRejected = false;
        try
        {
            using FileBootstrapPublicationStore publisher = new(
                directory.Path,
                directory.OwnerSid,
                () => "endpoint-v1-directory-pin.tmp",
                stage =>
                {
                    if (stage == FilePublicationStage.TempCreated &&
                        !moveAttempted)
                    {
                        moveAttempted = true;
                        try
                        {
                            Directory.Move(directory.Path, movedPath);
                        }
                        catch (IOException exception) when (
                            (exception.HResult & 0xffff) == 32)
                        {
                            moveRejected = true;
                        }
                    }
                });
            BootstrapPublishResult result = await publisher.TryPublishAsync(
                    descriptor,
                    MonotonicDeadline.Start(TimeProvider.System, TestTimeout),
                    CancellationToken.None)
                .ConfigureAwait(false);
            Assert(moveAttempted,
                "the guarded-directory rename hook must run");
            Assert(moveRejected,
                "the retained guarded-directory handle must reject namespace rename");
            Assert(Directory.Exists(directory.Path) &&
                !Directory.Exists(movedPath),
                "the guarded directory path must remain pinned");
            BootstrapPublicationLease lease = result.Lease ??
                throw new InvalidOperationException(
                    "The directory-pinning publication lease is missing.");
            _ = await lease.RemoveExactAsync(
                    MonotonicDeadline.Start(TimeProvider.System, TestTimeout))
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(descriptor);
            if (Directory.Exists(directory.Path))
            {
                DeleteTestDirectoryTree(directory.Path);
            }

            if (Directory.Exists(movedPath))
            {
                DeleteTestDirectoryTree(movedPath);
            }
        }
    }

    private static async Task TestFilePublicationMultiPageEnumeration()
    {
        using FilePublicationTestDirectory directory = new();
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] descriptor = fixture.CreateDescriptor().EncodeCanonical();
        const int MultiPageCount = 400;
        const int OverBudgetCount = 2_300;
        string longSuffix = new('x', 180);
        try
        {
            for (int index = 0; index < MultiPageCount; index++)
            {
                File.WriteAllBytes(
                    Path.Combine(
                        directory.Path,
                        "a-sentinel-" + index.ToString("D4") +
                            "-" + longSuffix + ".bin"),
                    new byte[] { checked((byte)(index & 0xff)) });
            }

            using FileBootstrapPublicationStore publisher = new(
                directory.Path,
                directory.OwnerSid);
            using FileBootstrapPublicationReader reader = new(
                directory.Path,
                directory.OwnerSid);
            BootstrapPublishResult published = await publisher.TryPublishAsync(
                    descriptor,
                    MonotonicDeadline.Start(TimeProvider.System, TestTimeout),
                    CancellationToken.None)
                .ConfigureAwait(false);
            BootstrapPublicationLease lease = published.Lease ??
                throw new InvalidOperationException(
                    "The multi-page enumeration lease is missing.");
            Assert(reader.TryRead(out BootstrapPublicationSnapshot? snapshot) &&
                snapshot is not null,
                "the reader must find the fixed name beyond one enumeration page");
            (snapshot ?? throw new InvalidOperationException(
                "The multi-page snapshot is missing.")).Dispose();
            _ = await lease.RemoveExactAsync(
                    MonotonicDeadline.Start(TimeProvider.System, TestTimeout))
                .ConfigureAwait(false);
            AssertEqual(
                MultiPageCount,
                Directory.EnumerateFiles(directory.Path, "a-sentinel-*.bin").Count(),
                "multi-page sentinel preservation count");

            for (int index = MultiPageCount;
                index < OverBudgetCount;
                index++)
            {
                File.WriteAllBytes(
                    Path.Combine(
                        directory.Path,
                        "a-sentinel-" + index.ToString("D4") +
                            "-" + longSuffix + ".bin"),
                    new byte[] { checked((byte)(index & 0xff)) });
            }

            AssertThrows<SecurityException>(() => reader.TryRead(out _));
            AssertEqual(
                OverBudgetCount,
                Directory.EnumerateFiles(directory.Path, "a-sentinel-*.bin").Count(),
                "enumeration-budget sentinel preservation count");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(descriptor);
        }
    }

    private static async Task TestFilePublicationIdentityReplacement()
    {
        using FilePublicationTestDirectory directory = new();
        using DescriptorFixture fixture = CreateDescriptorFixture();
        byte[] original = fixture.CreateDescriptor().EncodeCanonical();
        bool replacementAttempted = false;
        bool replacementRejected = false;
        try
        {
            using FileBootstrapPublicationStore publisher = new(
                directory.Path,
                directory.OwnerSid,
                () => "endpoint-v1-identity.tmp",
                stage =>
                {
                    if (stage != FilePublicationStage.FinalValidated ||
                        replacementAttempted)
                    {
                        return;
                    }

                    replacementAttempted = true;
                    try
                    {
                        PosixUnlinkTestFile(directory.FinalPath);
                    }
                    catch (Win32Exception exception) when (
                        exception.NativeErrorCode == 32)
                    {
                        replacementRejected = true;
                    }
                    catch (IOException exception) when (
                        (exception.HResult & 0xffff) == 32)
                    {
                        replacementRejected = true;
                    }
                });
            BootstrapPublishResult published = await publisher.TryPublishAsync(
                    original,
                    MonotonicDeadline.Start(
                        TimeProvider.System,
                        TestTimeout),
                    CancellationToken.None)
                .ConfigureAwait(false);
            Assert(replacementAttempted,
                "the final-validation replacement hook must run");
            Assert(replacementRejected,
                "the retained publication handle must reject name replacement");
            BootstrapPublicationLease lease = published.Lease ??
                throw new InvalidOperationException(
                    "The replacement-defence lease is missing.");
            using FileBootstrapPublicationReader reader = new(
                directory.Path,
                directory.OwnerSid);
            Assert(reader.TryRead(out BootstrapPublicationSnapshot? snapshot) &&
                snapshot is not null,
                "the guarded name must still resolve to the published descriptor");
            using BootstrapPublicationSnapshot owned = snapshot ??
                throw new InvalidOperationException(
                    "The replacement-defence snapshot is missing.");
            Assert(owned.Descriptor.SequenceEqual(original),
                "the guarded publication bytes must remain unchanged");
            _ = await lease.RemoveExactAsync(
                    MonotonicDeadline.Start(
                        TimeProvider.System,
                        TestTimeout))
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(original);
        }
    }

    private static Task TestFilePublicationReparsePoint()
    {
        using FilePublicationTestDirectory directory = new();
        string target = Path.Combine(directory.Path, "reparse-target");
        string leafJunction = directory.FinalPath;
        string rootJunction = Path.Combine(
            directory.Path,
            "reparse-root-junction");
        CreateProtectedTestDirectory(
            target,
            directory.OwnerSid,
            includeSystem: true);
        CreateProtectedTestDirectory(
            leafJunction,
            directory.OwnerSid,
            includeSystem: true);
        CreateProtectedTestDirectory(
            rootJunction,
            directory.OwnerSid,
            includeSystem: true);
        byte[] sentinel = { 0x6a, 0x75, 0x6e, 0x63, 0x74, 0x69, 0x6f, 0x6e };
        string targetSentinel = Path.Combine(target, "unchanged.bin");
        File.WriteAllBytes(targetSentinel, sentinel);
        try
        {
            CreateDirectoryJunction(leafJunction, target);
            CreateDirectoryJunction(rootJunction, target);
            Assert((File.GetAttributes(leafJunction) &
                    FileAttributes.ReparsePoint) != 0,
                "the fixed-leaf junction must expose the reparse attribute");
            Assert((File.GetAttributes(rootJunction) &
                    FileAttributes.ReparsePoint) != 0,
                "the root junction must expose the reparse attribute");
            Assert(File.ReadAllBytes(Path.Combine(
                        leafJunction,
                        "unchanged.bin"))
                    .AsSpan()
                    .SequenceEqual(sentinel),
                "the fixed-leaf junction must resolve before rejection");
            using (FileBootstrapPublicationReader reader = new(
                directory.Path,
                directory.OwnerSid))
            {
                AssertThrowsAny(
                    () => reader.TryRead(out _),
                    typeof(SecurityException),
                    typeof(Win32Exception),
                    typeof(IOException));
            }

            AssertThrows<SecurityException>(() =>
            {
                using FileBootstrapPublicationReader ignored = new(
                    rootJunction,
                    directory.OwnerSid);
            });
            Assert(File.ReadAllBytes(targetSentinel)
                    .AsSpan()
                    .SequenceEqual(sentinel),
                "reparse rejection must not alter its target");
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sentinel);
        }
    }

    private static Task TestFilePublicationRetainedRootRename()
    {
        using FilePublicationTestDirectory source = new();
        using FilePublicationTestDirectory destination = new();
        const string SourceName = "endpoint-v1-native-source.tmp";
        const string TargetName = "endpoint-v1-native-target.bin";
        byte[] firstBytes = { 0x72, 0x6f, 0x6f, 0x74, 0x2d, 0x31 };
        byte[] secondBytes = { 0x72, 0x6f, 0x6f, 0x74, 0x2d, 0x32 };
        string firstSource = Path.Combine(source.Path, SourceName);
        string secondSource = Path.Combine(source.Path, SourceName + ".second");
        string target = Path.Combine(destination.Path, TargetName);
        try
        {
            CreateProtectedTestFile(
                firstSource,
                source.OwnerSid,
                firstBytes,
                includeSystem: true);
            using GuardedDescriptorDirectory retainedRoot =
                GuardedDescriptorDirectory.Open(
                    destination.Path,
                    destination.OwnerSid,
                    testHook: null);
            using SafeFileHandle first = OpenRenameSourceForTest(firstSource);
            Assert(retainedRoot.TryRenameNoReplace(first, TargetName),
                "the retained root rename must succeed");
            first.Dispose();
            Assert(!File.Exists(firstSource) && File.Exists(target),
                "the native rename must move the source only into the retained root");
            Assert(File.ReadAllBytes(target).AsSpan().SequenceEqual(firstBytes),
                "the retained-root target bytes must match the exact source");

            CreateProtectedTestFile(
                secondSource,
                source.OwnerSid,
                secondBytes,
                includeSystem: true);
            using SafeFileHandle second = OpenRenameSourceForTest(secondSource);
            Assert(!retainedRoot.TryRenameNoReplace(second, TargetName),
                "the retained-root collision must fail without replacement");
            second.Dispose();
            Assert(File.Exists(secondSource) && File.Exists(target),
                "a retained-root collision must preserve source and target names");
            Assert(File.ReadAllBytes(target).AsSpan().SequenceEqual(firstBytes) &&
                File.ReadAllBytes(secondSource).AsSpan().SequenceEqual(secondBytes),
                "a retained-root collision must preserve both exact byte sequences");
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(firstBytes);
            CryptographicOperations.ZeroMemory(secondBytes);
        }
    }

    private static Task TestTrustedArtifactIdentity()
    {
        using FilePublicationTestDirectory directory = new();
        string path = Path.Combine(directory.Path, "trusted-artifact.bin");
        string replacement = Path.Combine(
            directory.Path,
            "trusted-artifact-replacement.bin");
        byte[] bytes = new byte[150_000];
        byte[] replacementBytes = new byte[150_000];
        byte[]? digest = null;
        byte[]? digestCopy = null;
        byte[]? identifierCopy = null;
        TrustedArtifactLease? lease = null;
        try
        {
            RandomNumberGenerator.Fill(bytes);
            bytes.CopyTo(replacementBytes, 0);
            replacementBytes[0] ^= 0xff;
            File.WriteAllBytes(path, bytes);
            File.WriteAllBytes(replacement, replacementBytes);
            digest = SHA256.HashData(bytes);

            lease = TrustedArtifactIdentity.Open(path, bytes.Length, digest);
            AssertEqual(path, lease.Path, "trusted artifact path");
            AssertEqual((long)bytes.Length, lease.Length,
                "trusted artifact length");

            digestCopy = lease.CopySha256Digest();
            Assert(digestCopy.AsSpan().SequenceEqual(digest),
                "the trusted artifact digest must match its verified input");
            digestCopy[0] ^= 0xff;
            byte[] secondDigestCopy = lease.CopySha256Digest();
            try
            {
                Assert(secondDigestCopy.AsSpan().SequenceEqual(digest),
                    "digest callers must receive independent copies");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secondDigestCopy);
            }

            identifierCopy = lease.Identity.CopyIdentifier();
            AssertEqual(
                TrustedArtifactFileIdentity.IdentifierLength,
                identifierCopy.Length,
                "trusted artifact identifier length");
            byte firstIdentifierByte = identifierCopy[0];
            identifierCopy[0] ^= 0xff;
            byte[] secondIdentifierCopy = lease.Identity.CopyIdentifier();
            try
            {
                AssertEqual(
                    firstIdentifierByte,
                    secondIdentifierCopy[0],
                    "trusted artifact identifier copy ownership");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secondIdentifierCopy);
            }

            lease.RevalidateCurrentPath();
            using (FileStream secondReader = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                AssertEqual((long)bytes.Length, secondReader.Length,
                    "concurrent read-only artifact length");
                lease.RevalidateCurrentPath();
            }

            AssertThrowsAny(
                () =>
                {
                    using FileStream ignored = new(
                        path,
                        FileMode.Open,
                        FileAccess.Write,
                        FileShare.ReadWrite | FileShare.Delete);
                },
                typeof(IOException),
                typeof(UnauthorizedAccessException));
            AssertThrowsAny(
                () => File.Delete(path),
                typeof(IOException),
                typeof(UnauthorizedAccessException));
            AssertThrowsAny(
                () => File.Move(replacement, path, overwrite: true),
                typeof(IOException),
                typeof(UnauthorizedAccessException));
            Assert(File.Exists(path) && File.Exists(replacement),
                "a blocked replacement must preserve both exact paths");
            lease.RevalidateCurrentPath();

            lease.Dispose();
            AssertThrows<ObjectDisposedException>(lease.RevalidateCurrentPath);
            AssertThrows<ObjectDisposedException>(() =>
            {
                _ = lease.CopySha256Digest();
            });
            lease = null;
            File.Move(replacement, path, overwrite: true);
            Assert(!File.Exists(replacement) &&
                    File.ReadAllBytes(path).AsSpan().SequenceEqual(
                        replacementBytes),
                "replacement must become possible only after lease disposal");
            return Task.CompletedTask;
        }
        finally
        {
            lease?.Dispose();
            CryptographicOperations.ZeroMemory(bytes);
            CryptographicOperations.ZeroMemory(replacementBytes);
            if (digest is not null)
            {
                CryptographicOperations.ZeroMemory(digest);
            }

            if (digestCopy is not null)
            {
                CryptographicOperations.ZeroMemory(digestCopy);
            }

            if (identifierCopy is not null)
            {
                CryptographicOperations.ZeroMemory(identifierCopy);
            }
        }
    }

    private static Task TestTrustedArtifactInvalidInputs()
    {
        using FilePublicationTestDirectory directory = new();
        string path = Path.Combine(directory.Path, "trusted-input.bin");
        byte[] bytes = { 0x74, 0x72, 0x75, 0x73, 0x74 };
        byte[] digest = SHA256.HashData(bytes);
        byte[] wrongDigest = (byte[])digest.Clone();
        try
        {
            wrongDigest[0] ^= 0xff;
            File.WriteAllBytes(path, bytes);
            AssertThrows<ArgumentOutOfRangeException>(() =>
            {
                using TrustedArtifactLease ignored =
                    TrustedArtifactIdentity.Open(path, -1, digest);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedArtifactLease ignored =
                    TrustedArtifactIdentity.Open(path, bytes.Length, new byte[31]);
            });
            AssertThrows<SecurityException>(() =>
            {
                using TrustedArtifactLease ignored =
                    TrustedArtifactIdentity.Open(path, bytes.Length + 1, digest);
            });
            AssertThrows<SecurityException>(() =>
            {
                using TrustedArtifactLease ignored =
                    TrustedArtifactIdentity.Open(path, bytes.Length, wrongDigest);
            });

            string relative = Path.GetFileName(path);
            string nonCanonical = Path.Combine(
                directory.Path,
                ".",
                Path.GetFileName(path));
            string dotDotPath = Path.Combine(
                directory.Path,
                "unused",
                "..",
                Path.GetFileName(path));
            string forwardSlashPath = path.Replace(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedArtifactLease ignored =
                    TrustedArtifactIdentity.Open(relative, bytes.Length, digest);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedArtifactLease ignored = TrustedArtifactIdentity.Open(
                    @"\\server\share\trusted-input.bin",
                    bytes.Length,
                    digest);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedArtifactLease ignored = TrustedArtifactIdentity.Open(
                    @"\\?\C:\trusted-input.bin",
                    bytes.Length,
                    digest);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedArtifactLease ignored = TrustedArtifactIdentity.Open(
                    path + ":alternate-stream",
                    bytes.Length,
                    digest);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedArtifactLease ignored = TrustedArtifactIdentity.Open(
                    nonCanonical,
                    bytes.Length,
                    digest);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedArtifactLease ignored = TrustedArtifactIdentity.Open(
                    dotDotPath,
                    bytes.Length,
                    digest);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedArtifactLease ignored = TrustedArtifactIdentity.Open(
                    forwardSlashPath,
                    bytes.Length,
                    digest);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using TrustedArtifactLease ignored = TrustedArtifactIdentity.Open(
                    path + Path.DirectorySeparatorChar,
                    bytes.Length,
                    digest);
            });
            AssertThrowsAny(
                () =>
                {
                    using TrustedArtifactLease ignored = TrustedArtifactIdentity.Open(
                        directory.Path,
                        0,
                        SHA256.HashData(Array.Empty<byte>()));
                },
                typeof(SecurityException),
                typeof(Win32Exception),
                typeof(UnauthorizedAccessException));
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            CryptographicOperations.ZeroMemory(digest);
            CryptographicOperations.ZeroMemory(wrongDigest);
        }
    }

    private static Task TestTrustedArtifactPathGuards()
    {
        using FilePublicationTestDirectory directory = new();
        string targetDirectory = Path.Combine(directory.Path, "artifact-target");
        string junction = Path.Combine(directory.Path, "artifact-junction");
        string targetPath = Path.Combine(targetDirectory, "artifact.bin");
        string indirectPath = Path.Combine(junction, "artifact.bin");
        string hardLink = Path.Combine(directory.Path, "artifact-hard-link.bin");
        byte[] bytes = { 0x70, 0x61, 0x74, 0x68, 0x2d, 0x67, 0x75, 0x61, 0x72, 0x64 };
        byte[] digest = SHA256.HashData(bytes);
        byte[] emptyDigest = SHA256.HashData(Array.Empty<byte>());
        try
        {
            CreateProtectedTestDirectory(
                targetDirectory,
                directory.OwnerSid,
                includeSystem: true);
            CreateProtectedTestDirectory(
                junction,
                directory.OwnerSid,
                includeSystem: true);
            File.WriteAllBytes(targetPath, bytes);
            CreateDirectoryJunction(junction, targetDirectory);
            Assert((File.GetAttributes(junction) &
                    FileAttributes.ReparsePoint) != 0,
                "the trusted artifact fixture must be a real junction");
            AssertThrows<SecurityException>(() =>
            {
                using TrustedArtifactLease ignored = TrustedArtifactIdentity.Open(
                    junction,
                    0,
                    emptyDigest);
            });
            AssertThrows<SecurityException>(() =>
            {
                using TrustedArtifactLease ignored = TrustedArtifactIdentity.Open(
                    indirectPath,
                    bytes.Length,
                    digest);
            });

            CreateHardLinkForTest(hardLink, targetPath);
            AssertThrows<SecurityException>(() =>
            {
                using TrustedArtifactLease ignored = TrustedArtifactIdentity.Open(
                    targetPath,
                    bytes.Length,
                    digest);
            });
            Assert(File.ReadAllBytes(targetPath).AsSpan().SequenceEqual(bytes),
                "reparse and hard-link rejection must preserve artifact bytes");
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            CryptographicOperations.ZeroMemory(digest);
            CryptographicOperations.ZeroMemory(emptyDigest);
        }
    }

    private static Task TestTrustedArtifactWritableMapping()
    {
        using FilePublicationTestDirectory directory = new();
        string path = Path.Combine(directory.Path, "trusted-mapped.bin");
        byte[] bytes = new byte[4_096];
        byte[]? digest = null;
        try
        {
            RandomNumberGenerator.Fill(bytes);
            File.WriteAllBytes(path, bytes);
            digest = SHA256.HashData(bytes);

            using FileStream writer = new(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.ReadWrite | FileShare.Delete);
            using MemoryMappedFile mapping = MemoryMappedFile.CreateFromFile(
                writer,
                mapName: null,
                capacity: 0,
                MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None,
                leaveOpen: true);
            using MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0,
                bytes.Length,
                MemoryMappedFileAccess.ReadWrite);
            writer.Dispose();

            AssertThrows<Win32Exception>(() =>
            {
                using TrustedArtifactLease ignored = TrustedArtifactIdentity.Open(
                    path,
                    bytes.Length,
                    digest);
            });
            view.Write(0, unchecked((byte)(bytes[0] ^ 0xff)));
            view.Flush();
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            if (digest is not null)
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }
    }

    private static Task TestTrustedArtifactSiblingBoundary()
    {
        using FilePublicationTestDirectory directory = new();
        string artifactPath = Path.Combine(directory.Path, "trusted-role.exe");
        string siblingDllPath = Path.Combine(directory.Path, "mutable-role.dll");
        string siblingConfigPath = Path.Combine(
            directory.Path,
            "trusted-role.runtimeconfig.json");
        byte[] artifact = { 0x65, 0x78, 0x65 };
        byte[] originalDll = { 0x64, 0x6c, 0x6c, 0x2d, 0x31 };
        byte[] changedDll = { 0x64, 0x6c, 0x6c, 0x2d, 0x32 };
        byte[] originalConfig = { 0x63, 0x66, 0x67, 0x2d, 0x31 };
        byte[] changedConfig = { 0x63, 0x66, 0x67, 0x2d, 0x32 };
        byte[]? digest = null;
        try
        {
            File.WriteAllBytes(artifactPath, artifact);
            File.WriteAllBytes(siblingDllPath, originalDll);
            File.WriteAllBytes(siblingConfigPath, originalConfig);
            digest = SHA256.HashData(artifact);
            using TrustedArtifactLease lease = TrustedArtifactIdentity.Open(
                artifactPath,
                artifact.Length,
                digest);

            File.WriteAllBytes(siblingDllPath, changedDll);
            File.WriteAllBytes(siblingConfigPath, changedConfig);
            lease.RevalidateCurrentPath();
            Assert(File.ReadAllBytes(siblingDllPath)
                    .AsSpan()
                    .SequenceEqual(changedDll) &&
                File.ReadAllBytes(siblingConfigPath)
                    .AsSpan()
                    .SequenceEqual(changedConfig),
                "one trusted artifact lease must not imply sibling role identity");
            Assert(File.ReadAllBytes(artifactPath)
                    .AsSpan()
                    .SequenceEqual(artifact),
                "sibling mutation must leave the leased artifact unchanged");
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(artifact);
            CryptographicOperations.ZeroMemory(originalDll);
            CryptographicOperations.ZeroMemory(changedDll);
            CryptographicOperations.ZeroMemory(originalConfig);
            CryptographicOperations.ZeroMemory(changedConfig);
            if (digest is not null)
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }
    }

    private static async Task TestBrokerClaim()
    {
        using BrokerFixture fixture = BrokerFixture.Start();
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            fixture.Session.PublishPipeName,
            revokeAfterPublish: false,
            malformedRevoke: false,
            allowExpectedClose: false,
            delayMilliseconds: 0,
            fixture.PublicationLifetime).ConfigureAwait(false);
        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
        await WaitForPublicationAsync(fixture.Store, run).ConfigureAwait(false);
        byte[] brokerTokenBorrow = GetBrokerTokenBacking(fixture.Session);
        await fixture.SendBrokerClaimAsync(
            malformedTranscript: false,
            badProof: false,
            allowExpectedClose: false,
            delayMilliseconds: 0).ConfigureAwait(false);
        BootstrapBrokerSessionResult result = await run.ConfigureAwait(false);
        AssertEqual(BootstrapBrokerOutcome.Claimed, result.Outcome,
            "broker outcome");
        AssertEqual(BootstrapBrokerSessionState.Claimed, fixture.Session.State,
            "broker state");
        Assert(!fixture.Store.TryRead(out _),
            "a completed claim must remove the publication");
        Assert(AllZero(brokerTokenBorrow),
            "a completed claim must wipe the broker token backing array");
        await AssertThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await fixture.Session.RunAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
        await fixture.RequireCleanExitAsync().ConfigureAwait(false);
    }

    private static async Task TestBrokerRoleBindings()
    {
        using BrokerFixture fixture = BrokerFixture.Start();
        using InMemoryBootstrapPublicationStore store = new();
        AssertThrows<ArgumentException>(() =>
        {
            using BootstrapBrokerSession _ = new(
                fixture.BrokerBinding,
                fixture.ControllerBinding,
                fixture.BrokerBinding,
                store,
                TimeProvider.System,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2));
        });
        AssertThrows<ArgumentException>(() =>
        {
            using BootstrapBrokerSession _ = new(
                fixture.ObserverBinding,
                fixture.BrokerBinding,
                fixture.BrokerBinding,
                store,
                TimeProvider.System,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2));
        });
        BootstrapBinding wrongContext = BindingWith(
            fixture.ControllerBinding,
            userSid: "S-1-1-0");
        AssertThrows<ArgumentException>(() =>
        {
            using BootstrapBrokerSession _ = new(
                fixture.ObserverBinding,
                wrongContext,
                fixture.BrokerBinding,
                store,
                TimeProvider.System,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2));
        });
        BootstrapBinding wrongBroker = BindingWith(
            fixture.BrokerBinding,
            processId: checked(fixture.BrokerBinding.ProcessId + 100_000));
        AssertThrows<SecurityException>(() =>
        {
            using BootstrapBrokerSession _ = new(
                fixture.ObserverBinding,
                fixture.ControllerBinding,
                wrongBroker,
                store,
                TimeProvider.System,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2));
        });
        await fixture.RequireCleanExitAsync(
                observerUnused: true,
                controllerUnused: true)
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerDisposeBeforeRun()
    {
        using BrokerFixture fixture = BrokerFixture.Start();
        string publishName = fixture.Session.PublishPipeName;
        Task firstDisposal = fixture.Session.DisposeAsync().AsTask();
        Task secondDisposal = fixture.Session.DisposeAsync().AsTask();
        Assert(ReferenceEquals(firstDisposal, secondDisposal),
            "concurrent asynchronous disposal must publish one completion task");
        await Task.WhenAll(firstDisposal, secondDisposal)
            .ConfigureAwait(false);
        AssertEqual(BootstrapBrokerSessionState.Disposed, fixture.Session.State,
            "pre-run disposal state");
        await AssertThrowsAsync<ObjectDisposedException>(async () =>
        {
            _ = await fixture.Session.RunAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
        using ProtectedNamedPipe replacement = ProtectedNamedPipe.Create(
            publishName,
            fixture.ObserverBinding);
        await fixture.RequireCleanExitAsync(
                observerUnused: true,
                controllerUnused: true)
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerRevoke()
    {
        using BrokerFixture fixture = BrokerFixture.Start();
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            fixture.Session.PublishPipeName,
            revokeAfterPublish: true,
            malformedRevoke: false,
            allowExpectedClose: false,
            delayMilliseconds: 100,
            fixture.PublicationLifetime).ConfigureAwait(false);
        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
        await WaitForPublicationAsync(fixture.Store, run).ConfigureAwait(false);
        byte[] brokerTokenBorrow = GetBrokerTokenBacking(fixture.Session);
        BootstrapBrokerSessionResult result = await run
            .ConfigureAwait(false);
        AssertEqual(BootstrapBrokerOutcome.Revoked, result.Outcome,
            "broker outcome");
        Assert(!fixture.Store.TryRead(out _),
            "a completed revocation must remove the publication");
        Assert(AllZero(brokerTokenBorrow),
            "a completed revocation must wipe the broker token backing array");
        await fixture.RequireCleanExitAsync(controllerUnused: true)
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerRaces()
    {
        await RunBrokerRaceAsync(claimDelay: 0, revokeDelay: 150,
            BootstrapBrokerOutcome.Claimed).ConfigureAwait(false);
        await RunBrokerRaceAsync(claimDelay: 300, revokeDelay: 50,
            BootstrapBrokerOutcome.Revoked).ConfigureAwait(false);
        BootstrapBrokerOutcome simultaneous = await RunBrokerRaceAsync(
                claimDelay: 100,
                revokeDelay: 100,
                expected: null)
            .ConfigureAwait(false);
        Assert(simultaneous is BootstrapBrokerOutcome.Claimed or
            BootstrapBrokerOutcome.Revoked,
            "a simultaneous race must select exactly one terminal outcome");
    }

    private static async Task<BootstrapBrokerOutcome> RunBrokerRaceAsync(
        int claimDelay,
        int revokeDelay,
        BootstrapBrokerOutcome? expected)
    {
        using BrokerFixture fixture = BrokerFixture.Start();
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            fixture.Session.PublishPipeName,
            revokeAfterPublish: true,
            malformedRevoke: false,
            allowExpectedClose: true,
            delayMilliseconds: revokeDelay,
            fixture.PublicationLifetime).ConfigureAwait(false);
        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
        await WaitForPublicationAsync(fixture.Store, run).ConfigureAwait(false);
        await fixture.SendBrokerClaimAsync(
            malformedTranscript: false,
            badProof: false,
            allowExpectedClose: true,
            delayMilliseconds: claimDelay).ConfigureAwait(false);
        BootstrapBrokerSessionResult result = await run.ConfigureAwait(false);
        if (expected is BootstrapBrokerOutcome value)
        {
            AssertEqual(value, result.Outcome, "race outcome");
        }

        Assert(!fixture.Store.TryRead(out _),
            "a race must remove the exact publication");
        await fixture.RequireCleanExitAsync()
            .ConfigureAwait(false);
        return result.Outcome;
    }

    private static async Task TestBrokerTranscriptMismatch()
    {
        using BrokerFixture fixture = BrokerFixture.Start();
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            fixture.Session.PublishPipeName,
            revokeAfterPublish: false,
            malformedRevoke: false,
            allowExpectedClose: false,
            delayMilliseconds: 0,
            fixture.PublicationLifetime).ConfigureAwait(false);
        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
        await WaitForPublicationAsync(fixture.Store, run).ConfigureAwait(false);
        await fixture.SendBrokerClaimAsync(
            malformedTranscript: true,
            badProof: false,
            allowExpectedClose: true,
            delayMilliseconds: 0).ConfigureAwait(false);
        await AssertThrowsAsync<SecurityException>(async () =>
        {
            _ = await run.ConfigureAwait(false);
        }).ConfigureAwait(false);
        AssertEqual(BootstrapBrokerSessionState.Failed, fixture.Session.State,
            "failed broker state");
        Assert(!fixture.Store.TryRead(out _),
            "a transcript failure must remove the publication");
        await fixture.RequireCleanExitAsync()
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerSemanticLoser()
    {
        await RunBrokerSemanticLoserAsync(
                preferClaim: true)
            .ConfigureAwait(false);
        await RunBrokerSemanticLoserAsync(
                preferClaim: false)
            .ConfigureAwait(false);
    }

    private static async Task RunBrokerSemanticLoserAsync(bool preferClaim)
    {
        using BrokerFixture fixture = BrokerFixture.Start(
            beforeArbitrationTestHook: async (
                claim,
                revoke,
                completed,
                cancellationToken) =>
            {
                Task expectedWinner = preferClaim ? claim : revoke;
                Assert(ReferenceEquals(completed, expectedWinner),
                    "the valid request must be selected before the semantic-loser barrier");
                await Task.WhenAll(
                        ObserveCompletionAsync(claim, cancellationToken),
                        ObserveCompletionAsync(revoke, cancellationToken))
                    .ConfigureAwait(false);
            });
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            fixture.Session.PublishPipeName,
            revokeAfterPublish: true,
            malformedRevoke: preferClaim,
            allowExpectedClose: true,
            delayMilliseconds: preferClaim ? 200 : 0,
            fixture.PublicationLifetime).ConfigureAwait(false);
        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
        await WaitForPublicationAsync(fixture.Store, run).ConfigureAwait(false);
        await fixture.SendBrokerClaimAsync(
            malformedTranscript: !preferClaim,
            badProof: false,
            allowExpectedClose: true,
            delayMilliseconds: preferClaim ? 0 : 200).ConfigureAwait(false);
        await AssertThrowsAsync<SecurityException>(async () =>
        {
            _ = await run.ConfigureAwait(false);
        }).ConfigureAwait(false);
        AssertEqual(BootstrapBrokerSessionState.Failed, fixture.Session.State,
            "semantic-loser broker state");
        Assert(!fixture.Store.TryRead(out _),
            "a completed semantic loser must remove the publication");
        await fixture.RequireCleanExitAsync().ConfigureAwait(false);
    }

    private static async Task ObserveCompletionAsync(
        Task task,
        CancellationToken cancellationToken)
    {
        try
        {
            await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The hook observes completion only; arbitration reads the fault.
        }
    }

    private static async Task TestBrokerBadProof()
    {
        using BrokerFixture fixture = BrokerFixture.Start();
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            fixture.Session.PublishPipeName,
            revokeAfterPublish: false,
            malformedRevoke: false,
            allowExpectedClose: false,
            delayMilliseconds: 0,
            fixture.PublicationLifetime).ConfigureAwait(false);
        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
        await WaitForPublicationAsync(fixture.Store, run).ConfigureAwait(false);
        await fixture.SendBrokerClaimAsync(
            malformedTranscript: false,
            badProof: true,
            allowExpectedClose: true,
            delayMilliseconds: 0).ConfigureAwait(false);
        await AssertThrowsAsync<SecurityException>(async () =>
        {
            _ = await run.ConfigureAwait(false);
        }).ConfigureAwait(false);
        Assert(!fixture.Store.TryRead(out _),
            "a proof failure must remove the publication");
        await fixture.RequireCleanExitAsync()
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerDeadline()
    {
        ManualTimeProvider clock = new(
            CanonicalTestUtcNow());
        BrokerFixture fixture = BrokerFixture.Start(
            publicationLifetime: TimeSpan.FromSeconds(4),
            sessionLifetime: TimeSpan.FromSeconds(5),
            timeProvider: clock);
        try
        {
            await fixture.Observer.SendBrokerPublishAsync(
                fixture.BrokerProcessId,
                fixture.Session.PublishPipeName,
                revokeAfterPublish: false,
                malformedRevoke: false,
                allowExpectedClose: false,
                delayMilliseconds: 0,
                fixture.PublicationLifetime).ConfigureAwait(false);
            Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
            await WaitForPublicationAsync(fixture.Store, run).ConfigureAwait(false);
            clock.Advance(fixture.PublicationLifetime);
            await fixture.SendBrokerClaimAsync(
                malformedTranscript: false,
                badProof: false,
                allowExpectedClose: true,
                delayMilliseconds: 0).ConfigureAwait(false);
            await AssertThrowsAnyAsync(async () =>
            {
                _ = await run.ConfigureAwait(false);
            },
                typeof(TimeoutException),
                typeof(OperationCanceledException),
                typeof(AggregateException))
                .ConfigureAwait(false);
            Assert(!fixture.Store.TryRead(out _),
                "deadline failure must remove the publication");
            await fixture.RequireCleanExitAsync()
                .ConfigureAwait(false);
        }
        finally
        {
            fixture.DisposeAfterObservedSessionFailure();
        }
    }

    private static async Task TestBrokerCombinedDeadlineCap()
    {
        TimeSpan sessionLifetime = TimeSpan.FromSeconds(5);
        TimeSpan publicationLifetime = TimeSpan.FromSeconds(4);
        TimeSpan elapsedBeforePublish = TimeSpan.FromSeconds(3);
        TimeSpan advanceInsidePublisher = TimeSpan.FromSeconds(3);
        ManualTimeProvider clock = new(
            CanonicalTestUtcNow(),
            elapsedBeforePublish);
        DeadlineProbePublisher? configuredPublisher = null;
        using BrokerFixture fixture = BrokerFixture.Start(
            publicationLifetime,
            sessionLifetime,
            clock,
            publisherFactory: _ =>
            {
                configuredPublisher = new DeadlineProbePublisher(
                    clock,
                    advanceInsidePublisher);
                return configuredPublisher;
            });
        DeadlineProbePublisher publisher = configuredPublisher ??
            throw new InvalidOperationException(
                "The combined-deadline probe publisher is missing.");
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            fixture.Session.PublishPipeName,
            revokeAfterPublish: false,
            malformedRevoke: false,
            allowExpectedClose: true,
            delayMilliseconds: 0,
            fixture.PublicationLifetime).ConfigureAwait(false);

        Exception failure = await CaptureExceptionAsync(
                fixture.Session.RunAsync())
            .ConfigureAwait(false);
        TimeSpan expectedRemaining = sessionLifetime - elapsedBeforePublish;
        AssertEqual(expectedRemaining, publisher.RecordedRemaining,
            "combined publisher deadline remaining time");
        Assert(publisher.RecordedRemaining < publicationLifetime,
            "the publication deadline must be capped by the remaining session");
        Assert(advanceInsidePublisher < publicationLifetime &&
            advanceInsidePublisher > publisher.RecordedRemaining,
            "the probe advance must expire only the combined deadline");
        Assert(failure is TestDeadlineProbeException probeFailure &&
            probeFailure.InnerException is TimeoutException,
            "combined deadline expiry must retain its nested TimeoutException");
        AssertEqual(BootstrapBrokerSessionState.Failed, fixture.Session.State,
            "combined-deadline broker state");
        Assert(!fixture.Store.TryRead(out _),
            "combined deadline failure must not retain a publication");
        await fixture.RequireCleanExitAsync(controllerUnused: true)
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerDeadlineSetupFailure()
    {
        ThrowingFirstTimestampTimeProvider clock = new();
        using BrokerFixture fixture = BrokerFixture.Start(timeProvider: clock);
        string publishName = fixture.Session.PublishPipeName;

        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
        Exception failure = await CaptureExceptionAsync(run)
            .ConfigureAwait(false);
        Assert(ContainsException<TestTimeProviderException>(failure),
            "a start-bound deadline capture failure must fault RunAsync");
        Assert(run.IsFaulted,
            "a start-bound deadline capture failure must publish a faulted task");
        AssertEqual(BootstrapBrokerSessionState.Failed, fixture.Session.State,
            "deadline-setup-failure broker state");
        Assert(!fixture.Store.TryRead(out _),
            "deadline setup failure must not retain a publication");
        using ProtectedNamedPipe replacement = ProtectedNamedPipe.Create(
            publishName,
            fixture.ObserverBinding);

        Task firstDisposal = fixture.Session.DisposeAsync().AsTask();
        Task secondDisposal = fixture.Session.DisposeAsync().AsTask();
        Assert(ReferenceEquals(firstDisposal, secondDisposal),
            "deadline setup failure disposal must remain coalesced");
        await Task.WhenAll(firstDisposal, secondDisposal)
            .ConfigureAwait(false);
        Assert(firstDisposal.IsCompletedSuccessfully,
            "deadline setup failure disposal must complete cleanly");
        await fixture.RequireCleanExitAsync(
                observerUnused: true,
                controllerUnused: true)
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerCancellation()
    {
        using BrokerFixture fixture = BrokerFixture.Start();
        using CancellationTokenSource cancellation = new();
        string publishName = fixture.Session.PublishPipeName;
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            publishName,
            revokeAfterPublish: false,
            malformedRevoke: false,
            allowExpectedClose: false,
            delayMilliseconds: 0,
            fixture.PublicationLifetime).ConfigureAwait(false);
        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync(
            cancellation.Token);
        await WaitForPublicationAsync(fixture.Store, run).ConfigureAwait(false);
        byte[] brokerTokenBorrow = GetBrokerTokenBacking(fixture.Session);
        cancellation.Cancel();
        await AssertThrowsAsync<OperationCanceledException>(async () =>
        {
            _ = await run.ConfigureAwait(false);
        }).ConfigureAwait(false);
        Assert(run.IsCanceled,
            "ordinary cancellation must publish a cancelled RunAsync task after cleanup");
        Assert(!fixture.Store.TryRead(out _),
            "cancellation must leave the store empty");
        Assert(AllZero(brokerTokenBorrow),
            "cancellation must wipe the broker token backing array");
        using ProtectedNamedPipe replacement = ProtectedNamedPipe.Create(
            publishName,
            fixture.ObserverBinding);
        await fixture.RequireCleanExitAsync(
                controllerUnused: true)
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerCancellationAwaitsRemoval()
    {
        ControlledPublicationPublisher? configuredPublisher = null;
        TaskCompletionSource<bool> arbitrationEntered = NewSignal();
        BrokerFixture fixture = BrokerFixture.Start(
            publisherFactory: store =>
            {
                configuredPublisher = new ControlledPublicationPublisher(
                    store,
                    blockRemoval: true);
                return configuredPublisher;
            },
            beforeArbitrationTestHook: async (
                _,
                _,
                _,
                cancellationToken) =>
            {
                arbitrationEntered.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                    .ConfigureAwait(false);
            });
        ControlledPublicationPublisher publisher = configuredPublisher ??
            throw new InvalidOperationException(
                "The controlled publication publisher is missing.");
        using CancellationTokenSource cancellation = new();
        string publishName = fixture.Session.PublishPipeName;
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            publishName,
            revokeAfterPublish: true,
            malformedRevoke: false,
            allowExpectedClose: true,
            delayMilliseconds: 0,
            fixture.PublicationLifetime).ConfigureAwait(false);
        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync(
            cancellation.Token);
        await publisher.PublicationCommitted.WaitAsync(TestTimeout)
            .ConfigureAwait(false);
        string claimName = GetPublishedDescriptor(fixture.Store).ClaimPipeName;
        string revokeName = GetBrokerPipeName(fixture.Session, "revokePipe");
        byte[] brokerTokenBorrow = GetBrokerTokenBacking(fixture.Session);
        await arbitrationEntered.Task.WaitAsync(TestTimeout)
            .ConfigureAwait(false);

        cancellation.Cancel();
        await publisher.RemovalStarted.WaitAsync(TestTimeout)
            .ConfigureAwait(false);
        Task firstDisposal = fixture.Session.DisposeAsync().AsTask();
        Task secondDisposal = fixture.Session.DisposeAsync().AsTask();
        Assert(ReferenceEquals(firstDisposal, secondDisposal),
            "concurrent disposal must await one published task");
        Assert(!run.IsCompleted,
            "run must await exact publication removal");
        AssertEqual(1, publisher.RemovalCalls,
            "coalesced removal call count while blocked");
        await WaitUntilAsync(
                () => AllZero(brokerTokenBorrow),
                "token cleanup did not run before exact-removal completion")
            .ConfigureAwait(false);
        Assert(AllZero(brokerTokenBorrow),
            "token cleanup must not wait for exact publication removal");

        Task<ProtectedNamedPipe> claimRelease = RecreatePipeWhenReleasedAsync(
            claimName,
            fixture.ControllerBinding);
        Task<ProtectedNamedPipe> revokeRelease = RecreatePipeWhenReleasedAsync(
            revokeName,
            fixture.ObserverBinding);
        ProtectedNamedPipe[] releasedPipes = await Task.WhenAll(
                claimRelease,
                revokeRelease)
            .WaitAsync(TestTimeout)
            .ConfigureAwait(false);
        using ProtectedNamedPipe claimReplacement = releasedPipes[0];
        using ProtectedNamedPipe revokeReplacement = releasedPipes[1];
        Assert(!run.IsCompleted,
            "claim and revoke pipes must be released before removal completes");
        Assert(fixture.Store.TryRead(out BootstrapPublicationSnapshot? blocked) &&
            blocked is not null,
            "pipe release must not claim blocked publication removal");
        (blocked ?? throw new InvalidOperationException(
            "The blocked-removal snapshot is missing.")).Dispose();

        publisher.ReleaseRemoval();
        Task runCompletion = await Task.WhenAny(
                run,
                Task.Delay(TestTimeout))
            .ConfigureAwait(false);
        Assert(ReferenceEquals(runCompletion, run),
            $"run remained blocked after removal release; removal calls: " +
            $"{publisher.RemovalCalls}, store occupied: " +
            $"{fixture.Store.TryRead(out _)}");
        await AssertThrowsAsync<OperationCanceledException>(async () =>
        {
            _ = await run.ConfigureAwait(false);
        }).ConfigureAwait(false);
        await Task.WhenAll(
                firstDisposal.WaitAsync(TestTimeout),
                secondDisposal.WaitAsync(TestTimeout))
            .ConfigureAwait(false);
        AssertEqual(1, publisher.RemovalCalls,
            "terminal coalesced removal call count");
        Assert(!fixture.Store.TryRead(out _),
            "cancellation must await verified exact absence");
        using ProtectedNamedPipe publishReplacement = ProtectedNamedPipe.Create(
            publishName,
            fixture.ObserverBinding);
        await fixture.RequireCleanExitAsync(controllerUnused: true)
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerDisposeDuringPublish()
    {
        ControlledPublicationPublisher? configuredPublisher = null;
        using BrokerFixture fixture = BrokerFixture.Start(
            publisherFactory: store =>
            {
                configuredPublisher = new ControlledPublicationPublisher(
                    store,
                    blockBeforeCommit: true);
                return configuredPublisher;
            });
        ControlledPublicationPublisher publisher = configuredPublisher ??
            throw new InvalidOperationException(
                "The blocked publication publisher is missing.");
        string publishName = fixture.Session.PublishPipeName;
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            publishName,
            revokeAfterPublish: false,
            malformedRevoke: false,
            allowExpectedClose: true,
            delayMilliseconds: 0,
            fixture.PublicationLifetime).ConfigureAwait(false);
        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
        await publisher.PublishEntered.WaitAsync(TestTimeout)
            .ConfigureAwait(false);

        Task firstDisposal = fixture.Session.DisposeAsync().AsTask();
        Task secondDisposal = fixture.Session.DisposeAsync().AsTask();
        Assert(ReferenceEquals(firstDisposal, secondDisposal),
            "blocked-publish disposal must be coalesced");
        await publisher.PublishCancelled.WaitAsync(TestTimeout)
            .ConfigureAwait(false);
        await AssertThrowsAsync<OperationCanceledException>(async () =>
        {
            _ = await run.ConfigureAwait(false);
        }).ConfigureAwait(false);
        await Task.WhenAll(firstDisposal, secondDisposal)
            .ConfigureAwait(false);
        Assert(!publisher.PublicationCommitted.IsCompleted,
            "a cancelled pre-commit publisher must not report a commit");
        AssertEqual(0, publisher.RemovalCalls,
            "a cancelled pre-commit publisher must not manufacture a lease");
        Assert(!fixture.Store.TryRead(out _),
            "a cancelled pre-commit publisher must leave no mutation");
        using ProtectedNamedPipe replacement = ProtectedNamedPipe.Create(
            publishName,
            fixture.ObserverBinding);
        await fixture.RequireCleanExitAsync(controllerUnused: true)
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerDisposalCancellationCallbackFailure()
    {
        ControlledPublicationPublisher? configuredPublisher = null;
        BrokerFixture fixture = BrokerFixture.Start(
            publisherFactory: store =>
            {
                configuredPublisher = new ControlledPublicationPublisher(store);
                return configuredPublisher;
            });
        ControlledPublicationPublisher publisher = configuredPublisher ??
            throw new InvalidOperationException(
                "The cancellation-callback publisher is missing.");
        CancellationTokenRegistration callbackRegistration = default;
        try
        {
            string publishName = fixture.Session.PublishPipeName;
            await fixture.Observer.SendBrokerPublishAsync(
                fixture.BrokerProcessId,
                publishName,
                revokeAfterPublish: false,
                malformedRevoke: false,
                allowExpectedClose: true,
                delayMilliseconds: 0,
                fixture.PublicationLifetime).ConfigureAwait(false);
            Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
            await publisher.PublicationCommitted.WaitAsync(TestTimeout)
                .ConfigureAwait(false);
            await WaitUntilAsync(
                    () => GetPrivateField(
                            fixture.Session,
                            "publicationLease") is BootstrapPublicationLease &&
                        GetPrivateField(fixture.Session, "publishPipe") is null,
                    "the broker did not enter its owned-publication wait state")
                .ConfigureAwait(false);

            string claimName = GetPublishedDescriptor(fixture.Store)
                .ClaimPipeName;
            string revokeName = GetBrokerPipeName(
                fixture.Session,
                "revokePipe");
            byte[] brokerTokenBorrow = GetBrokerTokenBacking(fixture.Session);
            CancellationTokenSource lifetime = GetPrivateField(
                    fixture.Session,
                    "lifetimeCancellation") as CancellationTokenSource ??
                throw new InvalidOperationException(
                    "The broker lifetime cancellation source is unavailable.");
            callbackRegistration = lifetime.Token.Register(static () =>
                throw new TestCancellationCallbackException());

            Task firstDisposal = fixture.Session.DisposeAsync().AsTask();
            Task secondDisposal = fixture.Session.DisposeAsync().AsTask();
            Assert(ReferenceEquals(firstDisposal, secondDisposal),
                "throwing-callback disposal must publish one coalesced task");
            Exception firstFailure = await CaptureExceptionAsync(
                    firstDisposal.WaitAsync(TestTimeout))
                .ConfigureAwait(false);
            Exception secondFailure = await CaptureExceptionAsync(
                    secondDisposal)
                .ConfigureAwait(false);
            Assert(ContainsException<TestCancellationCallbackException>(
                    firstFailure) &&
                ContainsException<TestCancellationCallbackException>(
                    secondFailure),
                "coalesced disposal must report the cancellation callback failure");
            Assert(firstDisposal.IsFaulted && secondDisposal.IsFaulted,
                "throwing-callback disposal must remain faulted");

            await AssertThrowsAsync<OperationCanceledException>(async () =>
            {
                _ = await run.ConfigureAwait(false);
            }).ConfigureAwait(false);
            Assert(run.IsCanceled,
                "the broker run must still reach its cancellation outcome");
            AssertEqual(1, publisher.RemovalCalls,
                "throwing-callback exact-removal call count");
            Assert(!fixture.Store.TryRead(out _),
                "throwing-callback cleanup must leave the store empty");
            Assert(AllZero(brokerTokenBorrow),
                "throwing-callback cleanup must wipe the broker token");
            using ProtectedNamedPipe publishReplacement =
                ProtectedNamedPipe.Create(
                    publishName,
                    fixture.ObserverBinding);
            using ProtectedNamedPipe claimReplacement =
                ProtectedNamedPipe.Create(
                    claimName,
                    fixture.ControllerBinding);
            using ProtectedNamedPipe revokeReplacement =
                ProtectedNamedPipe.Create(
                    revokeName,
                    fixture.ObserverBinding);
            await fixture.RequireCleanExitAsync(controllerUnused: true)
                .ConfigureAwait(false);
        }
        finally
        {
            callbackRegistration.Dispose();
            fixture.DisposeAfterObservedSessionFailure();
        }
    }

    private static async Task TestBrokerPublisherReentrantDisposal()
    {
        BrokerFixture? fixture = null;
        ControlledPublicationPublisher? configuredPublisher = null;
        Task? reentrantDisposal = null;
        TaskCompletionSource<bool> disposalPublished = NewSignal();
        fixture = BrokerFixture.Start(
            publisherFactory: store =>
            {
                configuredPublisher = new ControlledPublicationPublisher(
                    store,
                    afterCommitBeforeReturn: () =>
                    {
                        BrokerFixture active = fixture ??
                            throw new InvalidOperationException(
                                "The reentrant broker fixture is missing.");
                        reentrantDisposal = active.Session.DisposeAsync()
                            .AsTask();
                        disposalPublished.TrySetResult(true);
                    });
                return configuredPublisher;
            });
        using BrokerFixture activeFixture = fixture;
        ControlledPublicationPublisher publisher = configuredPublisher ??
            throw new InvalidOperationException(
                "The reentrant publication publisher is missing.");
        await activeFixture.Observer.SendBrokerPublishAsync(
            activeFixture.BrokerProcessId,
            activeFixture.Session.PublishPipeName,
            revokeAfterPublish: false,
            malformedRevoke: false,
            allowExpectedClose: true,
            delayMilliseconds: 0,
            activeFixture.PublicationLifetime).ConfigureAwait(false);

        Task<BootstrapBrokerSessionResult> run =
            activeFixture.Session.RunAsync();
        await disposalPublished.Task.WaitAsync(TestTimeout)
            .ConfigureAwait(false);
        Task disposal = reentrantDisposal ??
            throw new InvalidOperationException(
                "The reentrant disposal task was not published.");
        Assert(ReferenceEquals(
                run,
                GetPrivateField(activeFixture.Session, "runTask")),
            "RunAsync must publish its task before publisher re-entry");
        Assert(ReferenceEquals(
                disposal,
                activeFixture.Session.DisposeAsync().AsTask()),
            "publisher re-entry must publish one coalesced disposal task");

        Task disposalCompletion = await Task.WhenAny(
                disposal,
                Task.Delay(TestTimeout))
            .ConfigureAwait(false);
        Assert(ReferenceEquals(disposalCompletion, disposal),
            "synchronous publisher disposal re-entry must not deadlock");
        await disposal.ConfigureAwait(false);
        Exception runFailure = await CaptureExceptionAsync(run)
            .ConfigureAwait(false);
        Assert(ContainsException<ObjectDisposedException>(runFailure),
            "publisher re-entry must reject the committed publication after disposal");
        Assert(run.IsFaulted && disposal.IsCompletedSuccessfully,
            "reentrant run and disposal tasks must publish terminal states");
        AssertEqual(1, publisher.RemovalCalls,
            "reentrant publisher exact-removal call count");
        Assert(!activeFixture.Store.TryRead(out _),
            "reentrant disposal must leave the publication store empty");
        await activeFixture.RequireCleanExitAsync(controllerUnused: true)
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerCommitBeforeReturnRollback()
    {
        ControlledPublicationPublisher? configuredPublisher = null;
        using BrokerFixture fixture = BrokerFixture.Start(
            publisherFactory: store =>
            {
                configuredPublisher = new ControlledPublicationPublisher(
                    store,
                    blockAfterCommit: true);
                return configuredPublisher;
            });
        ControlledPublicationPublisher publisher = configuredPublisher ??
            throw new InvalidOperationException(
                "The commit-before-return publisher is missing.");
        string publishName = fixture.Session.PublishPipeName;
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            publishName,
            revokeAfterPublish: false,
            malformedRevoke: false,
            allowExpectedClose: true,
            delayMilliseconds: 0,
            fixture.PublicationLifetime).ConfigureAwait(false);
        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
        await publisher.PublicationCommitted.WaitAsync(TestTimeout)
            .ConfigureAwait(false);

        Task firstDisposal = fixture.Session.DisposeAsync().AsTask();
        Task secondDisposal = fixture.Session.DisposeAsync().AsTask();
        Assert(ReferenceEquals(firstDisposal, secondDisposal),
            "commit-before-return disposal must be coalesced");
        Assert(!firstDisposal.IsCompleted,
            "disposal must wait for a publisher that has committed but not returned");
        publisher.ReleasePublish();
        await publisher.RemovalStarted.WaitAsync(TestTimeout)
            .ConfigureAwait(false);
        await AssertThrowsAsync<ObjectDisposedException>(async () =>
        {
            _ = await run.ConfigureAwait(false);
        }).ConfigureAwait(false);
        await Task.WhenAll(firstDisposal, secondDisposal)
            .ConfigureAwait(false);
        AssertEqual(1, publisher.RemovalCalls,
            "a commit returned after disposal must be rolled back exactly once");
        Assert(!fixture.Store.TryRead(out _),
            "commit-before-return rollback must verify exact absence");
        using ProtectedNamedPipe replacement = ProtectedNamedPipe.Create(
            publishName,
            fixture.ObserverBinding);
        await fixture.RequireCleanExitAsync(controllerUnused: true)
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerPublisherFault()
    {
        ControlledPublicationPublisher? configuredPublisher = null;
        using BrokerFixture fixture = BrokerFixture.Start(
            publisherFactory: store =>
            {
                configuredPublisher = new ControlledPublicationPublisher(
                    store,
                    publishFailure: new TestPublisherException());
                return configuredPublisher;
            });
        ControlledPublicationPublisher publisher = configuredPublisher ??
            throw new InvalidOperationException(
                "The faulting publication publisher is missing.");
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            fixture.Session.PublishPipeName,
            revokeAfterPublish: false,
            malformedRevoke: false,
            allowExpectedClose: true,
            delayMilliseconds: 0,
            fixture.PublicationLifetime).ConfigureAwait(false);
        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
        await publisher.PublishEntered.WaitAsync(TestTimeout)
            .ConfigureAwait(false);
        await AssertThrowsAsync<TestPublisherException>(async () =>
        {
            _ = await run.ConfigureAwait(false);
        }).ConfigureAwait(false);
        AssertEqual(BootstrapBrokerSessionState.Failed, fixture.Session.State,
            "publisher-fault broker state");
        Assert(!fixture.Store.TryRead(out _),
            "a faulting publisher must leave no retained publication");
        AssertEqual(0, publisher.RemovalCalls,
            "a pre-commit publisher fault must not create removal authority");
        await fixture.RequireCleanExitAsync(controllerUnused: true)
            .ConfigureAwait(false);
    }

    private static async Task TestBrokerRemovalFault()
    {
        ControlledPublicationPublisher? configuredPublisher = null;
        using BrokerFixture fixture = BrokerFixture.Start(
            publisherFactory: store =>
            {
                configuredPublisher = new ControlledPublicationPublisher(
                    store,
                    removalFailure: new TestRemovalException());
                return configuredPublisher;
            });
        ControlledPublicationPublisher publisher = configuredPublisher ??
            throw new InvalidOperationException(
                "The removal-fault publisher is missing.");
        try
        {
            await fixture.Observer.SendBrokerPublishAsync(
                fixture.BrokerProcessId,
                fixture.Session.PublishPipeName,
                revokeAfterPublish: true,
                malformedRevoke: false,
                allowExpectedClose: true,
                delayMilliseconds: 0,
                fixture.PublicationLifetime).ConfigureAwait(false);
            Exception failure = await CaptureExceptionAsync(
                    fixture.Session.RunAsync())
                .ConfigureAwait(false);
            Assert(ContainsException<TestRemovalException>(failure),
                "a terminal removal fault must be observable to the caller");
            AssertEqual(BootstrapBrokerSessionState.Failed, fixture.Session.State,
                "removal-fault broker state");
            AssertEqual(1, publisher.RemovalCalls,
                "a faulting lease removal must remain coalesced");
            Assert(fixture.Store.TryRead(
                    out BootstrapPublicationSnapshot? retained) &&
                retained is not null,
                "indeterminate removal must not claim publication absence");
            (retained ?? throw new InvalidOperationException(
                "The indeterminate-removal snapshot is missing.")).Dispose();
            await publisher.ForceRemoveAsync().ConfigureAwait(false);
            Assert(!fixture.Store.TryRead(out _),
                "test cleanup must remove the retained exact publication");
            await fixture.RequireCleanExitAsync(controllerUnused: true)
                .ConfigureAwait(false);
        }
        finally
        {
            fixture.DisposeAfterObservedSessionFailure();
        }
    }

    private static async Task TestBrokerUnknownRemovalStatus()
    {
        await RunBrokerUnknownRemovalStatusAsync(claim: true)
            .ConfigureAwait(false);
        await RunBrokerUnknownRemovalStatusAsync(claim: false)
            .ConfigureAwait(false);
    }

    private static async Task RunBrokerUnknownRemovalStatusAsync(bool claim)
    {
        ControlledPublicationPublisher? configuredPublisher = null;
        BrokerFixture fixture = BrokerFixture.Start(
            publisherFactory: store =>
            {
                configuredPublisher = new ControlledPublicationPublisher(
                    store,
                    returnDefaultRemovalStatus: true);
                return configuredPublisher;
            });
        ControlledPublicationPublisher publisher = configuredPublisher ??
            throw new InvalidOperationException(
                "The unknown-removal publisher is missing.");
        try
        {
            await fixture.Observer.SendBrokerPublishAsync(
                fixture.BrokerProcessId,
                fixture.Session.PublishPipeName,
                revokeAfterPublish: !claim,
                malformedRevoke: false,
                allowExpectedClose: !claim,
                delayMilliseconds: 0,
                fixture.PublicationLifetime,
                expectTerminalRejection: !claim).ConfigureAwait(false);
            Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
            await publisher.PublicationCommitted.WaitAsync(TestTimeout)
                .ConfigureAwait(false);
            if (claim)
            {
                await fixture.SendBrokerClaimAsync(
                    malformedTranscript: false,
                    badProof: false,
                    allowExpectedClose: true,
                    delayMilliseconds: 0,
                    expectTerminalRejection: true).ConfigureAwait(false);
            }

            Exception failure = await CaptureExceptionAsync(run)
                .ConfigureAwait(false);
            Assert(ContainsException<InvalidOperationException>(failure),
                "an unknown removal result must fail the broker closed");
            AssertEqual(BootstrapBrokerSessionState.Failed,
                fixture.Session.State,
                "unknown-removal broker state");
            AssertEqual(1, publisher.RemovalCalls,
                "unknown exact-removal call count");
            Assert(fixture.Store.TryRead(
                    out BootstrapPublicationSnapshot? retained) &&
                retained is not null,
                "an unknown removal result must not claim publication absence");
            (retained ?? throw new InvalidOperationException(
                "The unknown-removal snapshot is missing.")).Dispose();
            await publisher.ForceRemoveAsync().ConfigureAwait(false);
            Assert(!fixture.Store.TryRead(out _),
                "unknown-removal test cleanup must remove the exact publication");
            await fixture.RequireCleanExitAsync(controllerUnused: !claim)
                .ConfigureAwait(false);
        }
        finally
        {
            fixture.DisposeAfterObservedSessionFailure();
        }
    }

    private static async Task TestBrokerDisposeCommitRemovalFault()
    {
        ControlledPublicationPublisher? configuredPublisher = null;
        BrokerFixture fixture = BrokerFixture.Start(
            publisherFactory: store =>
            {
                configuredPublisher = new ControlledPublicationPublisher(
                    store,
                    blockAfterCommit: true,
                    removalFailure: new TestRemovalException());
                return configuredPublisher;
            });
        ControlledPublicationPublisher publisher = configuredPublisher ??
            throw new InvalidOperationException(
                "The disposal removal-fault publisher is missing.");
        try
        {
            await fixture.Observer.SendBrokerPublishAsync(
                fixture.BrokerProcessId,
                fixture.Session.PublishPipeName,
                revokeAfterPublish: false,
                malformedRevoke: false,
                allowExpectedClose: true,
                delayMilliseconds: 0,
                fixture.PublicationLifetime).ConfigureAwait(false);
            Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
            await publisher.PublicationCommitted.WaitAsync(TestTimeout)
                .ConfigureAwait(false);

            Task firstDisposal = fixture.Session.DisposeAsync().AsTask();
            Task secondDisposal = fixture.Session.DisposeAsync().AsTask();
            Assert(ReferenceEquals(firstDisposal, secondDisposal),
                "post-commit faulting disposal must remain coalesced");
            Assert(!firstDisposal.IsCompleted,
                "disposal must await the committed publisher result");
            publisher.ReleasePublish();
            await publisher.RemovalStarted.WaitAsync(TestTimeout)
                .ConfigureAwait(false);

            Exception disposalFailure = await CaptureExceptionAsync(
                    firstDisposal)
                .ConfigureAwait(false);
            Exception repeatedDisposalFailure = await CaptureExceptionAsync(
                    secondDisposal)
                .ConfigureAwait(false);
            Exception runFailure = await CaptureExceptionAsync(run)
                .ConfigureAwait(false);
            Assert(ContainsException<TestRemovalException>(disposalFailure) &&
                ContainsException<TestRemovalException>(
                    repeatedDisposalFailure),
                "every coalesced disposal caller must observe removal failure");
            Assert(ContainsException<TestRemovalException>(runFailure),
                "the run caller must observe post-commit removal failure");
            AssertEqual(1, publisher.RemovalCalls,
                "post-commit disposal removal call count");
            Assert(fixture.Store.TryRead(
                    out BootstrapPublicationSnapshot? retained) &&
                retained is not null,
                "faulting disposal must not claim publication absence");
            (retained ?? throw new InvalidOperationException(
                "The disposal-fault snapshot is missing.")).Dispose();
            await publisher.ForceRemoveAsync().ConfigureAwait(false);
            Assert(!fixture.Store.TryRead(out _),
                "disposal-fault test cleanup must remove the exact publication");
            await fixture.RequireCleanExitAsync(controllerUnused: true)
                .ConfigureAwait(false);
        }
        finally
        {
            publisher.ReleasePublish();
            fixture.DisposeAfterObservedSessionFailure();
        }
    }

    private static async Task TestBrokerPrimaryAndRemovalFailure()
    {
        ControlledPublicationPublisher? configuredPublisher = null;
        BrokerFixture fixture = BrokerFixture.Start(
            publisherFactory: store =>
            {
                configuredPublisher = new ControlledPublicationPublisher(
                    store,
                    removalFailure: new TestRemovalException());
                return configuredPublisher;
            });
        ControlledPublicationPublisher publisher = configuredPublisher ??
            throw new InvalidOperationException(
                "The dual-fault publication publisher is missing.");
        try
        {
            await fixture.Observer.SendBrokerPublishAsync(
                fixture.BrokerProcessId,
                fixture.Session.PublishPipeName,
                revokeAfterPublish: false,
                malformedRevoke: false,
                allowExpectedClose: false,
                delayMilliseconds: 0,
                fixture.PublicationLifetime).ConfigureAwait(false);
            Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
            await publisher.PublicationCommitted.WaitAsync(TestTimeout)
                .ConfigureAwait(false);
            await fixture.SendBrokerClaimAsync(
                malformedTranscript: true,
                badProof: false,
                allowExpectedClose: true,
                delayMilliseconds: 0).ConfigureAwait(false);
            Exception failure = await CaptureExceptionAsync(run)
                .ConfigureAwait(false);
            Assert(ContainsException<SecurityException>(failure),
                "the primary protocol failure must remain observable");
            Assert(ContainsException<TestRemovalException>(failure),
                "the terminal removal failure must remain observable");
            AssertEqual(1, publisher.RemovalCalls,
                "dual-fault cleanup must attempt exact removal once");
            AssertEqual(BootstrapBrokerSessionState.Failed, fixture.Session.State,
                "dual-fault broker state");
            await publisher.ForceRemoveAsync().ConfigureAwait(false);
            Assert(!fixture.Store.TryRead(out _),
                "dual-fault test cleanup must remove the exact publication");
            await fixture.RequireCleanExitAsync().ConfigureAwait(false);
        }
        finally
        {
            fixture.DisposeAfterObservedSessionFailure();
        }
    }

    private static async Task TestBrokerOccupiedStore()
    {
        using BrokerFixture fixture = BrokerFixture.Start();
        using DescriptorFixture occupiedFixture = CreateDescriptorFixture();
        byte[] occupied = occupiedFixture.CreateDescriptor().EncodeCanonical();
        try
        {
            Assert(fixture.Store.TryPublish(
                    occupied,
                    out BootstrapPublicationRegistration? owner) &&
                owner is not null, "the fixture store must accept its sentinel");
            BootstrapPublicationRegistration sentinelOwner = owner ??
                throw new InvalidOperationException("The sentinel owner is missing.");
            await fixture.Observer.SendBrokerPublishAsync(
                fixture.BrokerProcessId,
                fixture.Session.PublishPipeName,
                revokeAfterPublish: false,
                malformedRevoke: false,
                allowExpectedClose: true,
                delayMilliseconds: 0,
                fixture.PublicationLifetime).ConfigureAwait(false);
            await AssertThrowsAsync<InvalidOperationException>(async () =>
            {
                _ = await fixture.Session.RunAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
            Assert(fixture.Store.TryRead(out BootstrapPublicationSnapshot? snapshot) &&
                snapshot is not null, "the occupied sentinel must survive rejection");
            (snapshot ?? throw new InvalidOperationException(
                "The sentinel snapshot is missing.")).Dispose();
            Assert(fixture.Store.TryRemove(sentinelOwner),
                "the sentinel owner must retain removal authority");
            await fixture.RequireCleanExitAsync(controllerUnused: true)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(occupied);
        }
    }

    private static async Task TestBrokerNameRelease()
    {
        using BrokerFixture fixture = BrokerFixture.Start();
        string publishName = fixture.Session.PublishPipeName;
        await fixture.Observer.SendBrokerPublishAsync(
            fixture.BrokerProcessId,
            publishName,
            revokeAfterPublish: true,
            malformedRevoke: false,
            allowExpectedClose: false,
            delayMilliseconds: 100,
            fixture.PublicationLifetime).ConfigureAwait(false);
        Task<BootstrapBrokerSessionResult> run = fixture.Session.RunAsync();
        await WaitForPublicationAsync(fixture.Store, run).ConfigureAwait(false);
        string claimName = GetPublishedDescriptor(fixture.Store).ClaimPipeName;
        string revokeName = GetBrokerPipeName(fixture.Session, "revokePipe");
        _ = await run.ConfigureAwait(false);
        await fixture.RequireCleanExitAsync(controllerUnused: true)
            .ConfigureAwait(false);
        using ProtectedNamedPipe publishReplacement = ProtectedNamedPipe.Create(
            publishName,
            fixture.ObserverBinding);
        using ProtectedNamedPipe claimReplacement = ProtectedNamedPipe.Create(
            claimName,
            fixture.ControllerBinding);
        using ProtectedNamedPipe revokeReplacement = ProtectedNamedPipe.Create(
            revokeName,
            fixture.ObserverBinding);
    }

    private static async Task WaitForPublicationAsync(
        InMemoryBootstrapPublicationStore store,
        Task<BootstrapBrokerSessionResult> session)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TestTimeout)
        {
            if (session.IsCompleted)
            {
                _ = await session.ConfigureAwait(false);
                throw new InvalidOperationException(
                    "The broker completed before its publication was observed.");
            }

            if (store.TryRead(out BootstrapPublicationSnapshot? snapshot) &&
                snapshot is not null)
            {
                snapshot.Dispose();
                return;
            }

            await Task.Delay(10).ConfigureAwait(false);
        }

        throw new TimeoutException("The broker did not publish in time.");
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        string failureMessage)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TestTimeout)
        {
            if (predicate())
            {
                return;
            }

            await Task.Yield();
        }

        throw new TimeoutException(failureMessage);
    }

    private static async Task<ProtectedNamedPipe> RecreatePipeWhenReleasedAsync(
        string name,
        BootstrapBinding expectedPeer)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        Win32Exception? collision = null;
        while (elapsed.Elapsed < TestTimeout)
        {
            try
            {
                return ProtectedNamedPipe.Create(name, expectedPeer);
            }
            catch (Win32Exception exception)
            {
                collision = exception;
                await Task.Yield();
            }
        }

        throw new TimeoutException(
            $"The protected pipe name was not released: {name}",
            collision);
    }

    private static TestChild StartChild()
    {
        return StartChild(ChildMode);
    }

    private static TestChild StartChild(string mode)
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

        start.ArgumentList.Add(mode);
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

    private static async Task<int> RunBrokerObserverChild()
    {
        const byte RevokeFlag = 1;
        const byte AllowCloseFlag = 2;
        const byte MalformedRevokeFlag = 4;
        const byte RequireTerminalRejectionFlag = 8;
        try
        {
            Stream input = System.Console.OpenStandardInput();
            byte[] header = new byte[sizeof(uint) + sizeof(byte) +
                sizeof(int) + sizeof(int) + sizeof(byte)];
            try
            {
                using CancellationTokenSource cancellation = new(TestTimeout);
                await input.ReadExactlyAsync(header, cancellation.Token)
                    .ConfigureAwait(false);
                uint brokerProcessId = BinaryPrimitives.ReadUInt32LittleEndian(
                    header.AsSpan(0, sizeof(uint)));
                byte flags = header[sizeof(uint)];
                int delayMilliseconds = BinaryPrimitives.ReadInt32LittleEndian(
                    header.AsSpan(sizeof(uint) + sizeof(byte), sizeof(int)));
                int lifetimeMilliseconds = BinaryPrimitives.ReadInt32LittleEndian(
                    header.AsSpan(
                        sizeof(uint) + sizeof(byte) + sizeof(int),
                        sizeof(int)));
                int nameLength = header[^1];
                if (brokerProcessId == 0 && flags == 0 &&
                    delayMilliseconds == 0 && lifetimeMilliseconds == 0 &&
                    nameLength == 0)
                {
                    return 0;
                }

                if (brokerProcessId == 0 || delayMilliseconds < 0 ||
                    lifetimeMilliseconds <= 0 || nameLength is < 1 or > 120 ||
                    (flags & ~(RevokeFlag | AllowCloseFlag |
                        MalformedRevokeFlag |
                        RequireTerminalRejectionFlag)) != 0)
                {
                    return 20;
                }

                byte[] nameBytes = new byte[nameLength];
                try
                {
                    await input.ReadExactlyAsync(nameBytes, cancellation.Token)
                        .ConfigureAwait(false);
                    string publishPipeName = Encoding.ASCII.GetString(nameBytes);
                    ProtectedNamedPipe.ValidateName(publishPipeName);
                    using ProcessIdentityLease broker =
                        ProcessIdentityLease.Capture(brokerProcessId);
                    using ProcessIdentityLease observer =
                        ProcessIdentityLease.Capture(
                            checked((uint)Environment.ProcessId));
                    using SecretBuffer token = SecretBuffer.CreateRandom32();
                    byte[] publicationNonce = RandomTestValue32();
                    Guid publishRequestId = Guid.NewGuid();
                    try
                    {
                        using ProtectedNamedPipeClient publish =
                            ProtectedNamedPipeClient.Connect(
                                publishPipeName,
                                broker.Snapshot(),
                                TestTimeout);
                        using (PublishRequest request = new(
                                   publishRequestId,
                                   publicationNonce,
                                   new ObserverTransportEndpoint(
                                       32_001,
                                       Guid.NewGuid()),
                                   token.Bytes))
                        using (SensitiveFrame requestFrame =
                               BootstrapProtocol.Encode(request))
                        {
                            await publish.SendFrameAsync(
                                    requestFrame.Bytes,
                                    TestTimeout)
                                .ConfigureAwait(false);
                        }
                        byte[] acknowledgementFrame = await publish
                            .ReceiveFrameAsync(TestTimeout)
                            .ConfigureAwait(false);
                        PublishAck acknowledgement = (PublishAck)
                            BootstrapProtocol.DecodeOwned(
                                acknowledgementFrame,
                                BootstrapMessageType.PublishAck,
                                BootstrapRole.Broker,
                                BootstrapRole.Observer);
                        if (acknowledgement.RequestId != publishRequestId)
                        {
                            return 21;
                        }

                        BootstrapDescriptor descriptor =
                            BootstrapDescriptor.Parse(acknowledgement.Descriptor);
                        if (!descriptor.Verify(
                                token.Bytes,
                                observer.Snapshot(),
                                broker.Snapshot(),
                                CanonicalTestUtcNow(),
                                TimeSpan.FromMilliseconds(lifetimeMilliseconds)))
                        {
                            return 22;
                        }

                        token.Dispose();

                        byte[] descriptorDigest = descriptor.ComputeDigest();
                        try
                        {
                            if (!CryptographicOperations.FixedTimeEquals(
                                    descriptorDigest,
                                    acknowledgement.DescriptorDigest))
                            {
                                return 23;
                            }

                            if ((flags & RevokeFlag) == 0)
                            {
                                return 0;
                            }

                            await Task.Delay(delayMilliseconds)
                                .ConfigureAwait(false);
                            try
                            {
                                using ProtectedNamedPipeClient revoke =
                                    ProtectedNamedPipeClient.Connect(
                                        acknowledgement.RevokePipeName,
                                        broker.Snapshot(),
                                        TimeSpan.FromSeconds(2));
                                byte[] revocationNonce = RandomTestValue32();
                                Guid revokeRequestId = Guid.NewGuid();
                                try
                                {
                                    using SensitiveFrame revokeFrame =
                                        BootstrapProtocol.Encode(
                                            new RevokeRequest(
                                                revokeRequestId,
                                                (flags & MalformedRevokeFlag) != 0
                                                    ? Guid.NewGuid()
                                                    : descriptor.PublicationId,
                                                descriptorDigest,
                                                revocationNonce));
                                    await revoke.SendFrameAsync(
                                            revokeFrame.Bytes,
                                            TimeSpan.FromSeconds(2))
                                        .ConfigureAwait(false);
                                    byte[] revokeAckFrame = await revoke
                                        .ReceiveFrameAsync(
                                            TimeSpan.FromSeconds(2))
                                        .ConfigureAwait(false);
                                    if ((flags &
                                        RequireTerminalRejectionFlag) != 0)
                                    {
                                        CryptographicOperations.ZeroMemory(
                                            revokeAckFrame);
                                        return 26;
                                    }

                                    RevokeAck revokeAck = (RevokeAck)
                                        BootstrapProtocol.DecodeOwned(
                                            revokeAckFrame,
                                            BootstrapMessageType.RevokeAck,
                                            BootstrapRole.Broker,
                                            BootstrapRole.Observer);
                                    return RevokeTranscriptMatches(
                                        revokeAck,
                                        revokeRequestId,
                                        descriptor.PublicationId,
                                        descriptorDigest,
                                        revocationNonce) ? 0 : 24;
                                }
                                finally
                                {
                                    CryptographicOperations.ZeroMemory(
                                        revocationNonce);
                                }
                            }
                            catch (Exception exception) when (
                                (flags & AllowCloseFlag) != 0 &&
                                IsExpectedChildClosure(exception))
                            {
                                return 0;
                            }
                        }
                        finally
                        {
                            CryptographicOperations.ZeroMemory(descriptorDigest);
                        }
                    }
                    catch (Exception exception) when (
                        (flags & AllowCloseFlag) != 0 &&
                        IsExpectedChildClosure(exception))
                    {
                        return 0;
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(publicationNonce);
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(nameBytes);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(header);
            }
        }
        catch
        {
            return 25;
        }
    }

    private static async Task<int> RunBrokerControllerChild()
    {
        const byte MalformedTranscriptFlag = 1;
        const byte BadProofFlag = 2;
        const byte AllowCloseFlag = 4;
        const byte RequireTerminalRejectionFlag = 8;
        try
        {
            Stream input = System.Console.OpenStandardInput();
            byte[] header = new byte[sizeof(uint) + sizeof(byte) +
                sizeof(int) + sizeof(ushort)];
            try
            {
                using CancellationTokenSource cancellation = new(TestTimeout);
                await input.ReadExactlyAsync(header, cancellation.Token)
                    .ConfigureAwait(false);
                uint brokerProcessId = BinaryPrimitives.ReadUInt32LittleEndian(
                    header.AsSpan(0, sizeof(uint)));
                byte flags = header[sizeof(uint)];
                int delayMilliseconds = BinaryPrimitives.ReadInt32LittleEndian(
                    header.AsSpan(sizeof(uint) + sizeof(byte), sizeof(int)));
                int descriptorLength = BinaryPrimitives.ReadUInt16LittleEndian(
                    header.AsSpan(
                        sizeof(uint) + sizeof(byte) + sizeof(int),
                        sizeof(ushort)));
                if (brokerProcessId == 0 && flags == 0 &&
                    delayMilliseconds == 0 && descriptorLength == 0)
                {
                    return 0;
                }

                if (brokerProcessId == 0 || delayMilliseconds < 0 ||
                    descriptorLength is < 1 or > BootstrapDescriptor.MaximumEncodedLength ||
                    (flags & ~(MalformedTranscriptFlag | BadProofFlag |
                        AllowCloseFlag | RequireTerminalRejectionFlag)) != 0)
                {
                    return 30;
                }

                byte[] descriptorBytes = new byte[descriptorLength];
                try
                {
                    await input.ReadExactlyAsync(
                            descriptorBytes,
                            cancellation.Token)
                        .ConfigureAwait(false);
                    BootstrapDescriptor descriptor =
                        BootstrapDescriptor.Parse(descriptorBytes);
                    byte[] descriptorDigest = descriptor.ComputeDigest();
                    byte[] controllerNonce = RandomTestValue32();
                    Guid claimRequestId = Guid.NewGuid();
                    Guid claimedPublicationId =
                        (flags & MalformedTranscriptFlag) != 0
                            ? Guid.NewGuid()
                            : descriptor.PublicationId;
                    try
                    {
                        using ProcessIdentityLease broker =
                            ProcessIdentityLease.Capture(brokerProcessId);
                        using ProcessIdentityLease controller =
                            ProcessIdentityLease.Capture(
                                checked((uint)Environment.ProcessId));
                        await Task.Delay(delayMilliseconds).ConfigureAwait(false);
                        try
                        {
                            using ProtectedNamedPipeClient claim =
                                ProtectedNamedPipeClient.Connect(
                                    descriptor.ClaimPipeName,
                                    broker.Snapshot(),
                                    TimeSpan.FromSeconds(2));
                            using (SensitiveFrame claimFrame =
                                   BootstrapProtocol.Encode(
                                       new ClaimRequest(
                                           claimRequestId,
                                           claimedPublicationId,
                                           descriptorDigest,
                                           controllerNonce)))
                            {
                                await claim.SendFrameAsync(
                                        claimFrame.Bytes,
                                        TimeSpan.FromSeconds(2))
                                    .ConfigureAwait(false);
                            }
                            byte[] grantFrame = await claim.ReceiveFrameAsync(
                                    TimeSpan.FromSeconds(2))
                                .ConfigureAwait(false);
                            if ((flags & RequireTerminalRejectionFlag) != 0)
                            {
                                CryptographicOperations.ZeroMemory(grantFrame);
                                return 33;
                            }

                            ProtectedNamedPipeClient? receipt = null;
                            byte[]? receiptNonce = null;
                            try
                            {
                                using (ClaimGrant grant = (ClaimGrant)
                                       BootstrapProtocol.DecodeOwned(
                                           grantFrame,
                                           BootstrapMessageType.ClaimGrant,
                                           BootstrapRole.Broker,
                                           BootstrapRole.Controller))
                                {
                                    if (!ClaimGrantMatches(
                                            grant,
                                            claimRequestId,
                                            descriptor.PublicationId,
                                            descriptorDigest,
                                            controllerNonce) ||
                                        !descriptor.Verify(
                                            grant.Token.Bytes,
                                            descriptor.ObserverBinding,
                                            broker.Snapshot(),
                                    CanonicalTestUtcNow(),
                                            descriptor.ExpiresUtc -
                                                descriptor.CreatedUtc))
                                    {
                                        return 31;
                                    }

                                    (receipt, receiptNonce) =
                                        await SendClaimReceiptAsync(
                                                grant,
                                                broker.Snapshot(),
                                                claimRequestId,
                                                descriptor.PublicationId,
                                                descriptorDigest,
                                                controllerNonce,
                                                (flags & BadProofFlag) != 0)
                                            .ConfigureAwait(false);
                                }

                                byte[] finalFrame = await receipt
                                    .ReceiveFrameAsync(
                                        TimeSpan.FromSeconds(2))
                                    .ConfigureAwait(false);
                                ClaimFinalAck finalAck = (ClaimFinalAck)
                                    BootstrapProtocol.DecodeOwned(
                                        finalFrame,
                                        BootstrapMessageType.ClaimFinalAck,
                                        BootstrapRole.Broker,
                                        BootstrapRole.Controller);
                                return ClaimTranscriptMatches(
                                    finalAck,
                                    claimRequestId,
                                    descriptor.PublicationId,
                                    descriptorDigest,
                                    controllerNonce,
                                    receiptNonce) ? 0 : 32;
                            }
                            finally
                            {
                                receipt?.Dispose();
                                if (receiptNonce is not null)
                                {
                                    CryptographicOperations.ZeroMemory(
                                        receiptNonce);
                                }
                            }
                        }
                        catch (Exception exception) when (
                            (flags & AllowCloseFlag) != 0 &&
                            IsExpectedChildClosure(exception))
                        {
                            return 0;
                        }
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(descriptorDigest);
                        CryptographicOperations.ZeroMemory(controllerNonce);
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(descriptorBytes);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(header);
            }
        }
        catch
        {
            return 33;
        }
    }

    private static bool ClaimGrantMatches(
        ClaimGrant grant,
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> controllerNonce)
    {
        return grant.RequestId == requestId &&
            grant.PublicationId == publicationId &&
            CryptographicOperations.FixedTimeEquals(
                grant.DescriptorDigest,
                descriptorDigest) &&
            CryptographicOperations.FixedTimeEquals(
                grant.ControllerNonce,
                controllerNonce);
    }

    private static async Task<(
        ProtectedNamedPipeClient Receipt,
        byte[] ReceiptNonce)> SendClaimReceiptAsync(
        ClaimGrant grant,
        BootstrapBinding brokerBinding,
        Guid requestId,
        Guid publicationId,
        ReadOnlyMemory<byte> descriptorDigest,
        ReadOnlyMemory<byte> controllerNonce,
        bool sendBadProof)
    {
        ProtectedNamedPipeClient? receipt = null;
        byte[]? receiptNonce = grant.ReceiptNonce.ToArray();
        try
        {
            receipt = ProtectedNamedPipeClient.Connect(
                grant.ReceiptPipeName,
                brokerBinding,
                TimeSpan.FromSeconds(2));
            using ClaimReceiptProof proof =
                BootstrapProtocol.ComputeClaimReceiptProof(
                    grant.Token.Bytes,
                    publicationId,
                    descriptorDigest.Span,
                    controllerNonce.Span,
                    receiptNonce);
            byte[] sentProof = proof.Bytes.ToArray();
            try
            {
                if (sendBadProof)
                {
                    sentProof[0] ^= 0x80;
                }

                using ClaimReceipt receiptMessage = new(
                    requestId,
                    publicationId,
                    descriptorDigest.Span,
                    controllerNonce.Span,
                    receiptNonce,
                    sentProof);
                using SensitiveFrame receiptFrame =
                    BootstrapProtocol.Encode(receiptMessage);
                await receipt.SendFrameAsync(
                        receiptFrame.Bytes,
                        TimeSpan.FromSeconds(2))
                    .ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(sentProof);
            }

            ProtectedNamedPipeClient result = receipt;
            byte[] resultNonce = receiptNonce;
            receipt = null;
            receiptNonce = null;
            return (result, resultNonce);
        }
        finally
        {
            receipt?.Dispose();
            if (receiptNonce is not null)
            {
                CryptographicOperations.ZeroMemory(receiptNonce);
            }
        }
    }

    private static bool ClaimTranscriptMatches(
        ClaimTranscript transcript,
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> controllerNonce,
        ReadOnlySpan<byte> receiptNonce)
    {
        return transcript.RequestId == requestId &&
            transcript.PublicationId == publicationId &&
            CryptographicOperations.FixedTimeEquals(
                transcript.DescriptorDigest,
                descriptorDigest) &&
            CryptographicOperations.FixedTimeEquals(
                transcript.ControllerNonce,
                controllerNonce) &&
            CryptographicOperations.FixedTimeEquals(
                transcript.ReceiptNonce,
                receiptNonce);
    }

    private static bool RevokeTranscriptMatches(
        RevokeTranscript transcript,
        Guid requestId,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> revocationNonce)
    {
        return transcript.RequestId == requestId &&
            transcript.PublicationId == publicationId &&
            CryptographicOperations.FixedTimeEquals(
                transcript.DescriptorDigest,
                descriptorDigest) &&
            CryptographicOperations.FixedTimeEquals(
                transcript.RevocationNonce,
                revocationNonce);
    }

    private static bool IsExpectedChildClosure(Exception exception)
    {
        return exception is IOException or
            TimeoutException or
            OperationCanceledException or
            ObjectDisposedException;
    }

    private static bool ContainsSequence(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> candidate)
    {
        if (candidate.IsEmpty || candidate.Length > source.Length)
        {
            return false;
        }

        for (int offset = 0;
            offset <= source.Length - candidate.Length;
            offset++)
        {
            if (source.Slice(offset, candidate.Length).SequenceEqual(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static void CreateProtectedTestDirectory(
        string path,
        string ownerSid,
        bool includeSystem)
    {
        nint descriptor = CreateTestSecurityDescriptor(
            ownerSid,
            includeSystem);
        try
        {
            NativeMethods.SecurityAttributes attributes = new()
            {
                Length = checked((uint)Marshal.SizeOf<
                    NativeMethods.SecurityAttributes>()),
                SecurityDescriptor = descriptor,
                InheritHandle = 0,
            };
            if (CreateDirectoryForTest(path, ref attributes) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "Creating the protected test directory failed");
            }
        }
        finally
        {
            _ = NativeMethods.LocalFree(descriptor);
        }
    }

    private static void CreateProtectedTestFile(
        string path,
        string ownerSid,
        ReadOnlySpan<byte> bytes,
        bool includeSystem)
    {
        nint descriptor = CreateTestSecurityDescriptor(
            ownerSid,
            includeSystem);
        SafeFileHandle? file = null;
        nint attributesPointer = 0;
        try
        {
            NativeMethods.SecurityAttributes attributes = new()
            {
                Length = checked((uint)Marshal.SizeOf<
                    NativeMethods.SecurityAttributes>()),
                SecurityDescriptor = descriptor,
                InheritHandle = 0,
            };
            attributesPointer = Marshal.AllocHGlobal(
                Marshal.SizeOf<NativeMethods.SecurityAttributes>());
            Marshal.StructureToPtr(
                attributes,
                attributesPointer,
                fDeleteOld: false);
            nint raw = NativeMethods.CreateFile(
                path,
                NativeMethods.GenericRead |
                    NativeMethods.GenericWrite |
                    NativeMethods.ReadControl |
                    NativeMethods.DeleteAccess,
                NativeMethods.FileShareRead | NativeMethods.FileShareDelete,
                attributesPointer,
                NativeMethods.CreateNew,
                NativeMethods.FileAttributeNormal |
                    NativeMethods.FileFlagOpenReparsePoint,
                0);
            file = new SafeFileHandle(raw, true);
            if (file.IsInvalid)
            {
                throw NativeMethods.Win32Failure(
                    "Creating the protected test file failed");
            }

            GuardedDescriptorDirectory.WriteExact(file, bytes);
            GuardedDescriptorDirectory.Flush(file);
        }
        finally
        {
            file?.Dispose();
            if (attributesPointer != 0)
            {
                Marshal.FreeHGlobal(attributesPointer);
            }
            _ = NativeMethods.LocalFree(descriptor);
        }
    }

    private static nint CreateTestSecurityDescriptor(
        string ownerSid,
        bool includeSystem)
    {
        string dacl = includeSystem
            ? "D:P(A;;FA;;;SY)(A;;FA;;;" + ownerSid + ")"
            : "D:P(A;;FA;;;" + ownerSid + ")";
        if (NativeMethods.ConvertStringSecurityDescriptor(
                "O:" + ownerSid + dacl,
                NativeMethods.SddlRevision1,
                out nint descriptor,
                out uint size) == 0 ||
            descriptor == 0 || size == 0)
        {
            throw NativeMethods.Win32Failure(
                "Creating the protected test security descriptor failed");
        }

        return descriptor;
    }

    private static void PosixUnlinkTestFile(string path)
    {
        nint raw = NativeMethods.CreateFile(
            path,
            NativeMethods.DeleteAccess | NativeMethods.ReadControl,
            NativeMethods.FileShareRead |
                NativeMethods.FileShareWrite |
                NativeMethods.FileShareDelete,
            0,
            NativeMethods.OpenExisting,
            NativeMethods.FileFlagOpenReparsePoint,
            0);
        using SafeFileHandle file = new(raw, true);
        if (file.IsInvalid)
        {
            throw NativeMethods.Win32Failure(
                "Opening the exact test file for POSIX unlink failed");
        }

        GuardedDescriptorDirectory.MarkPosixDelete(file);
        file.Dispose();
        if (File.Exists(path))
        {
            throw new IOException(
                "The exact test file name remained after POSIX unlink.");
        }
    }

    private static SafeFileHandle OpenRenameSourceForTest(string path)
    {
        nint raw = NativeMethods.CreateFile(
            path,
            NativeMethods.DeleteAccess | NativeMethods.ReadControl,
            NativeMethods.FileShareRead |
                NativeMethods.FileShareWrite |
                NativeMethods.FileShareDelete,
            0,
            NativeMethods.OpenExisting,
            NativeMethods.FileFlagOpenReparsePoint,
            0);
        SafeFileHandle file = new(raw, true);
        if (file.IsInvalid)
        {
            file.Dispose();
            throw NativeMethods.Win32Failure(
                "Opening the retained-root rename source failed");
        }

        return file;
    }

    private static void CreateDirectoryJunction(
        string junctionPath,
        string targetPath)
    {
        const uint FsctlSetReparsePoint = 0x000900A4;
        const uint IoReparseTagMountPoint = 0xA0000003;
        string target = Path.GetFullPath(targetPath);
        byte[] substitute = Encoding.Unicode.GetBytes(@"\??\" + target);
        byte[] print = Encoding.Unicode.GetBytes(target);
        int printOffset = checked(substitute.Length + sizeof(ushort));
        int pathBytes = checked(
            printOffset + print.Length + sizeof(ushort));
        int reparseDataLength = checked(8 + pathBytes);
        byte[] buffer = new byte[checked(8 + reparseDataLength)];
        nint native = 0;
        try
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                buffer.AsSpan(0, sizeof(uint)),
                IoReparseTagMountPoint);
            BinaryPrimitives.WriteUInt16LittleEndian(
                buffer.AsSpan(4, sizeof(ushort)),
                checked((ushort)reparseDataLength));
            BinaryPrimitives.WriteUInt16LittleEndian(
                buffer.AsSpan(8, sizeof(ushort)),
                0);
            BinaryPrimitives.WriteUInt16LittleEndian(
                buffer.AsSpan(10, sizeof(ushort)),
                checked((ushort)substitute.Length));
            BinaryPrimitives.WriteUInt16LittleEndian(
                buffer.AsSpan(12, sizeof(ushort)),
                checked((ushort)printOffset));
            BinaryPrimitives.WriteUInt16LittleEndian(
                buffer.AsSpan(14, sizeof(ushort)),
                checked((ushort)print.Length));
            substitute.CopyTo(buffer, 16);
            print.CopyTo(buffer, checked(16 + printOffset));

            nint raw = NativeMethods.CreateFile(
                junctionPath,
                NativeMethods.GenericWrite | NativeMethods.ReadControl,
                NativeMethods.FileShareRead |
                    NativeMethods.FileShareWrite |
                    NativeMethods.FileShareDelete,
                0,
                NativeMethods.OpenExisting,
                NativeMethods.FileFlagBackupSemantics |
                    NativeMethods.FileFlagOpenReparsePoint,
                0);
            using SafeFileHandle junction = new(raw, true);
            if (junction.IsInvalid)
            {
                throw NativeMethods.Win32Failure(
                    "Opening the directory junction fixture failed");
            }

            native = Marshal.AllocHGlobal(buffer.Length);
            Marshal.Copy(buffer, 0, native, buffer.Length);
            if (DeviceIoControlForTest(
                    junction,
                    FsctlSetReparsePoint,
                    native,
                    checked((uint)buffer.Length),
                    0,
                    0,
                    out uint returned,
                    0) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "Creating the directory junction fixture failed");
            }

            AssertEqual(0U, returned,
                "directory junction control output length");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(substitute);
            CryptographicOperations.ZeroMemory(print);
            CryptographicOperations.ZeroMemory(buffer);
            if (native != 0)
            {
                Marshal.FreeHGlobal(native);
            }
        }
    }

    private static void CreateHardLinkForTest(
        string linkPath,
        string existingPath)
    {
        if (CreateHardLinkForTestNative(linkPath, existingPath, 0) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Creating the trusted artifact hard-link fixture failed");
        }
    }

    private static void DeleteTestDirectoryTree(string path)
    {
        DirectoryInfo root = new(path);
        if (!root.Exists)
        {
            return;
        }

        if (root.FullName.Length < 4 ||
            !root.Name.StartsWith(
                "hrc-bootstrap-file-test-",
                StringComparison.Ordinal) ||
            !char.IsAsciiLetter(root.FullName[0]))
        {
            throw new InvalidOperationException(
                "Refusing to clean an unexpected test directory.");
        }

        foreach (FileSystemInfo entry in root.EnumerateFileSystemInfos())
        {
            bool directory =
                (entry.Attributes & FileAttributes.Directory) != 0;
            bool reparse =
                (entry.Attributes & FileAttributes.ReparsePoint) != 0;
            if (directory)
            {
                Directory.Delete(entry.FullName, recursive: !reparse);
            }
            else
            {
                if (!reparse)
                {
                    File.SetAttributes(entry.FullName, FileAttributes.Normal);
                }
                File.Delete(entry.FullName);
            }
        }

        root.Delete();
    }

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateDirectoryW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial int CreateDirectoryForTest(
        string path,
        ref NativeMethods.SecurityAttributes securityAttributes);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "DeviceIoControl",
        SetLastError = true)]
    private static partial int DeviceIoControlForTest(
        SafeFileHandle device,
        uint controlCode,
        nint inputBuffer,
        uint inputBufferSize,
        nint outputBuffer,
        uint outputBufferSize,
        out uint bytesReturned,
        nint overlapped);

    [LibraryImport(
        "kernel32.dll",
        EntryPoint = "CreateHardLinkW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    private static partial int CreateHardLinkForTestNative(
        string fileName,
        string existingFileName,
        nint securityAttributes);

    private static byte[] RandomTestValue32()
    {
        using SecretBuffer value = SecretBuffer.CreateRandom32();
        byte[] result = new byte[SecretBuffer.Length];
        value.CopyTo(result);
        return result;
    }

    private static DateTimeOffset CanonicalTestUtcNow()
    {
        DateTimeOffset utc = TimeProvider.System.GetUtcNow().ToUniversalTime();
        long ticks = utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
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

    private static TaskCompletionSource<bool> NewSignal()
    {
        return new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static async Task<Exception> CaptureExceptionAsync(Task action)
    {
        try
        {
            await action.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException(
            "Expected an observable failure, but the task completed successfully.");
    }

    private static async Task<Exception> CaptureExceptionAsync(
        Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return exception;
        }

        throw new InvalidOperationException(
            "Expected an observable failure, but the operation completed successfully.");
    }

    private static bool ContainsException<TException>(Exception exception)
        where TException : Exception
    {
        if (exception is TException)
        {
            return true;
        }

        if (exception is AggregateException aggregate)
        {
            return aggregate.InnerExceptions.Any(ContainsException<TException>);
        }

        return exception.InnerException is not null &&
            ContainsException<TException>(exception.InnerException);
    }

    private static byte[] GetStoredDescriptorBacking(
        InMemoryBootstrapPublicationStore store)
    {
        object entry = GetPrivateField(store, "current") ??
            throw new InvalidOperationException(
                "The publication store has no current entry.");
        return GetPrivateField(entry, "descriptor") as byte[] ??
            throw new InvalidOperationException(
                "The publication store descriptor backing is unavailable.");
    }

    private static BootstrapDescriptor GetPublishedDescriptor(
        InMemoryBootstrapPublicationStore store)
    {
        if (!store.TryRead(out BootstrapPublicationSnapshot? snapshot) ||
            snapshot is null)
        {
            throw new InvalidOperationException(
                "The broker publication is unavailable.");
        }

        using (snapshot)
        {
            return BootstrapDescriptor.Parse(snapshot.Descriptor);
        }
    }

    private static string GetBrokerPipeName(
        BootstrapBrokerSession session,
        string fieldName)
    {
        ProtectedNamedPipe pipe = GetPrivateField(session, fieldName) as
            ProtectedNamedPipe ?? throw new InvalidOperationException(
                $"The broker {fieldName} is unavailable.");
        return pipe.Name;
    }

    private static byte[] GetBrokerTokenBacking(
        BootstrapBrokerSession session)
    {
        SecretBuffer token = GetPrivateField(session, "brokerToken") as
            SecretBuffer ?? throw new InvalidOperationException(
                "The broker token is unavailable.");
        return GetPrivateField(token, "bytes") as byte[] ??
            throw new InvalidOperationException(
                "The broker token backing is unavailable.");
    }

    private static object? GetPrivateField(object target, string name)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.GetType().GetField(
                name,
                System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)?
            .GetValue(target);
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

    private sealed class FilePublicationTestDirectory : IDisposable
    {
        private bool disposed;

        internal FilePublicationTestDirectory(bool includeSystem = true)
        {
            using ProcessIdentityLease current = ProcessIdentityLease.Capture(
                checked((uint)Environment.ProcessId));
            OwnerSid = current.UserSid;
            string tempRoot = System.IO.Path.TrimEndingDirectorySeparator(
                System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()));
            if (tempRoot.Length < 3 ||
                !char.IsAsciiLetter(tempRoot[0]) ||
                tempRoot[1] != ':' ||
                tempRoot[2] != System.IO.Path.DirectorySeparatorChar)
            {
                throw new PlatformNotSupportedException(
                    "File publication tests require a local drive temp root.");
            }

            Path = System.IO.Path.Combine(
                tempRoot,
                "hrc-bootstrap-file-test-" + Guid.NewGuid().ToString("N"));
            CreateProtectedTestDirectory(Path, OwnerSid, includeSystem);
        }

        internal string Path { get; }

        internal string OwnerSid { get; }

        internal string FinalPath => System.IO.Path.Combine(
            Path,
            FileBootstrapPublicationStore.FinalFileName);

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (!Directory.Exists(Path))
            {
                return;
            }

            DeleteTestDirectoryTree(Path);
        }
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

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset initialUtc;
        private readonly TimeSpan advanceOnFirstUtcNow;
        private long timestamp;
        private int utcNowReads;

        internal ManualTimeProvider(
            DateTimeOffset initialUtc,
            TimeSpan? advanceOnFirstUtcNow = null)
        {
            this.initialUtc = initialUtc;
            this.advanceOnFirstUtcNow = advanceOnFirstUtcNow ?? TimeSpan.Zero;
            if (this.advanceOnFirstUtcNow < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(advanceOnFirstUtcNow));
            }
        }

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow()
        {
            if (Interlocked.Increment(ref utcNowReads) == 1 &&
                advanceOnFirstUtcNow > TimeSpan.Zero)
            {
                Advance(advanceOnFirstUtcNow);
            }

            return initialUtc + TimeSpan.FromTicks(
                Interlocked.Read(ref timestamp));
        }

        public override long GetTimestamp()
        {
            return Interlocked.Read(ref timestamp);
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            return TimeProvider.System.CreateTimer(
                callback,
                state,
                dueTime,
                period);
        }

        internal void Advance(TimeSpan amount)
        {
            if (amount < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Interlocked.Add(ref timestamp, amount.Ticks);
        }
    }

    private sealed class ThrowingFirstTimestampTimeProvider : TimeProvider
    {
        private int timestampCalls;

        public override long TimestampFrequency =>
            TimeProvider.System.TimestampFrequency;

        public override DateTimeOffset GetUtcNow()
        {
            return TimeProvider.System.GetUtcNow();
        }

        public override long GetTimestamp()
        {
            if (Interlocked.Increment(ref timestampCalls) == 1)
            {
                throw new TestTimeProviderException();
            }

            return TimeProvider.System.GetTimestamp();
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            return TimeProvider.System.CreateTimer(
                callback,
                state,
                dueTime,
                period);
        }
    }

    private sealed class DeadlineProbePublisher :
        IBootstrapPublicationPublisher
    {
        private readonly ManualTimeProvider clock;
        private readonly TimeSpan advanceAfterRecord;
        private TimeSpan? recordedRemaining;

        internal DeadlineProbePublisher(
            ManualTimeProvider clock,
            TimeSpan advanceAfterRecord)
        {
            ArgumentNullException.ThrowIfNull(clock);
            if (advanceAfterRecord <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(advanceAfterRecord));
            }

            this.clock = clock;
            this.advanceAfterRecord = advanceAfterRecord;
        }

        internal TimeSpan RecordedRemaining => recordedRemaining ??
            throw new InvalidOperationException(
                "The combined deadline was not observed by the publisher.");

        public ValueTask<BootstrapPublishResult> TryPublishAsync(
            ReadOnlyMemory<byte> canonicalDescriptor,
            MonotonicDeadline deadline,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            recordedRemaining = deadline.GetRemaining();
            clock.Advance(advanceAfterRecord);
            try
            {
                _ = deadline.GetRemaining();
            }
            catch (TimeoutException exception)
            {
                throw new TestDeadlineProbeException(exception);
            }

            throw new InvalidOperationException(
                "The publisher deadline was reset instead of capped by the session.");
        }
    }

    private sealed class ControlledPublicationPublisher :
        IBootstrapPublicationPublisher
    {
        private readonly InMemoryBootstrapPublicationStore store;
        private readonly bool blockBeforeCommit;
        private readonly bool blockAfterCommit;
        private readonly bool blockRemoval;
        private readonly bool returnDefaultRemovalStatus;
        private readonly Exception? publishFailure;
        private readonly Exception? removalFailure;
        private readonly Action? afterCommitBeforeReturn;
        private readonly TaskCompletionSource<bool> publishEntered = NewSignal();
        private readonly TaskCompletionSource<bool> publicationCommitted =
            NewSignal();
        private readonly TaskCompletionSource<bool> publishCancelled = NewSignal();
        private readonly TaskCompletionSource<bool> allowPublishReturn = NewSignal();
        private readonly TaskCompletionSource<bool> removalStarted = NewSignal();
        private readonly TaskCompletionSource<bool> allowRemoval = NewSignal();
        private BootstrapPublicationLease? exactStoreLease;
        private int removalCalls;

        internal ControlledPublicationPublisher(
            InMemoryBootstrapPublicationStore store,
            bool blockBeforeCommit = false,
            bool blockAfterCommit = false,
            bool blockRemoval = false,
            bool returnDefaultRemovalStatus = false,
            Exception? publishFailure = null,
            Exception? removalFailure = null,
            Action? afterCommitBeforeReturn = null)
        {
            ArgumentNullException.ThrowIfNull(store);
            if (blockBeforeCommit && blockAfterCommit)
            {
                throw new ArgumentException(
                    "A controlled publisher cannot block on both sides of commit.");
            }

            this.store = store;
            this.blockBeforeCommit = blockBeforeCommit;
            this.blockAfterCommit = blockAfterCommit;
            this.blockRemoval = blockRemoval;
            this.returnDefaultRemovalStatus = returnDefaultRemovalStatus;
            this.publishFailure = publishFailure;
            this.removalFailure = removalFailure;
            this.afterCommitBeforeReturn = afterCommitBeforeReturn;
        }

        internal Task PublishEntered => publishEntered.Task;

        internal Task PublicationCommitted => publicationCommitted.Task;

        internal Task PublishCancelled => publishCancelled.Task;

        internal Task RemovalStarted => removalStarted.Task;

        internal int RemovalCalls => Volatile.Read(ref removalCalls);

        public async ValueTask<BootstrapPublishResult> TryPublishAsync(
            ReadOnlyMemory<byte> canonicalDescriptor,
            MonotonicDeadline deadline,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = deadline.GetRemaining();
            publishEntered.TrySetResult(true);
            if (blockBeforeCommit)
            {
                try
                {
                    await allowPublishReturn.Task.WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    publishCancelled.TrySetResult(true);
                    throw;
                }
            }

            Exception? configuredPublishFailure = publishFailure;
            if (configuredPublishFailure is not null)
            {
                await Task.Yield();
                throw configuredPublishFailure;
            }

            BootstrapPublishResult result = await store.TryPublishAsync(
                    canonicalDescriptor,
                    deadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.Status != BootstrapPublishStatus.Published ||
                result.Lease is null)
            {
                return result;
            }

            BootstrapPublicationLease inner = result.Lease;
            exactStoreLease = inner;
            ControlledPublicationLease controlled = new(this, inner);
            publicationCommitted.TrySetResult(true);
            afterCommitBeforeReturn?.Invoke();
            if (blockAfterCommit)
            {
                await allowPublishReturn.Task.ConfigureAwait(false);
            }

            return BootstrapPublishResult.Published(controlled);
        }

        internal void ReleasePublish()
        {
            allowPublishReturn.TrySetResult(true);
        }

        internal void ReleaseRemoval()
        {
            allowRemoval.TrySetResult(true);
        }

        internal async Task ForceRemoveAsync()
        {
            BootstrapPublicationLease lease = exactStoreLease ??
                throw new InvalidOperationException(
                    "The controlled publisher has no exact store lease.");
            _ = await lease.RemoveExactAsync(
                    MonotonicDeadline.Start(TimeProvider.System, TestTimeout))
                .ConfigureAwait(false);
        }

        private async ValueTask<BootstrapPublicationRemovalStatus>
            RemoveControlledAsync(
                BootstrapPublicationLease inner,
                MonotonicDeadline deadline)
        {
            Interlocked.Increment(ref removalCalls);
            removalStarted.TrySetResult(true);
            if (blockRemoval)
            {
                await allowRemoval.Task.ConfigureAwait(false);
            }

            Exception? configuredRemovalFailure = removalFailure;
            if (configuredRemovalFailure is not null)
            {
                await Task.Yield();
                throw configuredRemovalFailure;
            }

            if (returnDefaultRemovalStatus)
            {
                return default;
            }

            return await inner.RemoveExactAsync(deadline)
                .ConfigureAwait(false);
        }

        private sealed class ControlledPublicationLease :
            BootstrapPublicationLease
        {
            private readonly ControlledPublicationPublisher owner;
            private readonly BootstrapPublicationLease inner;

            internal ControlledPublicationLease(
                ControlledPublicationPublisher owner,
                BootstrapPublicationLease inner)
            {
                this.owner = owner;
                this.inner = inner;
            }

            protected internal override
                ValueTask<BootstrapPublicationRemovalStatus>
                RemoveExactCoreAsync(MonotonicDeadline deadline)
            {
                return owner.RemoveControlledAsync(inner, deadline);
            }
        }
    }

    private sealed class LatchPublicationLease : BootstrapPublicationLease
    {
        private readonly TaskCompletionSource<bool> removalStarted = NewSignal();
        private readonly TaskCompletionSource<bool> allowRemoval = NewSignal();
        private int removalCalls;

        internal Task RemovalStarted => removalStarted.Task;

        internal int RemovalCalls => Volatile.Read(ref removalCalls);

        internal void ReleaseRemoval()
        {
            allowRemoval.TrySetResult(true);
        }

        protected internal override async
            ValueTask<BootstrapPublicationRemovalStatus>
            RemoveExactCoreAsync(MonotonicDeadline deadline)
        {
            _ = deadline.GetRemaining();
            Interlocked.Increment(ref removalCalls);
            removalStarted.TrySetResult(true);
            await allowRemoval.Task.ConfigureAwait(false);
            return BootstrapPublicationRemovalStatus.Removed;
        }
    }

    private sealed class SynchronouslyThrowingPublicationLease :
        BootstrapPublicationLease
    {
        private int removalCalls;

        internal int RemovalCalls => Volatile.Read(ref removalCalls);

        protected internal override ValueTask<BootstrapPublicationRemovalStatus>
            RemoveExactCoreAsync(MonotonicDeadline deadline)
        {
            _ = deadline.GetRemaining();
            Interlocked.Increment(ref removalCalls);
            throw new TestRemovalException();
        }
    }

    private sealed class TestPublisherException : Exception
    {
        internal TestPublisherException()
            : base("Synthetic asynchronous publication failure.")
        {
        }
    }

    private sealed class TestRemovalException : Exception
    {
        internal TestRemovalException()
            : base("Synthetic exact-removal failure.")
        {
        }
    }

    private sealed class TestTimeProviderException : Exception
    {
        internal TestTimeProviderException()
            : base("Synthetic first timestamp failure.")
        {
        }
    }

    private sealed class TestDeadlineProbeException : Exception
    {
        internal TestDeadlineProbeException(TimeoutException innerException)
            : base("Synthetic combined deadline expiry.", innerException)
        {
        }
    }

    private sealed class TestCancellationCallbackException : Exception
    {
        internal TestCancellationCallbackException()
            : base("Synthetic cancellation callback failure.")
        {
        }
    }

    private sealed class BrokerFixture : IDisposable
    {
        private bool disposed;

        private BrokerFixture(
            TestChild observer,
            TestChild controller,
            BootstrapBinding observerBinding,
            BootstrapBinding controllerBinding,
            BootstrapBinding brokerBinding,
            InMemoryBootstrapPublicationStore store,
            BootstrapBrokerSession session,
            TimeSpan publicationLifetime)
        {
            Observer = observer;
            Controller = controller;
            ObserverBinding = observerBinding;
            ControllerBinding = controllerBinding;
            BrokerBinding = brokerBinding;
            Store = store;
            Session = session;
            PublicationLifetime = publicationLifetime;
        }

        internal TestChild Observer { get; }

        internal TestChild Controller { get; }

        internal BootstrapBinding ObserverBinding { get; }

        internal BootstrapBinding ControllerBinding { get; }

        internal BootstrapBinding BrokerBinding { get; }

        internal InMemoryBootstrapPublicationStore Store { get; }

        internal BootstrapBrokerSession Session { get; }

        internal TimeSpan PublicationLifetime { get; }

        internal uint BrokerProcessId => BrokerBinding.ProcessId;

        internal static BrokerFixture Start(
            TimeSpan? publicationLifetime = null,
            TimeSpan? sessionLifetime = null,
            TimeProvider? timeProvider = null,
            Func<Task, Task, Task, CancellationToken, Task>?
                beforeArbitrationTestHook = null,
            Func<InMemoryBootstrapPublicationStore,
                IBootstrapPublicationPublisher>? publisherFactory = null)
        {
            TimeSpan publication = publicationLifetime ??
                TimeSpan.FromSeconds(4);
            TimeSpan session = sessionLifetime ?? TimeSpan.FromSeconds(5);
            TestChild? observer = null;
            TestChild? controller = null;
            InMemoryBootstrapPublicationStore? store = null;
            BootstrapBrokerSession? brokerSession = null;
            try
            {
                observer = StartChild(BrokerObserverChildMode);
                controller = StartChild(BrokerControllerChildMode);
                using ProcessIdentityLease observerIdentity =
                    ProcessIdentityLease.Capture(observer.ProcessId);
                using ProcessIdentityLease controllerIdentity =
                    ProcessIdentityLease.Capture(controller.ProcessId);
                using ProcessIdentityLease brokerIdentity =
                    ProcessIdentityLease.Capture(
                        checked((uint)Environment.ProcessId));
                BootstrapBinding observerBinding = observerIdentity.Snapshot();
                BootstrapBinding controllerBinding = controllerIdentity.Snapshot();
                BootstrapBinding brokerBinding = brokerIdentity.Snapshot();
                store = new InMemoryBootstrapPublicationStore();
                IBootstrapPublicationPublisher publisher =
                    publisherFactory?.Invoke(store) ?? store;
                brokerSession = new BootstrapBrokerSession(
                    observerBinding,
                    controllerBinding,
                    brokerBinding,
                    publisher,
                    timeProvider ?? TimeProvider.System,
                    publication,
                    session,
                    beforeArbitrationTestHook);
                BrokerFixture result = new(
                    observer,
                    controller,
                    observerBinding,
                    controllerBinding,
                    brokerBinding,
                    store,
                    brokerSession,
                    publication);
                observer = null;
                controller = null;
                store = null;
                brokerSession = null;
                return result;
            }
            finally
            {
                brokerSession?.Dispose();
                store?.Dispose();
                controller?.Dispose();
                observer?.Dispose();
            }
        }

        internal async Task SendBrokerClaimAsync(
            bool malformedTranscript,
            bool badProof,
            bool allowExpectedClose,
            int delayMilliseconds,
            bool expectTerminalRejection = false)
        {
            if (!Store.TryRead(out BootstrapPublicationSnapshot? snapshot) ||
                snapshot is null)
            {
                throw new InvalidOperationException(
                    "The broker publication is not visible.");
            }

            using (snapshot)
            {
                byte[] descriptor = snapshot.Descriptor.ToArray();
                try
                {
                    await Controller.SendBrokerClaimAsync(
                            BrokerProcessId,
                            descriptor,
                            malformedTranscript,
                            badProof,
                            allowExpectedClose,
                            delayMilliseconds,
                            expectTerminalRejection)
                        .ConfigureAwait(false);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(descriptor);
                }
            }
        }

        internal async Task RequireCleanExitAsync(
            bool observerUnused = false,
            bool controllerUnused = false)
        {
            if (observerUnused)
            {
                await Observer.SendBrokerObserverExitAsync()
                    .ConfigureAwait(false);
            }

            if (controllerUnused)
            {
                await Controller.SendBrokerControllerExitAsync()
                    .ConfigureAwait(false);
            }

            await Task.WhenAll(
                    Observer.RequireExitAsync(TestTimeout),
                    Controller.RequireExitAsync(TestTimeout))
                .ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Session.Dispose();
            Store.Dispose();
            Controller.Dispose();
            Observer.Dispose();
        }

        internal void DisposeAfterObservedSessionFailure()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                try
                {
                    Session.Dispose();
                }
                catch (Exception)
                {
                    // The test already asserted the shared disposal failure.
                }
            }
            finally
            {
                try
                {
                    Store.Dispose();
                }
                finally
                {
                    try
                    {
                        Controller.Dispose();
                    }
                    finally
                    {
                        Observer.Dispose();
                    }
                }
            }
        }
    }

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

        internal Task SendBrokerControllerExitAsync()
        {
            return WriteCommandAsync(new byte[sizeof(uint) + sizeof(byte) +
                sizeof(int) + sizeof(ushort)]);
        }

        internal Task SendBrokerObserverExitAsync()
        {
            return WriteCommandAsync(new byte[sizeof(uint) + sizeof(byte) +
                sizeof(int) + sizeof(int) + sizeof(byte)]);
        }

        internal async Task SendBrokerPublishAsync(
            uint brokerProcessId,
            string pipeName,
            bool revokeAfterPublish,
            bool malformedRevoke,
            bool allowExpectedClose,
            int delayMilliseconds,
            TimeSpan publicationLifetime,
            bool expectTerminalRejection = false)
        {
            ProtectedNamedPipe.ValidateName(pipeName);
            if (brokerProcessId == 0 || delayMilliseconds < 0 ||
                publicationLifetime <= TimeSpan.Zero ||
                publicationLifetime.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(delayMilliseconds));
            }

            byte flags = 0;
            if (revokeAfterPublish)
            {
                flags |= 1;
            }

            if (allowExpectedClose)
            {
                flags |= 2;
            }

            if (malformedRevoke)
            {
                flags |= 4;
            }

            if (expectTerminalRejection)
            {
                flags |= 8;
            }

            byte[] name = Encoding.ASCII.GetBytes(pipeName);
            byte[] command = new byte[sizeof(uint) + sizeof(byte) +
                sizeof(int) + sizeof(int) + sizeof(byte) + name.Length];
            try
            {
                BinaryPrimitives.WriteUInt32LittleEndian(command, brokerProcessId);
                command[sizeof(uint)] = flags;
                BinaryPrimitives.WriteInt32LittleEndian(
                    command.AsSpan(sizeof(uint) + sizeof(byte)),
                    delayMilliseconds);
                BinaryPrimitives.WriteInt32LittleEndian(
                    command.AsSpan(
                        sizeof(uint) + sizeof(byte) + sizeof(int)),
                    checked((int)publicationLifetime.TotalMilliseconds));
                command[sizeof(uint) + sizeof(byte) +
                    sizeof(int) + sizeof(int)] = checked((byte)name.Length);
                name.CopyTo(command, sizeof(uint) + sizeof(byte) +
                    sizeof(int) + sizeof(int) + sizeof(byte));
                await WriteCommandAsync(command).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(name);
                CryptographicOperations.ZeroMemory(command);
            }
        }

        internal async Task SendBrokerClaimAsync(
            uint brokerProcessId,
            ReadOnlyMemory<byte> descriptor,
            bool malformedTranscript,
            bool badProof,
            bool allowExpectedClose,
            int delayMilliseconds,
            bool expectTerminalRejection = false)
        {
            if (brokerProcessId == 0 || delayMilliseconds < 0 ||
                descriptor.Length is < 1 or > BootstrapDescriptor.MaximumEncodedLength)
            {
                throw new ArgumentOutOfRangeException(nameof(delayMilliseconds));
            }

            byte flags = 0;
            if (malformedTranscript)
            {
                flags |= 1;
            }

            if (badProof)
            {
                flags |= 2;
            }

            if (allowExpectedClose)
            {
                flags |= 4;
            }

            if (expectTerminalRejection)
            {
                flags |= 8;
            }

            byte[] command = new byte[sizeof(uint) + sizeof(byte) +
                sizeof(int) + sizeof(ushort) + descriptor.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(command, brokerProcessId);
            command[sizeof(uint)] = flags;
            BinaryPrimitives.WriteInt32LittleEndian(
                command.AsSpan(sizeof(uint) + sizeof(byte)),
                delayMilliseconds);
            BinaryPrimitives.WriteUInt16LittleEndian(
                command.AsSpan(
                    sizeof(uint) + sizeof(byte) + sizeof(int)),
                checked((ushort)descriptor.Length));
            descriptor.Span.CopyTo(command.AsSpan(
                sizeof(uint) + sizeof(byte) + sizeof(int) + sizeof(ushort)));
            try
            {
                await WriteCommandAsync(command).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(command);
            }
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
