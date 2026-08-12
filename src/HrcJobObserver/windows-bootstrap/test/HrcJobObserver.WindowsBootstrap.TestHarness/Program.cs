using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security;
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
    private static readonly byte[] SyntheticRequest = { 0x31, 0x41, 0x59 };
    private static readonly byte[] SyntheticResponse = { 0x26, 0x53, 0x58 };

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
            new("protected pipe operations time out and poison the channel", TestPipeTimeout),
            new("protected pipe rejects malformed receive frames", TestMalformedReceiveFrames),
            new("protected pipe applies an exact protected DACL", TestAppliedPipeDacl),
            new("protected pipe rejects invalid frames", TestInvalidFrames),
            new("protected pipe authenticates distinct processes", TestCrossProcessIdentity),
            new("protected pipe rejects a different live process", TestCrossProcessMismatch),
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
        Assert(!BindingWith(
                valid,
                creation: valid.CreationTimeFileTime + 1).Matches(identity),
            "creation mismatch must reject");
        Assert(!BindingWith(valid, imagePath: valid.ImagePath + ".other")
                .Matches(identity),
            "image mismatch must reject");
        Assert(!BindingWith(valid, userSid: "S-1-5-18").Matches(identity),
            "user SID mismatch must reject");
        Assert(!BindingWith(valid, logonSid: "S-1-5-19").Matches(identity),
            "logon SID mismatch must reject");
        AssertThrows<ArgumentException>(() => _ = BindingWith(
            valid,
            tokenSession: valid.TokenSessionId + 1));
        Assert(!BindingWith(
                valid,
                tokenSession: valid.TokenSessionId + 1,
                processSession: valid.ProcessSessionId + 1).Matches(identity),
            "session mismatch must reject");
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
                ProtectedNamedPipeClient.Connect(server.Name, identity.Snapshot());
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
                    ProtectedNamedPipeClient.Connect(server.Name, identity.Snapshot());
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
                ProtectedNamedPipeClient.Connect(server.Name, wrong);
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
                ProtectedNamedPipeClient.Connect(server.Name, identity.Snapshot());
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

    private static async Task TestInvalidFrames()
    {
        using ProcessIdentityLease identity = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        using ProtectedNamedPipe server = ProtectedNamedPipe.Create(identity.Snapshot());
        using ManualResetEventSlim releaseClient = new(false);
        Task client = Task.Run(() =>
        {
            using ProtectedNamedPipeClient connection =
                ProtectedNamedPipeClient.Connect(server.Name, identity.Snapshot());
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
                            parent.Snapshot());
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
