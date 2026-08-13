using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Immutable expected identity for one app-local artifact. The relative name
/// is one canonical Windows filename, not a path.
/// </summary>
internal sealed class TrustedArtifactExpectation
{
    private const int Sha256Length = 32;
    private readonly byte[] sha256Digest;

    internal TrustedArtifactExpectation(
        string relativeFileName,
        long length,
        ReadOnlySpan<byte> sha256Digest)
    {
        RelativeFileName = TrustedArtifactSetLease.ValidateRelativeFileName(
            relativeFileName,
            nameof(relativeFileName));
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (sha256Digest.Length != Sha256Length)
        {
            throw new ArgumentException(
                "The expected SHA-256 digest must contain exactly 32 bytes.",
                nameof(sha256Digest));
        }

        Length = length;
        this.sha256Digest = sha256Digest.ToArray();
    }

    internal string RelativeFileName { get; }

    internal long Length { get; }

    internal byte[] CopySha256Digest()
    {
        return (byte[])sha256Digest.Clone();
    }
}

/// <summary>
/// Owns an exact protected app-local artifact snapshot. Every expected file is
/// pinned against new data-write and delete access. The protected directory is
/// retained against rename and deletion, but its owner can still create a new
/// child. Consequently this lease detects, rather than prevents, extra-entry
/// races. A future launcher must call <see cref="RevalidateExactSet"/>
/// immediately before process creation and still treat launch atomicity as a
/// separate boundary. The domain-separated digest identifies only the
/// caller-supplied set; it neither authenticates release provenance nor binds
/// or selects a shared .NET runtime.
/// </summary>
internal sealed class TrustedArtifactSetLease : IDisposable
{
    internal const int MaximumArtifactCount = 128;
    private const int ManifestSha256Length = 32;
    private static readonly byte[] ManifestDomain = Encoding.ASCII.GetBytes(
        "HRC-BETA-OBSERVER-PROTECTED-ARTIFACT-SET-V1\0");

    private readonly object gate = new();
    private readonly GuardedDescriptorDirectory directoryLease;
    private readonly ArtifactSetMember[] members;
    private readonly Dictionary<string, string> expectedNames;
    private readonly byte[] manifestSha256;
    private bool disposed;

    private TrustedArtifactSetLease(
        GuardedDescriptorDirectory directoryLease,
        string applicationDirectory,
        string executableRelativeFileName,
        ArtifactSetMember[] members,
        Dictionary<string, string> expectedNames,
        byte[] manifestSha256)
    {
        this.directoryLease = directoryLease;
        ApplicationDirectory = applicationDirectory;
        ExecutableRelativeFileName = executableRelativeFileName;
        ExecutablePath = Path.Combine(
            applicationDirectory,
            executableRelativeFileName);
        this.members = members;
        this.expectedNames = expectedNames;
        this.manifestSha256 = manifestSha256;
    }

    internal string ApplicationDirectory { get; }

    internal string ExecutableRelativeFileName { get; }

    internal string ExecutablePath { get; }

    internal int Count => members.Length;

    internal static TrustedArtifactSetLease Open(
        string exactApplicationDirectory,
        string executableRelativeFileName,
        IEnumerable<TrustedArtifactExpectation> expectations,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        string applicationDirectory = ValidateCanonicalApplicationDirectory(
            exactApplicationDirectory);
        string executableName = ValidateRelativeFileName(
            executableRelativeFileName,
            nameof(executableRelativeFileName));
        TrustedArtifactExpectation[] expected = MaterializeExpectations(
            expectations,
            deadline,
            cancellationToken);
        Dictionary<string, string> expectedNames = BuildExpectedNameMap(expected);
        if (!expectedNames.TryGetValue(executableName, out string? actualName) ||
            !string.Equals(actualName, executableName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The executable filename must exactly name one expected artifact.",
                nameof(executableRelativeFileName));
        }

        TrustedArtifactExpectation[] sorted = expected
            .OrderBy(
                static item => item.RelativeFileName,
                StringComparer.Ordinal)
            .ToArray();
        GuardedDescriptorDirectory? directory = null;
        List<ArtifactSetMember> retained = new(sorted.Length);
        byte[]? manifest = null;
        try
        {
            CheckOperation(deadline, cancellationToken);
            using (ProcessIdentityLease current = ProcessIdentityLease.Capture(
                       checked((uint)Environment.ProcessId)))
            {
                CheckOperation(deadline, cancellationToken);
                directory = GuardedDescriptorDirectory.Open(
                    applicationDirectory,
                    current.UserSid,
                    testHook: null);
            }

            CheckOperation(deadline, cancellationToken);
            directory.RevalidateDirectory();
            RequireExactEntries(
                applicationDirectory,
                expectedNames,
                deadline,
                cancellationToken);

            foreach (TrustedArtifactExpectation expectation in sorted)
            {
                CheckOperation(deadline, cancellationToken);
                byte[] digest = expectation.CopySha256Digest();
                try
                {
                    string path = Path.Combine(
                        applicationDirectory,
                        expectation.RelativeFileName);
                    TrustedArtifactLease artifact = TrustedArtifactIdentity.Open(
                        path,
                        expectation.Length,
                        digest,
                        deadline,
                        cancellationToken);
                    retained.Add(new ArtifactSetMember(
                        expectation.RelativeFileName,
                        artifact));
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(digest);
                }

                CheckOperation(deadline, cancellationToken);
            }

            RequireExactEntries(
                applicationDirectory,
                expectedNames,
                deadline,
                cancellationToken);
            directory.RevalidateDirectory();
            CheckOperation(deadline, cancellationToken);
            manifest = ComputeManifestSha256(
                executableName,
                retained,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);

            ArtifactSetMember[] retainedArray = retained.ToArray();
            TrustedArtifactSetLease result = new(
                directory,
                applicationDirectory,
                executableName,
                retainedArray,
                expectedNames,
                manifest);
            directory = null;
            retained.Clear();
            manifest = null;
            return result;
        }
        finally
        {
            for (int index = retained.Count - 1; index >= 0; index--)
            {
                retained[index].Artifact.Dispose();
            }

            directory?.Dispose();
            if (manifest is not null)
            {
                CryptographicOperations.ZeroMemory(manifest);
            }
        }
    }

