using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace HrcJobObserver.WindowsBootstrap;

internal enum TrustedNativeSystemModuleConsumerProfile : ushort
{
    SyntheticNativeFixture = 1,
}

internal readonly record struct NativeSystemModuleHostFacts(
    int PointerSize,
    Architecture ProcessArchitecture,
    Architecture OperatingSystemArchitecture,
    NativeFixtureWindowsVersion WindowsVersion);

/// <summary>
/// Authenticated, canonical policy for the exact native System32 files admitted
/// by the synthetic native-fixture consumer. Authentication is relative only to
/// the independently supplied SHA-256 pin; it supplies no signer, Microsoft,
/// KnownDLL, servicing, freshness, rollback, or trusted-launch provenance.
/// </summary>
internal sealed class TrustedNativeSystemModulePolicyV1 : IDisposable
{
    internal const int EncodedLength = 250;
    internal const int RequiredModuleCount = 4;
    internal const ushort Amd64Machine = 0x8664;
    internal const ushort WindowsNtPlatformId = 2;
    internal const uint WindowsMajorVersion = 10;
    internal const uint WindowsMinorVersion = 0;
    internal const uint MinimumWindowsBuild = 16_299;

    private const int Sha256Length = 32;
    private const int HashChunkLength = 4_096;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("HRCOSM01");
    private static readonly byte[] PinDomain = Encoding.ASCII.GetBytes(
        "HRC-BETA-OBSERVER-NATIVE-SYSTEM-MODULE-POLICY-PIN-V1\0");

    private readonly object gate = new();
    private readonly byte[] canonicalPolicy;
    private readonly byte[] policyPinSha256;
    private readonly ModuleExpectation[] modules;
    private bool disposed;

    private TrustedNativeSystemModulePolicyV1(
        byte[] canonicalPolicy,
        byte[] policyPinSha256,
        uint exactWindowsBuild,
        ModuleExpectation[] modules)
    {
        this.canonicalPolicy = canonicalPolicy;
        this.policyPinSha256 = policyPinSha256;
        this.modules = modules;
        ExactWindowsBuildValue = exactWindowsBuild;
    }

