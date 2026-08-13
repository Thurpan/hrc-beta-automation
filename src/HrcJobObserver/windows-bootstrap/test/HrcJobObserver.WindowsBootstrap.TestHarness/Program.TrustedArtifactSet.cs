using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class Program
{
    private static Task TestTrustedArtifactSetRoundTrip()
    {
        using FilePublicationTestDirectory directory = new();
        string executablePath = Path.Combine(directory.Path, "observer.exe");
        string assemblyPath = Path.Combine(directory.Path, "observer.dll");
        string configPath = Path.Combine(
            directory.Path,
            "observer.runtimeconfig.json");
        string unexpectedPath = Path.Combine(directory.Path, "late-load.dll");
        byte[] executable = { 0x4d, 0x5a, 0x01 };
        byte[] assembly = { 0x61, 0x73, 0x73, 0x65, 0x6d, 0x62, 0x6c, 0x79 };
        byte[] config = { 0x7b, 0x7d };
        byte[] changed = { 0x63, 0x68, 0x61, 0x6e, 0x67, 0x65, 0x64 };
        byte[] unexpected = { 0x6c, 0x61, 0x74, 0x65 };
        TrustedArtifactSetLease? lease = null;
        byte[]? manifest = null;
        try
        {
            File.WriteAllBytes(executablePath, executable);
            File.WriteAllBytes(assemblyPath, assembly);
            File.WriteAllBytes(configPath, config);
            TrustedArtifactExpectation[] expected =
            {
                ArtifactExpectation("observer.exe", executable),
                ArtifactExpectation("observer.dll", assembly),
                ArtifactExpectation("observer.runtimeconfig.json", config),
            };

            lease = OpenArtifactSet(directory.Path, "observer.exe", expected);
            AssertEqual(directory.Path, lease.ApplicationDirectory,
                "artifact set application directory");
            AssertEqual("observer.exe", lease.ExecutableRelativeFileName,
                "artifact set executable filename");
            AssertEqual(executablePath, lease.ExecutablePath,
                "artifact set executable path");
            AssertEqual(3, lease.Count, "artifact set member count");
            manifest = lease.CopyManifestSha256();
            AssertEqual(32, manifest.Length, "artifact set manifest length");
            lease.RevalidateExactSet(NewArtifactDeadline(), CancellationToken.None);

            AssertThrowsAny(
                () => File.WriteAllBytes(assemblyPath, changed),
                typeof(IOException),
                typeof(UnauthorizedAccessException));
            AssertThrowsAny(
                () => File.WriteAllBytes(configPath, changed),
                typeof(IOException),
                typeof(UnauthorizedAccessException));

            File.WriteAllBytes(unexpectedPath, unexpected);
            Assert(File.Exists(unexpectedPath),
                "the retained directory must not be claimed to block new children");
            AssertThrows<SecurityException>(() => lease.RevalidateExactSet(
                NewArtifactDeadline(),
                CancellationToken.None));
            File.Delete(unexpectedPath);
            lease.RevalidateExactSet(NewArtifactDeadline(), CancellationToken.None);

            lease.Dispose();
            AssertThrows<ObjectDisposedException>(() =>
                lease.RevalidateExactSet(
                    NewArtifactDeadline(),
                    CancellationToken.None));
            AssertThrows<ObjectDisposedException>(() =>
            {
                _ = lease.CopyManifestSha256();
            });
            lease = null;

            File.WriteAllBytes(assemblyPath, changed);
            File.WriteAllBytes(unexpectedPath, unexpected);
            Assert(File.ReadAllBytes(assemblyPath).AsSpan().SequenceEqual(changed) &&
                File.Exists(unexpectedPath),
                "member mutation and child creation must be possible after disposal");
            return Task.CompletedTask;
        }
        finally
        {
            lease?.Dispose();
            CryptographicOperations.ZeroMemory(executable);
            CryptographicOperations.ZeroMemory(assembly);
            CryptographicOperations.ZeroMemory(config);
            CryptographicOperations.ZeroMemory(changed);
            CryptographicOperations.ZeroMemory(unexpected);
            if (manifest is not null)
            {
                CryptographicOperations.ZeroMemory(manifest);
            }
        }
    }

    private static Task TestTrustedArtifactSetEntryGuards()
    {
        byte[] executable = { 0x4d, 0x5a };
        byte[] assembly = { 0x64, 0x6c, 0x6c };
        byte[] emptyDigest = SHA256.HashData(Array.Empty<byte>());
        try
        {
            using (FilePublicationTestDirectory missing = new())
            {
                File.WriteAllBytes(
                    Path.Combine(missing.Path, "role.exe"),
                    executable);
                TrustedArtifactExpectation[] expected =
                {
                    ArtifactExpectation("role.exe", executable),
                    ArtifactExpectation("role.dll", assembly),
                };
                AssertThrows<SecurityException>(() =>
                {
                    using TrustedArtifactSetLease ignored = OpenArtifactSet(
                        missing.Path,
                        "role.exe",
                        expected);
                });
            }

            using (FilePublicationTestDirectory extras = new())
            {
                string executablePath = Path.Combine(extras.Path, "role.exe");
                File.WriteAllBytes(executablePath, executable);
                TrustedArtifactExpectation[] expected =
                {
                    ArtifactExpectation("role.exe", executable),
                };

                string pdb = Path.Combine(extras.Path, "role.pdb");
                File.WriteAllBytes(pdb, assembly);
                AssertThrows<SecurityException>(() =>
                {
                    using TrustedArtifactSetLease ignored = OpenArtifactSet(
                        extras.Path,
                        "role.exe",
                        expected);
                });
                File.Delete(pdb);

                string runtimeDevelopment = Path.Combine(
                    extras.Path,
                    "role.runtimeconfig.dev.json");
                File.WriteAllBytes(runtimeDevelopment, assembly);
                AssertThrows<SecurityException>(() =>
                {
                    using TrustedArtifactSetLease ignored = OpenArtifactSet(
                        extras.Path,
                        "role.exe",
                        expected);
                });
                File.Delete(runtimeDevelopment);

                string subdirectory = Path.Combine(extras.Path, "cache");
                CreateProtectedTestDirectory(
                    subdirectory,
                    extras.OwnerSid,
                    includeSystem: true);
                AssertThrows<SecurityException>(() =>
                {
                    using TrustedArtifactSetLease ignored = OpenArtifactSet(
                        extras.Path,
                        "role.exe",
                        expected);
                });
                Directory.Delete(subdirectory);
            }

            using (FilePublicationTestDirectory caseMismatch = new())
            {
                File.WriteAllBytes(
                    Path.Combine(caseMismatch.Path, "Role.exe"),
                    executable);
                TrustedArtifactExpectation[] expected =
                {
                    ArtifactExpectation("role.exe", executable),
                };
                AssertThrows<SecurityException>(() =>
                {
                    using TrustedArtifactSetLease ignored = OpenArtifactSet(
                        caseMismatch.Path,
                        "role.exe",
                        expected);
                });
            }

            using (FilePublicationTestDirectory reparse = new())
            using (FilePublicationTestDirectory target = new())
            {
                File.WriteAllBytes(
                    Path.Combine(reparse.Path, "role.exe"),
                    executable);
                string junction = Path.Combine(reparse.Path, "role.dll");
                CreateProtectedTestDirectory(
                    junction,
                    reparse.OwnerSid,
                    includeSystem: true);
                CreateDirectoryJunction(junction, target.Path);
                TrustedArtifactExpectation[] expected =
                {
                    ArtifactExpectation("role.exe", executable),
                    new TrustedArtifactExpectation(
                        "role.dll",
                        0,
                        emptyDigest),
                };
                AssertThrows<SecurityException>(() =>
                {
                    using TrustedArtifactSetLease ignored = OpenArtifactSet(
                        reparse.Path,
                        "role.exe",
                        expected);
                });
            }

            using (FilePublicationTestDirectory duplicate = new())
            {
                File.WriteAllBytes(
                    Path.Combine(duplicate.Path, "role.exe"),
                    executable);
                TrustedArtifactExpectation exact =
                    ArtifactExpectation("role.exe", executable);
                TrustedArtifactExpectation caseCollision =
                    ArtifactExpectation("Role.exe", executable);
                AssertThrows<ArgumentException>(() =>
                {
                    using TrustedArtifactSetLease ignored = OpenArtifactSet(
                        duplicate.Path,
                        "role.exe",
                        new[] { exact, exact });
                });
                AssertThrows<ArgumentException>(() =>
                {
                    using TrustedArtifactSetLease ignored = OpenArtifactSet(
                        duplicate.Path,
                        "role.exe",
                        new[] { exact, caseCollision });
                });
            }

            AssertThrows<ArgumentException>(() =>
                _ = new TrustedArtifactExpectation(
                    @"..\role.dll",
                    0,
                    emptyDigest));
            AssertThrows<ArgumentException>(() =>
                _ = new TrustedArtifactExpectation(
                    @"sub\role.dll",
                    0,
                    emptyDigest));
            AssertThrows<ArgumentException>(() =>
                _ = new TrustedArtifactExpectation(
                    "role.dll:stream",
                    0,
                    emptyDigest));
            AssertThrows<ArgumentException>(() =>
                _ = new TrustedArtifactExpectation(
                    "r\u00f4le.dll",
                    0,
                    emptyDigest));
            AssertThrows<ArgumentException>(() =>
                _ = new TrustedArtifactExpectation(
                    "CON.txt",
                    0,
                    emptyDigest));
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(executable);
            CryptographicOperations.ZeroMemory(assembly);
            CryptographicOperations.ZeroMemory(emptyDigest);
        }
    }

    private static Task TestTrustedArtifactSetIdentityMismatches()
    {
        using FilePublicationTestDirectory directory = new();
        string executablePath = Path.Combine(directory.Path, "role.exe");
        string assemblyPath = Path.Combine(directory.Path, "role.dll");
        byte[] executable = { 0x4d, 0x5a, 0x10, 0x11 };
        byte[] assembly = { 0x64, 0x6c, 0x6c, 0x10, 0x11 };
        byte[] executableDigest = SHA256.HashData(executable);
        byte[] assemblyDigest = SHA256.HashData(assembly);
        byte[] wrongExecutableDigest = (byte[])executableDigest.Clone();
        byte[] wrongAssemblyDigest = (byte[])assemblyDigest.Clone();
        try
        {
            wrongExecutableDigest[0] ^= 0xff;
            wrongAssemblyDigest[0] ^= 0xff;
            File.WriteAllBytes(executablePath, executable);
            File.WriteAllBytes(assemblyPath, assembly);

            AssertArtifactSetIdentityFailure(
                directory.Path,
                new TrustedArtifactExpectation(
                    "role.exe",
                    executable.Length + 1,
                    executableDigest),
                new TrustedArtifactExpectation(
                    "role.dll",
                    assembly.Length,
                    assemblyDigest));
            AssertArtifactSetIdentityFailure(
                directory.Path,
                new TrustedArtifactExpectation(
                    "role.exe",
                    executable.Length,
                    executableDigest),
                new TrustedArtifactExpectation(
                    "role.dll",
                    assembly.Length + 1,
                    assemblyDigest));
            File.WriteAllBytes(executablePath, executable);
            Assert(File.ReadAllBytes(executablePath)
                    .AsSpan()
                    .SequenceEqual(executable),
                "a later-member failure must release every earlier retained member");
            AssertArtifactSetIdentityFailure(
                directory.Path,
                new TrustedArtifactExpectation(
                    "role.exe",
                    executable.Length,
                    wrongExecutableDigest),
                new TrustedArtifactExpectation(
                    "role.dll",
                    assembly.Length,
                    assemblyDigest));
            AssertArtifactSetIdentityFailure(
                directory.Path,
                new TrustedArtifactExpectation(
                    "role.exe",
                    executable.Length,
                    executableDigest),
                new TrustedArtifactExpectation(
                    "role.dll",
                    assembly.Length,
                    wrongAssemblyDigest));

            File.WriteAllBytes(executablePath, executable);
            File.WriteAllBytes(assemblyPath, assembly);
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(executable);
            CryptographicOperations.ZeroMemory(assembly);
            CryptographicOperations.ZeroMemory(executableDigest);
            CryptographicOperations.ZeroMemory(assemblyDigest);
            CryptographicOperations.ZeroMemory(wrongExecutableDigest);
            CryptographicOperations.ZeroMemory(wrongAssemblyDigest);
        }
    }

    private static Task TestTrustedArtifactSetManifest()
    {
        using FilePublicationTestDirectory firstDirectory = new();
        using FilePublicationTestDirectory caseDirectory = new();
        byte[] executable = { 0x4d, 0x5a, 0x20 };
        byte[] assembly = { 0x64, 0x6c, 0x6c, 0x20 };
        byte[] changedAssembly = { 0x64, 0x6c, 0x6c, 0x21 };
        byte[]? firstManifest = null;
        byte[]? reorderedManifest = null;
        byte[]? caseManifest = null;
        byte[]? changedContentManifest = null;
        byte[]? changedExecutableManifest = null;
        try
        {
            File.WriteAllBytes(
                Path.Combine(firstDirectory.Path, "Role.exe"),
                executable);
            File.WriteAllBytes(
                Path.Combine(firstDirectory.Path, "Role.dll"),
                assembly);
            TrustedArtifactExpectation executableExpectation =
                ArtifactExpectation("Role.exe", executable);
            TrustedArtifactExpectation assemblyExpectation =
                ArtifactExpectation("Role.dll", assembly);
            using (TrustedArtifactSetLease first = OpenArtifactSet(
                firstDirectory.Path,
                "Role.exe",
                new[] { executableExpectation, assemblyExpectation }))
            {
                firstManifest = first.CopyManifestSha256();
                byte[] independent = first.CopyManifestSha256();
                try
                {
                    independent[0] ^= 0xff;
                    byte[] secondIndependent = first.CopyManifestSha256();
                    try
                    {
                        Assert(secondIndependent.AsSpan().SequenceEqual(firstManifest),
                            "manifest callers must receive independent copies");
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(secondIndependent);
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(independent);
                }
            }

            using (TrustedArtifactSetLease reordered = OpenArtifactSet(
                firstDirectory.Path,
                "Role.exe",
                new[] { assemblyExpectation, executableExpectation }))
            {
                reorderedManifest = reordered.CopyManifestSha256();
            }

            Assert(firstManifest.AsSpan().SequenceEqual(reorderedManifest),
                "manifest identity must not depend on expectation input order");

            File.WriteAllBytes(
                Path.Combine(firstDirectory.Path, "Role.dll"),
                changedAssembly);
            using (TrustedArtifactSetLease changedContent = OpenArtifactSet(
                firstDirectory.Path,
                "Role.exe",
                new[]
                {
                    executableExpectation,
                    ArtifactExpectation("Role.dll", changedAssembly),
                }))
            {
                changedContentManifest = changedContent.CopyManifestSha256();
            }

            Assert(!firstManifest.AsSpan().SequenceEqual(changedContentManifest),
                "manifest identity must bind every member length and digest");
            File.WriteAllBytes(
                Path.Combine(firstDirectory.Path, "Role.dll"),
                assembly);

            using (TrustedArtifactSetLease changedExecutable = OpenArtifactSet(
                firstDirectory.Path,
                "Role.dll",
                new[] { executableExpectation, assemblyExpectation }))
            {
                changedExecutableManifest =
                    changedExecutable.CopyManifestSha256();
            }

            Assert(!firstManifest.AsSpan().SequenceEqual(changedExecutableManifest),
                "manifest identity must bind the designated executable filename");

            File.WriteAllBytes(
                Path.Combine(caseDirectory.Path, "role.exe"),
                executable);
            File.WriteAllBytes(
                Path.Combine(caseDirectory.Path, "Role.dll"),
                assembly);
            using (TrustedArtifactSetLease caseChanged = OpenArtifactSet(
                caseDirectory.Path,
                "role.exe",
                new[]
                {
                    ArtifactExpectation("role.exe", executable),
                    ArtifactExpectation("Role.dll", assembly),
                }))
            {
                caseManifest = caseChanged.CopyManifestSha256();
            }

            Assert(!firstManifest.AsSpan().SequenceEqual(caseManifest),
                "manifest identity must bind exact filename casing");
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(executable);
            CryptographicOperations.ZeroMemory(assembly);
            CryptographicOperations.ZeroMemory(changedAssembly);
            if (firstManifest is not null)
            {
                CryptographicOperations.ZeroMemory(firstManifest);
            }

            if (reorderedManifest is not null)
            {
                CryptographicOperations.ZeroMemory(reorderedManifest);
            }

            if (caseManifest is not null)
            {
                CryptographicOperations.ZeroMemory(caseManifest);
            }

            if (changedContentManifest is not null)
            {
                CryptographicOperations.ZeroMemory(changedContentManifest);
            }

            if (changedExecutableManifest is not null)
            {
                CryptographicOperations.ZeroMemory(changedExecutableManifest);
            }
        }
    }

    private static Task TestTrustedArtifactSetBounds()
    {
        using FilePublicationTestDirectory directory = new();
        byte[] executable = { 0x4d, 0x5a, 0x30 };
        byte[] assembly = { 0x64, 0x6c, 0x6c, 0x30 };
        try
        {
            File.WriteAllBytes(
                Path.Combine(directory.Path, "role.exe"),
                executable);
            File.WriteAllBytes(
                Path.Combine(directory.Path, "role.dll"),
                assembly);
            TrustedArtifactExpectation first =
                ArtifactExpectation("role.exe", executable);
            TrustedArtifactExpectation second =
                ArtifactExpectation("role.dll", assembly);

            using CancellationTokenSource alreadyCancelled = new();
            alreadyCancelled.Cancel();
            AssertThrows<OperationCanceledException>(() =>
            {
                using TrustedArtifactSetLease ignored =
                    TrustedArtifactSetLease.Open(
                        directory.Path,
                        "role.exe",
                        new[] { first, second },
                        NewArtifactDeadline(),
                        alreadyCancelled.Token);
            });

            ManualTimeProvider expiredClock = new(CanonicalTestUtcNow());
            MonotonicDeadline expired = MonotonicDeadline.Start(
                expiredClock,
                TestTimeout);
            expiredClock.Advance(TestTimeout);
            AssertThrows<TimeoutException>(() =>
            {
                using TrustedArtifactSetLease ignored =
                    TrustedArtifactSetLease.Open(
                        directory.Path,
                        "role.exe",
                        new[] { first, second },
                        expired,
                        CancellationToken.None);
            });

            ManualTimeProvider enumerationClock = new(CanonicalTestUtcNow());
            MonotonicDeadline enumerationDeadline = MonotonicDeadline.Start(
                enumerationClock,
                TestTimeout);
            IEnumerable<TrustedArtifactExpectation> deadlineSequence =
                BoundExpectationSequence(
                    first,
                    () => enumerationClock.Advance(TestTimeout),
                    second);
            AssertThrows<TimeoutException>(() =>
            {
                using TrustedArtifactSetLease ignored =
                    TrustedArtifactSetLease.Open(
                        directory.Path,
                        "role.exe",
                        deadlineSequence,
                        enumerationDeadline,
                        CancellationToken.None);
            });

            using CancellationTokenSource midEnumeration = new();
            IEnumerable<TrustedArtifactExpectation> cancellationSequence =
                BoundExpectationSequence(
                    first,
                    midEnumeration.Cancel,
                    second);
            AssertThrows<OperationCanceledException>(() =>
            {
                using TrustedArtifactSetLease ignored =
                    TrustedArtifactSetLease.Open(
                        directory.Path,
                        "role.exe",
                        cancellationSequence,
                        NewArtifactDeadline(),
                        midEnumeration.Token);
                });

            List<TrustedArtifactExpectation> tooMany = new(
                TrustedArtifactSetLease.MaximumArtifactCount + 1)
            {
                first,
            };
            byte[] emptyDigest = SHA256.HashData(Array.Empty<byte>());
            try
            {
                for (int index = 1;
                    index <= TrustedArtifactSetLease.MaximumArtifactCount;
                    index++)
                {
                    tooMany.Add(new TrustedArtifactExpectation(
                        $"file-{index:D3}.bin",
                        0,
                        emptyDigest));
                }

                AssertThrows<ArgumentException>(() =>
                {
                    using TrustedArtifactSetLease ignored =
                        TrustedArtifactSetLease.Open(
                            directory.Path,
                            "role.exe",
                            tooMany,
                            NewArtifactDeadline(),
                            CancellationToken.None);
                });
            }
            finally
            {
                CryptographicOperations.ZeroMemory(emptyDigest);
            }

            using TrustedArtifactSetLease lease = OpenArtifactSet(
                directory.Path,
                "role.exe",
                new[] { first, second });
            AssertThrows<TimeoutException>(() => lease.RevalidateExactSet(
                expired,
                CancellationToken.None));
            AssertThrows<OperationCanceledException>(() =>
                lease.RevalidateExactSet(
                    NewArtifactDeadline(),
                    alreadyCancelled.Token));
            lease.RevalidateExactSet(
                NewArtifactDeadline(),
                CancellationToken.None);
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(executable);
            CryptographicOperations.ZeroMemory(assembly);
        }
    }

    private static Task TestTrustedArtifactSetRootGuards()
    {
        byte[] executable = { 0x4d, 0x5a, 0x40 };
        try
        {
            using (FilePublicationTestDirectory outer = new())
            {
                string wrongDacl = Path.Combine(outer.Path, "wrong-dacl");
                CreateProtectedTestDirectory(
                    wrongDacl,
                    outer.OwnerSid,
                    includeSystem: false);
                File.WriteAllBytes(
                    Path.Combine(wrongDacl, "role.exe"),
                    executable);
                TrustedArtifactExpectation[] expected =
                {
                    ArtifactExpectation("role.exe", executable),
                };
                AssertThrows<SecurityException>(() =>
                {
                    using TrustedArtifactSetLease ignored = OpenArtifactSet(
                        wrongDacl,
                        "role.exe",
                        expected);
                });
            }

            using (FilePublicationTestDirectory outer = new())
            using (FilePublicationTestDirectory target = new())
            {
                File.WriteAllBytes(
                    Path.Combine(target.Path, "role.exe"),
                    executable);
                string junction = Path.Combine(outer.Path, "app-junction");
                CreateProtectedTestDirectory(
                    junction,
                    outer.OwnerSid,
                    includeSystem: true);
                CreateDirectoryJunction(junction, target.Path);
                TrustedArtifactExpectation[] expected =
                {
                    ArtifactExpectation("role.exe", executable),
                };
                AssertThrows<SecurityException>(() =>
                {
                    using TrustedArtifactSetLease ignored = OpenArtifactSet(
                        junction,
                        "role.exe",
                        expected);
                });
            }

            using (FilePublicationTestDirectory canonical = new())
            {
                File.WriteAllBytes(
                    Path.Combine(canonical.Path, "role.exe"),
                    executable);
                TrustedArtifactExpectation[] expected =
                {
                    ArtifactExpectation("role.exe", executable),
                };
                AssertThrows<ArgumentException>(() =>
                {
                    using TrustedArtifactSetLease ignored = OpenArtifactSet(
                        canonical.Path + Path.DirectorySeparatorChar,
                        "role.exe",
                        expected);
                });
            }

            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(executable);
        }
    }

    private static TrustedArtifactExpectation ArtifactExpectation(
        string relativeFileName,
        ReadOnlySpan<byte> content)
    {
        byte[] digest = SHA256.HashData(content);
        try
        {
            return new TrustedArtifactExpectation(
                relativeFileName,
                content.Length,
                digest);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static TrustedArtifactSetLease OpenArtifactSet(
        string applicationDirectory,
        string executableRelativeFileName,
        IEnumerable<TrustedArtifactExpectation> expectations)
    {
        return TrustedArtifactSetLease.Open(
            applicationDirectory,
            executableRelativeFileName,
            expectations,
            NewArtifactDeadline(),
            CancellationToken.None);
    }

    private static MonotonicDeadline NewArtifactDeadline()
    {
        return MonotonicDeadline.Start(TimeProvider.System, TestTimeout);
    }

    private static void AssertArtifactSetIdentityFailure(
        string applicationDirectory,
        TrustedArtifactExpectation executable,
        TrustedArtifactExpectation assembly)
    {
        AssertThrows<SecurityException>(() =>
        {
            using TrustedArtifactSetLease ignored = OpenArtifactSet(
                applicationDirectory,
                "role.exe",
                new[] { executable, assembly });
        });
    }

    private static IEnumerable<TrustedArtifactExpectation>
        BoundExpectationSequence(
            TrustedArtifactExpectation first,
            Action beforeSecond,
            TrustedArtifactExpectation second)
    {
        yield return first;
        beforeSecond();
        yield return second;
    }
}