    internal byte[] CopyManifestSha256()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return (byte[])manifestSha256.Clone();
        }
    }

    internal byte[] CopyExactExecutableBytes(
        int maximumLength,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            ArtifactSetMember executable = members.Single(member =>
                string.Equals(
                    member.RelativeFileName,
                    ExecutableRelativeFileName,
                    StringComparison.Ordinal));
            return executable.Artifact.CopyExactBytes(
                maximumLength,
                deadline,
                cancellationToken);
        }
    }

    internal TrustedArtifactLaunchNamespaceLease
        OpenExecutableLaunchNamespaceLease(
            MonotonicDeadline deadline,
            CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            RevalidateExactSet(deadline, cancellationToken);
            ArtifactSetMember executable = members.Single(member =>
                string.Equals(
                    member.RelativeFileName,
                    ExecutableRelativeFileName,
                    StringComparison.Ordinal));
            TrustedArtifactLaunchNamespaceLease? launchNamespace = null;
            try
            {
                launchNamespace = executable.Artifact.OpenLaunchNamespaceLease(
                    deadline,
                    cancellationToken);
                RevalidateExactSet(deadline, cancellationToken);
                CheckOperation(deadline, cancellationToken);
                TrustedArtifactLaunchNamespaceLease result = launchNamespace;
                launchNamespace = null;
                return result;
            }
            finally
            {
                launchNamespace?.Dispose();
            }
        }
    }

    internal void RevalidateExactSet(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            directoryLease.RevalidateDirectory();
            RequireExactEntries(
                ApplicationDirectory,
                expectedNames,
                deadline,
                cancellationToken);

            foreach (ArtifactSetMember member in members)
            {
                CheckOperation(deadline, cancellationToken);
                member.Artifact.RevalidateCurrentPath(
                    deadline,
                    cancellationToken);
            }

            RequireExactEntries(
                ApplicationDirectory,
                expectedNames,
                deadline,
                cancellationToken);
            directoryLease.RevalidateDirectory();
            CheckOperation(deadline, cancellationToken);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            for (int index = members.Length - 1; index >= 0; index--)
            {
                members[index].Artifact.Dispose();
            }

            directoryLease.Dispose();
            CryptographicOperations.ZeroMemory(manifestSha256);
        }
    }

    internal static string ValidateRelativeFileName(
        string value,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 255 ||
            value is "." or ".." ||
            value.EndsWith(' ') ||
            value.EndsWith('.'))
        {
            throw new ArgumentException(
                "An artifact name must be one canonical Windows filename.",
                parameterName);
        }

        const string invalid = "<>:\"/\\|?*";
        foreach (char character in value)
        {
            if (character < 0x20 ||
                character > 0x7e ||
                invalid.IndexOf(character, StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException(
                    "An artifact name must contain only canonical printable ASCII filename characters.",
                    parameterName);
            }
        }

        int dot = value.IndexOf('.');
        string deviceBase = (dot < 0 ? value : value[..dot]).TrimEnd(' ');
        if (IsReservedDeviceBase(deviceBase))
        {
            throw new ArgumentException(
                "An artifact name must not use a reserved Windows device base.",
                parameterName);
        }

        return value;
    }

    private static bool IsReservedDeviceBase(string value)
    {
        if (value.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return value.Length == 4 &&
            (value.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
            value[3] is >= '1' and <= '9';
    }

    private static string ValidateCanonicalApplicationDirectory(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length < 4 ||
            !char.IsAsciiLetter(value[0]) ||
            value[1] != ':' ||
            value[2] != Path.DirectorySeparatorChar ||
            value.IndexOf(Path.AltDirectorySeparatorChar) >= 0 ||
            value.IndexOf(':', 2) >= 0 ||
            Path.EndsInDirectorySeparator(value) ||
            !Path.IsPathFullyQualified(value))
        {
            throw new ArgumentException(
                "The application directory must be an exact absolute local DOS directory path.",
                nameof(value));
        }

        string full = Path.GetFullPath(value);
        if (!string.Equals(full, value, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The application directory path must already be canonical.",
                nameof(value));
        }

        return full;
    }

    private static TrustedArtifactExpectation[] MaterializeExpectations(
        IEnumerable<TrustedArtifactExpectation> expectations,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectations);
        List<TrustedArtifactExpectation> result = new();
        CheckOperation(deadline, cancellationToken);
        using IEnumerator<TrustedArtifactExpectation> enumerator =
            expectations.GetEnumerator();
        while (true)
        {
            CheckOperation(deadline, cancellationToken);
            bool moved = enumerator.MoveNext();
            CheckOperation(deadline, cancellationToken);
            if (!moved)
            {
                break;
            }

            result.Add(enumerator.Current ??
                throw new ArgumentException(
                    "Artifact expectations must not contain null entries.",
                    nameof(expectations)));
            if (result.Count > MaximumArtifactCount)
            {
                throw new ArgumentException(
                    $"An app-local artifact set may contain at most {MaximumArtifactCount} files.",
                    nameof(expectations));
            }
        }

        if (result.Count == 0)
        {
            throw new ArgumentException(
                "At least one app-local artifact is required.",
                nameof(expectations));
        }

        return result.ToArray();
    }

    private static Dictionary<string, string> BuildExpectedNameMap(
        IEnumerable<TrustedArtifactExpectation> expectations)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (TrustedArtifactExpectation expectation in expectations)
        {
            if (!result.TryAdd(
                    expectation.RelativeFileName,
                    expectation.RelativeFileName))
            {
                throw new ArgumentException(
                    "Artifact filenames must be unique without case collisions.",
                    nameof(expectations));
            }
        }

        return result;
    }

    private static void RequireExactEntries(
        string applicationDirectory,
        IReadOnlyDictionary<string, string> expectedNames,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        HashSet<string> observed = new(StringComparer.OrdinalIgnoreCase);
        EnumerationOptions options = new()
        {
            AttributesToSkip = 0,
            IgnoreInaccessible = false,
            MatchCasing = MatchCasing.PlatformDefault,
            MatchType = MatchType.Simple,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
        };

        CheckOperation(deadline, cancellationToken);
        using IEnumerator<string> entries = Directory
            .EnumerateFileSystemEntries(applicationDirectory, "*", options)
            .GetEnumerator();
        while (true)
        {
            CheckOperation(deadline, cancellationToken);
            bool moved = entries.MoveNext();
            CheckOperation(deadline, cancellationToken);
            if (!moved)
            {
                break;
            }

            string name = Path.GetFileName(entries.Current);
            if (!expectedNames.TryGetValue(name, out string? exactName) ||
                !string.Equals(name, exactName, StringComparison.Ordinal) ||
                !observed.Add(name))
            {
                throw new SecurityException(
                    "The protected application directory contains an unexpected, case-mismatched, or duplicate entry.");
            }

            if (observed.Count > MaximumArtifactCount)
            {
                throw new SecurityException(
                    "The protected application directory exceeds the artifact-set entry limit.");
            }
        }

        if (observed.Count != expectedNames.Count)
        {
            throw new SecurityException(
                "The protected application directory is missing an expected artifact.");
        }
    }

    private static byte[] ComputeManifestSha256(
        string executableRelativeFileName,
        IReadOnlyList<ArtifactSetMember> sortedMembers,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        hash.AppendData(ManifestDomain);
        AppendName(hash, executableRelativeFileName);
        Span<byte> scalar = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteUInt32BigEndian(
            scalar,
            checked((uint)sortedMembers.Count));
        hash.AppendData(scalar[..sizeof(uint)]);

        foreach (ArtifactSetMember member in sortedMembers)
        {
            CheckOperation(deadline, cancellationToken);
            AppendName(hash, member.RelativeFileName);
            BinaryPrimitives.WriteInt64BigEndian(scalar, member.Artifact.Length);
            hash.AppendData(scalar);
            byte[] digest = member.Artifact.CopySha256Digest();
            try
            {
                hash.AppendData(digest);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
            }

            CheckOperation(deadline, cancellationToken);
        }

        CheckOperation(deadline, cancellationToken);
        byte[] result = hash.GetHashAndReset();
        if (result.Length != ManifestSha256Length)
        {
            CryptographicOperations.ZeroMemory(result);
            throw new CryptographicException(
                "The artifact-set manifest digest length was invalid.");
        }

        return result;
    }

    private static void AppendName(IncrementalHash hash, string value)
    {
        byte[] name = Encoding.ASCII.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)name.Length));
        hash.AppendData(length);
        hash.AppendData(name);
    }

    private static void CheckOperation(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.GetRemaining();
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(TrustedArtifactSetLease));
        }
    }

    private readonly record struct ArtifactSetMember(
        string RelativeFileName,
        TrustedArtifactLease Artifact);
}
