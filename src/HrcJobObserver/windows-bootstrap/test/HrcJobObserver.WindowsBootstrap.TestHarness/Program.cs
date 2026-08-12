using System;
using System.Collections.Generic;
using System.ComponentModel;
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

    private static async Task<int> Main()
    {
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
}