    internal TrustedNativeSystemModuleConsumerProfile ConsumerProfile
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return TrustedNativeSystemModuleConsumerProfile
                    .SyntheticNativeFixture;
            }
        }
    }

    internal ushort Architecture
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return Amd64Machine;
            }
        }
    }

    internal ushort PlatformId
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return WindowsNtPlatformId;
            }
        }
    }

    internal uint OperatingSystemMajorVersion
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return WindowsMajorVersion;
            }
        }
    }

    internal uint OperatingSystemMinorVersion
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return WindowsMinorVersion;
            }
        }
    }

    internal uint ExactWindowsBuild
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return ExactWindowsBuildValue;
            }
        }
    }

    internal int ModuleCount
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return modules.Length;
            }
        }
    }

    internal bool IsEligibleForTrustedLaunch
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return false;
            }
        }
    }

    private uint ExactWindowsBuildValue { get; }

    internal static TrustedNativeSystemModulePolicyV1 Authenticate(
        ReadOnlySpan<byte> canonicalPolicy,
        ReadOnlySpan<byte> expectedPolicyPinSha256,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        if (canonicalPolicy.Length != EncodedLength)
        {
            throw new ArgumentException(
                $"The native system-module policy must contain exactly {EncodedLength} bytes.",
                nameof(canonicalPolicy));
        }

        if (expectedPolicyPinSha256.Length != Sha256Length)
        {
            throw new ArgumentException(
                "The expected native system-module policy pin must contain exactly 32 bytes.",
                nameof(expectedPolicyPinSha256));
        }

        byte[]? ownedPolicy = null;
        byte[]? ownedExpectedPin = null;
        byte[]? actualPin = null;
        ModuleExpectation[]? modules = null;
        bool transferred = false;
        try
        {
            ownedPolicy = canonicalPolicy.ToArray();
            ownedExpectedPin = expectedPolicyPinSha256.ToArray();
            CheckOperation(deadline, cancellationToken);
            actualPin = ComputePinSha256(
                ownedPolicy,
                deadline,
                cancellationToken);
            CheckOperation(deadline, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(
                    actualPin,
                    ownedExpectedPin))
            {
                throw new SecurityException(
                    "The native system-module policy does not match the independently supplied pin.");
            }

            CheckOperation(deadline, cancellationToken);
            uint exactWindowsBuild = ParseStructuralCanonical(
                ownedPolicy,
                deadline,
                cancellationToken,
                out modules);
            CheckOperation(deadline, cancellationToken);
            TrustedNativeSystemModulePolicyV1 result = new(
                ownedPolicy,
                actualPin,
                exactWindowsBuild,
                modules);
            ownedPolicy = null;
            actualPin = null;
            modules = null;
            transferred = true;
            try
            {
                CheckOperation(deadline, cancellationToken);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }
        finally
        {
            if (ownedExpectedPin is not null)
            {
                CryptographicOperations.ZeroMemory(ownedExpectedPin);
            }

            if (!transferred)
            {
                if (ownedPolicy is not null)
                {
                    CryptographicOperations.ZeroMemory(ownedPolicy);
                }

                if (actualPin is not null)
                {
                    CryptographicOperations.ZeroMemory(actualPin);
                }

                DisposeExpectations(modules);
            }
        }
    }

    internal NativeStartupSystemModule GetExpectedModule(int ordinal)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return GetExpectationAtOrdinal(ordinal).Module;
        }
    }

    internal string GetExpectedFileName(NativeStartupSystemModule module)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return GetExpectation(module).FileName;
        }
    }

    internal long GetExpectedLength(NativeStartupSystemModule module)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return GetExpectation(module).Length;
        }
    }

    internal byte[] CopyExpectedSha256Digest(
        NativeStartupSystemModule module)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return GetExpectation(module).CopySha256Digest();
        }
    }

    internal byte[] CopyPolicyPinSha256()
    {
        lock (gate)
        {
            ThrowIfDisposed();
            return (byte[])policyPinSha256.Clone();
        }
    }

    internal void RevalidateExactHost(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            RevalidateExactHostCore(new NativeSystemModuleHostFacts(
                IntPtr.Size,
                RuntimeInformation.ProcessArchitecture,
                RuntimeInformation.OSArchitecture,
                NativeFixturePlatformPolicy.ReadProductionVersion()));
            CheckOperation(deadline, cancellationToken);
        }
    }

    /// <summary>
    /// Pure boundary seam for deterministic policy tests. Runtime composition
    /// must use the overload that reads the production process and OS facts.
    /// </summary>
    internal void RevalidateExactHost(
        NativeSystemModuleHostFacts actual,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            CheckOperation(deadline, cancellationToken);
            RevalidateExactHostCore(actual);
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
            CryptographicOperations.ZeroMemory(canonicalPolicy);
            CryptographicOperations.ZeroMemory(policyPinSha256);
            DisposeExpectations(modules);
        }
    }

    private static uint ParseStructuralCanonical(
        ReadOnlySpan<byte> encoded,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken,
        out ModuleExpectation[] modules)
    {
        CheckOperation(deadline, cancellationToken);
        LittleEndianReader reader = new(encoded);
        if (!reader.ReadBytes(Magic.Length, "policy magic").SequenceEqual(Magic))
        {
            throw new FormatException(
                "The native system-module policy magic or version is invalid.");
        }

        if (reader.ReadUInt16("consumer profile") !=
            (ushort)TrustedNativeSystemModuleConsumerProfile
                .SyntheticNativeFixture)
        {
            throw new FormatException(
                "The native system-module policy consumer profile is not admitted.");
        }

        if (reader.ReadUInt16("architecture") != Amd64Machine)
        {
            throw new FormatException(
                "The native system-module policy architecture is not AMD64.");
        }

        if (reader.ReadUInt16("platform") != WindowsNtPlatformId)
        {
            throw new FormatException(
                "The native system-module policy platform is not Win32NT.");
        }

        reader.RequireZero(sizeof(ushort), "first reserved field");
        if (reader.ReadUInt32("operating-system major version") !=
                WindowsMajorVersion ||
            reader.ReadUInt32("operating-system minor version") !=
                WindowsMinorVersion)
        {
            throw new FormatException(
                "The native system-module policy operating-system version is not exact Windows 10.0.");
        }

        uint exactWindowsBuild = reader.ReadUInt32(
            "exact operating-system build");
        if (exactWindowsBuild < MinimumWindowsBuild)
        {
            throw new FormatException(
                "The native system-module policy build predates Windows 10 version 1709.");
        }

        if (reader.ReadUInt32("module count") != RequiredModuleCount)
        {
            throw new FormatException(
                "The native system-module policy must contain exactly four modules.");
        }

        reader.RequireZero(sizeof(uint), "second reserved field");
        ModuleExpectation[] parsed = new ModuleExpectation[RequiredModuleCount];
        try
        {
            for (int ordinal = 0; ordinal < parsed.Length; ordinal++)
            {
                CheckOperation(deadline, cancellationToken);
                NativeStartupSystemModule module = GetModuleAtOrdinal(ordinal);
                string expectedName = GetExactFileName(module);
                ushort nameLength = reader.ReadUInt16(
                    $"module {ordinal} filename length");
                ReadOnlySpan<byte> nameBytes = reader.ReadBytes(
                    nameLength,
                    $"module {ordinal} filename");
                if (nameBytes.Length != expectedName.Length ||
                    !nameBytes.SequenceEqual(Encoding.ASCII.GetBytes(expectedName)))
                {
                    throw new FormatException(
                        $"Native system-module policy ordinal {ordinal} does not contain the exact {expectedName} filename.");
                }

                ulong encodedLength = reader.ReadUInt64(
                    $"module {ordinal} length");
                if (encodedLength == 0 || encodedLength > long.MaxValue)
                {
                    throw new FormatException(
                        "A native system-module policy length is outside the admitted range.");
                }

                ReadOnlySpan<byte> digest = reader.ReadBytes(
                    Sha256Length,
                    $"module {ordinal} SHA-256");
                parsed[ordinal] = new ModuleExpectation(
                    module,
                    expectedName,
                    checked((long)encodedLength),
                    digest);
                CheckOperation(deadline, cancellationToken);
            }

            reader.RequireEnd();
            CheckOperation(deadline, cancellationToken);
            modules = parsed;
            return exactWindowsBuild;
        }
        catch
        {
            DisposeExpectations(parsed);
            throw;
        }
    }

    private static byte[] ComputePinSha256(
        ReadOnlySpan<byte> canonicalPolicy,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        CheckOperation(deadline, cancellationToken);
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        hash.AppendData(PinDomain);
        for (int offset = 0; offset < canonicalPolicy.Length;
            offset += HashChunkLength)
        {
            CheckOperation(deadline, cancellationToken);
            int length = Math.Min(
                HashChunkLength,
                canonicalPolicy.Length - offset);
            hash.AppendData(canonicalPolicy.Slice(offset, length));
        }

        CheckOperation(deadline, cancellationToken);
        byte[] result = hash.GetHashAndReset();
        if (result.Length != Sha256Length)
        {
            CryptographicOperations.ZeroMemory(result);
            throw new CryptographicException(
                "The native system-module policy pin length was invalid.");
        }

        return result;
    }

    private ModuleExpectation GetExpectation(
        NativeStartupSystemModule module) =>
        GetExpectationAtOrdinal(GetOrdinal(module));

    private ModuleExpectation GetExpectationAtOrdinal(int ordinal)
    {
        if ((uint)ordinal >= modules.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        return modules[ordinal];
    }

    private static NativeStartupSystemModule GetModuleAtOrdinal(int ordinal) =>
        ordinal switch
        {
            0 => NativeStartupSystemModule.Ntdll,
            1 => NativeStartupSystemModule.Kernel32,
            2 => NativeStartupSystemModule.KernelBase,
            3 => NativeStartupSystemModule.Apphelp,
            _ => throw new ArgumentOutOfRangeException(nameof(ordinal)),
        };

    private static int GetOrdinal(NativeStartupSystemModule module) =>
        module switch
        {
            NativeStartupSystemModule.Ntdll => 0,
            NativeStartupSystemModule.Kernel32 => 1,
            NativeStartupSystemModule.KernelBase => 2,
            NativeStartupSystemModule.Apphelp => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(module)),
        };

    private static string GetExactFileName(NativeStartupSystemModule module) =>
        module switch
        {
            NativeStartupSystemModule.Ntdll => "ntdll.dll",
            NativeStartupSystemModule.Kernel32 => "kernel32.dll",
            NativeStartupSystemModule.KernelBase => "KernelBase.dll",
            NativeStartupSystemModule.Apphelp => "apphelp.dll",
            _ => throw new ArgumentOutOfRangeException(nameof(module)),
        };

    private static void DisposeExpectations(ModuleExpectation[]? expectations)
    {
        if (expectations is null)
        {
            return;
        }

        for (int index = expectations.Length - 1; index >= 0; index--)
        {
            expectations[index]?.Dispose();
        }
    }

    private static void CheckOperation(
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.GetRemaining();
    }

    private void RevalidateExactHostCore(NativeSystemModuleHostFacts actual)
    {
        if (actual.PointerSize != 8 ||
            actual.ProcessArchitecture !=
                System.Runtime.InteropServices.Architecture.X64 ||
            actual.OperatingSystemArchitecture !=
                System.Runtime.InteropServices.Architecture.X64)
        {
            throw new PlatformNotSupportedException(
                "The current host does not match the authenticated AMD64 native system-module policy.");
        }

        NativeFixtureWindowsVersion version = actual.WindowsVersion;
        if (version.PlatformId != WindowsNtPlatformId ||
            version.Major != WindowsMajorVersion ||
            version.Minor != WindowsMinorVersion ||
            version.Build != ExactWindowsBuildValue)
        {
            throw new PlatformNotSupportedException(
                "The current operating system does not match the authenticated native system-module policy.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed class ModuleExpectation : IDisposable
    {
        private readonly byte[] sha256Digest;

        internal ModuleExpectation(
            NativeStartupSystemModule module,
            string fileName,
            long length,
            ReadOnlySpan<byte> sha256Digest)
        {
            Module = module;
            FileName = fileName;
            Length = length;
            this.sha256Digest = sha256Digest.ToArray();
        }

        internal NativeStartupSystemModule Module { get; }

        internal string FileName { get; }

        internal long Length { get; }

        internal byte[] CopySha256Digest() => (byte[])sha256Digest.Clone();

        public void Dispose()
        {
            CryptographicOperations.ZeroMemory(sha256Digest);
        }
    }

    private ref struct LittleEndianReader
    {
        private readonly ReadOnlySpan<byte> source;
        private int offset;

        internal LittleEndianReader(ReadOnlySpan<byte> source)
        {
            this.source = source;
            offset = 0;
        }

        private int Remaining => source.Length - offset;

        internal ReadOnlySpan<byte> ReadBytes(int length, string fieldName)
        {
            if (length < 0 || length > Remaining)
            {
                throw new FormatException($"The {fieldName} is truncated.");
            }

            ReadOnlySpan<byte> value = source.Slice(offset, length);
            offset += length;
            return value;
        }

        internal ushort ReadUInt16(string fieldName) =>
            BinaryPrimitives.ReadUInt16LittleEndian(
                ReadBytes(sizeof(ushort), fieldName));

        internal uint ReadUInt32(string fieldName) =>
            BinaryPrimitives.ReadUInt32LittleEndian(
                ReadBytes(sizeof(uint), fieldName));

        internal ulong ReadUInt64(string fieldName) =>
            BinaryPrimitives.ReadUInt64LittleEndian(
                ReadBytes(sizeof(ulong), fieldName));

        internal void RequireZero(int length, string fieldName)
        {
            ReadOnlySpan<byte> value = ReadBytes(length, fieldName);
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] != 0)
                {
                    throw new FormatException($"The {fieldName} must be zero.");
                }
            }
        }

        internal void RequireEnd()
        {
            if (Remaining != 0)
            {
                throw new FormatException(
                    "The native system-module policy has trailing bytes.");
            }
        }
    }
}
