using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class Program
{
    private const int NativePeChecksumOffset = 0x130;
    private static readonly byte[] ExactNativeFixtureSha256 =
        Convert.FromHexString(
            "3c9bee49acfffaea7f3fae2692900b47" +
            "eef0e41e61e4ae7b14e2b1884a05fe34");
    private static readonly byte[] ExactNativeFixtureReproId =
        Convert.FromHexString(
            "3ba123e6d4167f80d4f2e48f9e4eb33f" +
            "2e58547e66f7ac1ac9da2692de334c5b");

    private static Task TestNativeFixturePeAuditRoundTrip()
    {
        NativeFixtureInputs inputs = ReadNativeFixtureInputs();
        byte[] expectedDigest = (byte[])ExactNativeFixtureSha256.Clone();
        NativeFixturePeAudit? audit = null;
        byte[]? firstDigest = null;
        byte[]? secondDigest = null;
        byte[]? reproId = null;
        try
        {
            audit = NativeFixturePeAudit.Open(
                inputs.Image,
                inputs.Manifest,
                expectedDigest);
            Assert(audit.RequiresNoDynamicIndirectControlFlow,
                "the native profile must carry its no-dynamic-indirect-control-flow boundary");
            Assert(!audit.HasGuardCfInstrumentation,
                "the exact native fixture profile must not claim absent Guard CF instrumentation");
            Assert(!audit.ProvesMachineCodeSemantics,
                "a structural PE audit must not claim a formal machine-code proof");
            Assert(!audit.IsEligibleForTrustedLaunch,
                "the source/test-only PE audit must not establish trusted launch eligibility");

            firstDigest = audit.CopyImageSha256();
            reproId = audit.CopyReproducibleBuildId();
            Assert(firstDigest.AsSpan().SequenceEqual(ExactNativeFixtureSha256),
                "the exact native fixture file identity changed");
            Assert(reproId.AsSpan().SequenceEqual(ExactNativeFixtureReproId),
                "the exact native fixture REPRO identity changed");

            // The successful audit must not retain any caller-owned backing.
            inputs.Image.AsSpan().Fill(0xa5);
            inputs.Manifest.AsSpan().Fill(0x5a);
            expectedDigest.AsSpan().Fill(0xff);
            firstDigest.AsSpan().Fill(0);
            secondDigest = audit.CopyImageSha256();
            Assert(secondDigest.AsSpan().SequenceEqual(ExactNativeFixtureSha256),
                "the PE audit did not own its independent authenticated snapshot");

            NativeFixturePeAudit disposedAudit = audit;
            disposedAudit.Dispose();
            audit = null;
            AssertThrows<ObjectDisposedException>(() =>
                disposedAudit.CopyImageSha256());
            return Task.CompletedTask;
        }
        finally
        {
            audit?.Dispose();
            inputs.Dispose();
            CryptographicOperations.ZeroMemory(expectedDigest);
            WipeNativeTestBytes(firstDigest);
            WipeNativeTestBytes(secondDigest);
            WipeNativeTestBytes(reproId);
        }
    }

    private static Task TestNativeFixturePeAuditImageLayout()
    {
        using NativeFixtureInputs inputs = ReadNativeFixtureInputs();
        AssertNativePeMutationRejected(inputs, static image =>
            BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(0xdc), 0x014c));
        AssertNativePeMutationRejected(inputs, static image =>
            BinaryPrimitives.WriteUInt32LittleEndian(
                image.AsSpan(0x204),
                0xe000_0020));
        AssertNativePeMutationRejected(inputs, static image =>
        {
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x160), 0x1000);
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x164), 1);
        });
        AssertNativePeMutationRejected(inputs, static image =>
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0xa04), 0x5000));
        AssertNativePeMutationRejected(inputs, static image => image[0x948] = 2);
        return Task.CompletedTask;
    }

    private static Task TestNativeFixturePeAuditImportsAndLoadPolicy()
    {
        using NativeFixtureInputs inputs = ReadNativeFixtureInputs();
        AssertNativePeMutationRejected(inputs, static image => image[0x9c0] = (byte)'U');
        AssertNativePeMutationRejected(inputs, static image =>
            BinaryPrimitives.WriteUInt64LittleEndian(
                image.AsSpan(0x600),
                BinaryPrimitives.ReadUInt64LittleEndian(image.AsSpan(0x600)) + 2));
        AssertNativePeMutationRejected(inputs, static image =>
        {
            ulong ordinal = BinaryPrimitives.ReadUInt64LittleEndian(
                    image.AsSpan(0x978)) |
                0x8000_0000_0000_0000UL;
            BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(0x978), ordinal);
            BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(0x600), ordinal);
        });
        AssertNativePeMutationRejected(inputs, static image =>
        {
            ulong first = BinaryPrimitives.ReadUInt64LittleEndian(
                image.AsSpan(0x978));
            ulong second = BinaryPrimitives.ReadUInt64LittleEndian(
                image.AsSpan(0x980));
            BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(0x978), second);
            BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(0x980), first);
            BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(0x600), second);
            BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(0x608), first);
        });
        AssertNativePeMutationRejected(inputs, static image =>
            BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(0x66e), 0));
        AssertNativePeMutationRejected(inputs, static image =>
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x6b0), 1));
        return Task.CompletedTask;
    }

    private static Task TestNativeFixturePeAuditDebugIdentity()
    {
        using NativeFixtureInputs inputs = ReadNativeFixtureInputs();
        AssertNativePeMutationRejected(inputs, static image => image[0x828] = (byte)'!');
        AssertNativePeMutationRejected(inputs, static image =>
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x7d0), 2));
        AssertNativePeMutationRejected(inputs, static image => image[0x924] ^= 1);
        AssertNativePeMutationRejected(inputs, static image =>
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x944), 0));
        return Task.CompletedTask;
    }

    private static Task TestNativeFixturePeAuditResourcesAndOverlay()
    {
        using NativeFixtureInputs inputs = ReadNativeFixtureInputs();
        AssertNativePeMutationRejected(inputs, static image =>
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0xc40), 0x0409));
        AssertNativePeMutationRejected(inputs, static image => image[0xc60] ^= 1);
        AssertNativePeMutationRejected(inputs, static image =>
        {
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x180), 0xf00);
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x184), 0x10);
        });
        AssertNativePeMutationRejected(inputs, static image =>
        {
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x188), 0x1000);
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x18c), 0x08);
        });

        byte[] overlay = new byte[inputs.Image.Length + 1];
        inputs.Image.CopyTo(overlay, 0);
        overlay[^1] = 0xa5;
        byte[] overlayDigest = SHA256.HashData(overlay);
        try
        {
            AssertThrows<ArgumentException>(() =>
            {
                using NativeFixturePeAudit ignored = NativeFixturePeAudit.Open(
                    overlay,
                    inputs.Manifest,
                    overlayDigest);
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(overlay);
            CryptographicOperations.ZeroMemory(overlayDigest);
        }

        return Task.CompletedTask;
    }

    private static Task TestNativeFixturePeAuditAuthenticationAndBounds()
    {
        using NativeFixtureInputs inputs = ReadNativeFixtureInputs();
        byte[] corruptedChecksum = (byte[])inputs.Image.Clone();
        byte[]? corruptedDigest = null;
        byte[] wrongDigest = (byte[])ExactNativeFixtureSha256.Clone();
        byte[] shortImage = inputs.Image[..^1];
        byte[] shortManifest = inputs.Manifest[..^1];
        try
        {
            corruptedChecksum[NativePeChecksumOffset] ^= 1;
            corruptedDigest = SHA256.HashData(corruptedChecksum);
            AssertThrows<FormatException>(() =>
            {
                using NativeFixturePeAudit ignored = NativeFixturePeAudit.Open(
                    corruptedChecksum,
                    inputs.Manifest,
                    corruptedDigest);
            });

            wrongDigest[0] ^= 1;
            AssertThrows<SecurityException>(() =>
            {
                using NativeFixturePeAudit ignored = NativeFixturePeAudit.Open(
                    inputs.Image,
                    inputs.Manifest,
                    wrongDigest);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using NativeFixturePeAudit ignored = NativeFixturePeAudit.Open(
                    shortImage,
                    inputs.Manifest,
                    ExactNativeFixtureSha256);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using NativeFixturePeAudit ignored = NativeFixturePeAudit.Open(
                    inputs.Image,
                    shortManifest,
                    ExactNativeFixtureSha256);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using NativeFixturePeAudit ignored = NativeFixturePeAudit.Open(
                    inputs.Image,
                    inputs.Manifest,
                    ExactNativeFixtureSha256.AsSpan(0, 31));
            });
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(corruptedChecksum);
            WipeNativeTestBytes(corruptedDigest);
            CryptographicOperations.ZeroMemory(wrongDigest);
            CryptographicOperations.ZeroMemory(shortImage);
            CryptographicOperations.ZeroMemory(shortManifest);
        }
    }

    private static async Task TestNativeFixtureRuntimeRoles()
    {
        string moduleRoot = FindWindowsBootstrapModuleRoot();
        string nativeRoot = Path.GetFullPath(Path.Combine(
            moduleRoot,
            "build",
            "native"));
        string executablePath = Path.GetFullPath(Path.Combine(
            nativeRoot,
            "HrcJobObserver.NativeFixture.exe"));

        int exitCode = await RunNativeFixtureRoleAsync(
            executablePath,
            nativeRoot,
            "--native-exit").ConfigureAwait(false);
        AssertEqual(0, exitCode, "native Exit role exit code");

        int invalidCode = await RunNativeFixtureRoleAsync(
            executablePath,
            nativeRoot,
            "--native-invalid").ConfigureAwait(false);
        AssertEqual(87, invalidCode, "native invalid-role exit code");

        await AssertNativeFixtureRoleBlocksAsync(
            executablePath,
            nativeRoot).ConfigureAwait(false);
    }

    private static async Task<int> RunNativeFixtureRoleAsync(
        string executablePath,
        string workingDirectory,
        string argument)
    {
        using Process process = StartNativeFixtureRole(
            executablePath,
            workingDirectory,
            argument);
        try
        {
            using CancellationTokenSource timeout = new(TestTimeout);
            await process.WaitForExitAsync(timeout.Token)
                .ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (Exception exception)
        {
            Exception primary = exception is OperationCanceledException
                ? new TimeoutException(
                    "The exact native fixture role did not exit within its test bound.",
                    exception)
                : exception;
            Exception? cleanupFailure = await KillAndWaitForNativeFixtureAsync(
                process).ConfigureAwait(false);
            if (cleanupFailure is not null)
            {
                throw new AggregateException(primary, cleanupFailure);
            }

            throw primary;
        }
    }

    private static async Task AssertNativeFixtureRoleBlocksAsync(
        string executablePath,
        string workingDirectory)
    {
        using Process process = StartNativeFixtureRole(
            executablePath,
            workingDirectory,
            "--native-block");
        Exception? primary = null;
        try
        {
            using CancellationTokenSource observation = new(
                TimeSpan.FromMilliseconds(250));
            await process.WaitForExitAsync(observation.Token)
                .ConfigureAwait(false);
            primary = new InvalidOperationException(
                $"The native Block role exited unexpectedly with code {process.ExitCode}.");
        }
        catch (OperationCanceledException)
        {
            if (process.HasExited)
            {
                primary = new InvalidOperationException(
                    "The native Block role exited at its observation boundary.");
            }
        }
        catch (Exception exception)
        {
            primary = exception;
        }

        Exception? cleanupFailure = await KillAndWaitForNativeFixtureAsync(
            process).ConfigureAwait(false);
        if (primary is not null && cleanupFailure is not null)
        {
            throw new AggregateException(primary, cleanupFailure);
        }

        if (primary is not null)
        {
            throw primary;
        }

        if (cleanupFailure is not null)
        {
            throw cleanupFailure;
        }
    }

    private static Process StartNativeFixtureRole(
        string executablePath,
        string workingDirectory,
        string argument)
    {
        ProcessStartInfo start = new()
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
        };
        start.Environment.Clear();
        start.Environment["SystemRoot"] = @"C:\Windows";
        start.Environment["WINDIR"] = @"C:\Windows";
        start.Environment["PATH"] = @"C:\Windows\System32";
        start.Environment["TEMP"] = workingDirectory;
        start.Environment["TMP"] = workingDirectory;
        start.ArgumentList.Add(argument);

        Assert(string.Equals(start.FileName, executablePath,
                StringComparison.Ordinal),
            "the native role executable path changed");
        Assert(string.Equals(start.WorkingDirectory, workingDirectory,
                StringComparison.Ordinal),
            "the native role working directory changed");
        Assert(!start.UseShellExecute && !start.CreateNoWindow &&
                !start.RedirectStandardInput &&
                !start.RedirectStandardOutput &&
                !start.RedirectStandardError,
            "the native role process configuration crossed its exact no-shell I/O boundary");
        Assert(start.ArgumentList.Count == 1 &&
                string.Equals(start.ArgumentList[0], argument,
                    StringComparison.Ordinal) &&
                string.IsNullOrEmpty(start.Arguments),
            "the native role argument list is not exact");
        Assert(start.Environment.Count == 5,
            "the native role environment contains an unexpected variable");
        AssertNativeFixtureEnvironment(start, "SystemRoot", @"C:\Windows");
        AssertNativeFixtureEnvironment(start, "WINDIR", @"C:\Windows");
        AssertNativeFixtureEnvironment(
            start,
            "PATH",
            @"C:\Windows\System32");
        AssertNativeFixtureEnvironment(start, "TEMP", workingDirectory);
        AssertNativeFixtureEnvironment(start, "TMP", workingDirectory);

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException(
                "Starting the native fixture returned no retained process.");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Starting the exact native fixture role failed.",
                exception);
        }

        return process;
    }

    private static async Task<Exception?> KillAndWaitForNativeFixtureAsync(
        Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // The retained exact process exited between the check and
                    // the kill. The exact-handle wait below remains required.
                }
            }

            using CancellationTokenSource cleanupTimeout = new(TestTimeout);
            await process.WaitForExitAsync(cleanupTimeout.Token)
                .ConfigureAwait(false);
            if (!process.HasExited)
            {
                throw new InvalidOperationException(
                    "The killed native fixture process did not report exact exit.");
            }

            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static void AssertNativeFixtureEnvironment(
        ProcessStartInfo start,
        string name,
        string expectedValue)
    {
        Assert(start.Environment.TryGetValue(name, out string? actualValue) &&
                string.Equals(actualValue, expectedValue, StringComparison.Ordinal),
            $"the native role {name} environment value is not exact");
    }

    private static void AssertNativePeMutationRejected(
        NativeFixtureInputs inputs,
        Action<byte[]> mutation)
    {
        byte[] mutated = (byte[])inputs.Image.Clone();
        byte[]? digest = null;
        try
        {
            mutation(mutated);
            RewriteNativePeChecksum(mutated);
            digest = SHA256.HashData(mutated);
            AssertThrows<FormatException>(() =>
            {
                using NativeFixturePeAudit ignored = NativeFixturePeAudit.Open(
                    mutated,
                    inputs.Manifest,
                    digest);
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mutated);
            WipeNativeTestBytes(digest);
        }
    }

    private static void RewriteNativePeChecksum(byte[] image)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(NativePeChecksumOffset),
            0);
        ulong sum = 0;
        for (int offset = 0; offset < image.Length; offset += 2)
        {
            if (offset == NativePeChecksumOffset ||
                offset == NativePeChecksumOffset + 2)
            {
                continue;
            }

            uint word = image[offset];
            if (offset + 1 < image.Length)
            {
                word |= checked((uint)image[offset + 1] << 8);
            }

            sum = checked(sum + word);
            sum = (sum & 0xffffUL) + (sum >> 16);
        }

        sum = (sum & 0xffffUL) + (sum >> 16);
        sum = (sum & 0xffffUL) + (sum >> 16);
        uint checksum = checked((uint)(sum + checked((uint)image.Length)));
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(NativePeChecksumOffset),
            checksum);
    }

    private static NativeFixtureInputs ReadNativeFixtureInputs()
    {
        string moduleRoot = FindWindowsBootstrapModuleRoot();
        string executablePath = Path.Combine(
            moduleRoot,
            "build",
            "native",
            "HrcJobObserver.NativeFixture.exe");
        string manifestPath = Path.Combine(
            moduleRoot,
            "native",
            "HrcJobObserver.NativeFixture.manifest");
        return new NativeFixtureInputs(
            File.ReadAllBytes(executablePath),
            File.ReadAllBytes(manifestPath));
    }

    private static string FindWindowsBootstrapModuleRoot()
    {
        string nested = Path.Combine(
            Environment.CurrentDirectory,
            "src",
            "HrcJobObserver",
            "windows-bootstrap");
        if (File.Exists(Path.Combine(
                nested,
                "HrcJobObserver.WindowsBootstrap.TestHarness.csproj")))
        {
            return nested;
        }

        DirectoryInfo? cursor = new(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            if (File.Exists(Path.Combine(
                    cursor.FullName,
                    "HrcJobObserver.WindowsBootstrap.TestHarness.csproj")))
            {
                return cursor.FullName;
            }

            cursor = cursor.Parent;
        }

        throw new DirectoryNotFoundException(
            "The Windows bootstrap module root could not be resolved for the native fixture test.");
    }

    private static void WipeNativeTestBytes(byte[]? value)
    {
        if (value is not null)
        {
            CryptographicOperations.ZeroMemory(value);
        }
    }

    private sealed class NativeFixtureInputs : IDisposable
    {
        internal NativeFixtureInputs(byte[] image, byte[] manifest)
        {
            Image = image;
            Manifest = manifest;
        }

        internal byte[] Image { get; }

        internal byte[] Manifest { get; }

        public void Dispose()
        {
            CryptographicOperations.ZeroMemory(Image);
            CryptographicOperations.ZeroMemory(Manifest);
        }
    }
}
