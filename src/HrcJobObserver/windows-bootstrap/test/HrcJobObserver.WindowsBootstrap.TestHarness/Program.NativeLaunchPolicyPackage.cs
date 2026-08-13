using System;
using System.Buffers.Binary;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HrcJobObserver.WindowsBootstrap;

internal static partial class Program
{
    private const ulong GoldenNativeLaunchPolicyGeneration =
        0x0102_0304_0506_0708UL;
    private const int NativeLaunchPolicyFixedHeaderLength = 92;
    private const int NativeLaunchPolicyReleaseOffset =
        NativeLaunchPolicyFixedHeaderLength;
    private static readonly byte[] NativeLaunchPolicyPackagePinDomain =
        Encoding.ASCII.GetBytes(
            "HRC-BETA-OBSERVER-NATIVE-LAUNCH-POLICY-PACKAGE-PIN-V1\0");

    private static Task TestNativeLaunchPolicyPackageRoundTripAndGoldenIdentity()
    {
        byte[] release = CreateGoldenNativeReleaseManifest();
        byte[] releasePin = ComputeReleaseManifestPin(release);
        byte[] modules = CreateGoldenNativeSystemModulePolicy();
        byte[] modulePin = ComputeNativeSystemModulePolicyPin(modules);
        byte[] package = EncodeNativeLaunchPolicyPackage(
            GoldenNativeLaunchPolicyGeneration,
            release,
            releasePin,
            modules,
            modulePin);
        byte[] expectedPackagePin = Convert.FromHexString(
            "1282E717A571FF3D70E95BEBDAD9F1E4F" +
            "E22A7B258BF2DEFC0729D007EE5B7E9");
        byte[] actualPackagePin = ComputeNativeLaunchPolicyPackagePin(package);
        byte[] packageBackup = (byte[])package.Clone();
        byte[] pinBackup = (byte[])expectedPackagePin.Clone();
        NativeLaunchPolicyPackageV1? authenticated = null;
        byte[]? packageCopy = null;
        byte[]? packagePinCopy = null;
        byte[]? releaseCopy = null;
        byte[]? releasePinCopy = null;
        byte[]? moduleCopy = null;
        byte[]? modulePinCopy = null;
        try
        {
            AssertEqual(160, release.Length,
                "golden native release-manifest length");
            AssertEqual(250, modules.Length,
                "golden native system-module policy length");
            AssertEqual(502, package.Length,
                "golden native launch-policy package length");
            Assert(actualPackagePin.AsSpan().SequenceEqual(expectedPackagePin),
                "the independent golden native launch-policy package pin changed");

            authenticated = NativeLaunchPolicyPackageV1.Authenticate(
                package,
                expectedPackagePin,
                NewArtifactDeadline(),
                CancellationToken.None);
            Assert(package.AsSpan().SequenceEqual(packageBackup),
                "package authentication must not mutate the caller's package");
            Assert(expectedPackagePin.AsSpan().SequenceEqual(pinBackup),
                "package authentication must not mutate the caller's pin");

            CryptographicOperations.ZeroMemory(package);
            CryptographicOperations.ZeroMemory(expectedPackagePin);
            AssertEqual(
                NativeLaunchPolicyProfile.SyntheticNativeFixture,
                authenticated.Profile,
                "native launch-policy profile");
            AssertEqual(
                GoldenNativeLaunchPolicyGeneration,
                authenticated.Generation,
                "native launch-policy generation");
            AssertEqual(
                ReleaseArtifactRole.SyntheticNativeFixture,
                authenticated.ReleaseArtifactRole,
                "nested release role");
            AssertEqual(
                ReleaseDeploymentKind.NativeNoCrtSystem32Fixture,
                authenticated.ReleaseDeploymentKind,
                "nested release deployment");
            AssertEqual(
                ReleaseTargetRuntimeIdentifier.WinX64,
                authenticated.TargetRuntimeIdentifier,
                "nested release target-runtime label");
            AssertEqual(
                TrustedNativeSystemModuleConsumerProfile.SyntheticNativeFixture,
                authenticated.NativeSystemModuleConsumerProfile,
                "nested native system-module profile");
            Assert(!authenticated.IsEligibleForTrustedLaunch,
                "an authenticated package must remain ineligible for trusted launch");

            packageCopy = authenticated.CopyCanonicalPackage();
            packagePinCopy = authenticated.CopyPackagePinSha256();
            releaseCopy = authenticated.CopyCanonicalReleaseManifest();
            releasePinCopy = authenticated.CopyReleaseManifestPinSha256();
            moduleCopy = authenticated.CopyCanonicalNativeSystemModulePolicy();
            modulePinCopy =
                authenticated.CopyNativeSystemModulePolicyPinSha256();
            Assert(packageCopy.AsSpan().SequenceEqual(packageBackup),
                "the authenticated package must retain its canonical bytes");
            Assert(packagePinCopy.AsSpan().SequenceEqual(pinBackup),
                "the authenticated package must retain its outer pin");
            Assert(releaseCopy.AsSpan().SequenceEqual(release),
                "the authenticated package must retain its release manifest");
            Assert(releasePinCopy.AsSpan().SequenceEqual(releasePin),
                "the authenticated package must retain its release pin");
            Assert(moduleCopy.AsSpan().SequenceEqual(modules),
                "the authenticated package must retain its module policy");
            Assert(modulePinCopy.AsSpan().SequenceEqual(modulePin),
                "the authenticated package must retain its module-policy pin");

            packageCopy[0] ^= 0xff;
            releaseCopy[0] ^= 0xff;
            moduleCopy[0] ^= 0xff;
            byte[] freshPackage = authenticated.CopyCanonicalPackage();
            byte[] freshRelease = authenticated.CopyCanonicalReleaseManifest();
            byte[] freshModules =
                authenticated.CopyCanonicalNativeSystemModulePolicy();
            try
            {
                Assert(freshPackage.AsSpan().SequenceEqual(packageBackup),
                    "package byte copies must be independent");
                Assert(freshRelease.AsSpan().SequenceEqual(release),
                    "release-manifest copies must be independent");
                Assert(freshModules.AsSpan().SequenceEqual(modules),
                    "module-policy copies must be independent");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(freshPackage);
                CryptographicOperations.ZeroMemory(freshRelease);
                CryptographicOperations.ZeroMemory(freshModules);
            }

            authenticated.Dispose();
            authenticated.Dispose();
            AssertThrows<ObjectDisposedException>(() => _ = authenticated.Profile);
            AssertThrows<ObjectDisposedException>(() => _ = authenticated.Generation);
            AssertThrows<ObjectDisposedException>(() =>
                authenticated.CopyCanonicalPackage());
            AssertThrows<ObjectDisposedException>(() =>
                authenticated.CopyPackagePinSha256());
            AssertThrows<ObjectDisposedException>(() =>
                authenticated.CopyCanonicalReleaseManifest());
            AssertThrows<ObjectDisposedException>(() =>
                authenticated.CopyReleaseManifestPinSha256());
            AssertThrows<ObjectDisposedException>(() =>
                authenticated.CopyCanonicalNativeSystemModulePolicy());
            AssertThrows<ObjectDisposedException>(() =>
                authenticated.CopyNativeSystemModulePolicyPinSha256());
            authenticated = null;
            return Task.CompletedTask;
        }
        finally
        {
            authenticated?.Dispose();
            WipeNativeLaunchPolicyBytes(release);
            WipeNativeLaunchPolicyBytes(releasePin);
            WipeNativeLaunchPolicyBytes(modules);
            WipeNativeLaunchPolicyBytes(modulePin);
            WipeNativeLaunchPolicyBytes(package);
            WipeNativeLaunchPolicyBytes(expectedPackagePin);
            WipeNativeLaunchPolicyBytes(actualPackagePin);
            WipeNativeLaunchPolicyBytes(packageBackup);
            WipeNativeLaunchPolicyBytes(pinBackup);
            WipeNativeLaunchPolicyBytes(packageCopy);
            WipeNativeLaunchPolicyBytes(packagePinCopy);
            WipeNativeLaunchPolicyBytes(releaseCopy);
            WipeNativeLaunchPolicyBytes(releasePinCopy);
            WipeNativeLaunchPolicyBytes(moduleCopy);
            WipeNativeLaunchPolicyBytes(modulePinCopy);
        }
    }

    private static Task TestNativeLaunchPolicyPackageAuthenticatesBeforeParsing()
    {
        byte[] malformed = new byte[
            NativeLaunchPolicyPackageV1.MinimumEncodedLength];
        byte[] correctPin = ComputeNativeLaunchPolicyPackagePin(malformed);
        byte[] wrongPin = (byte[])correctPin.Clone();
        wrongPin[0] ^= 0xff;
        byte[] shortPin = wrongPin.AsSpan(0, 31).ToArray();
        byte[] longPin = new byte[33];
        wrongPin.CopyTo(longPin, 0);
        byte[] tooShort = new byte[
            NativeLaunchPolicyPackageV1.MinimumEncodedLength - 1];
        byte[] tooLong = new byte[
            NativeLaunchPolicyPackageV1.MaximumEncodedLength + 1];
        byte[] callerBackup = (byte[])malformed.Clone();
        try
        {
            AssertThrows<SecurityException>(() =>
            {
                using NativeLaunchPolicyPackageV1 ignored =
                    NativeLaunchPolicyPackageV1.Authenticate(
                        malformed,
                        wrongPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            AssertThrows<FormatException>(() =>
            {
                using NativeLaunchPolicyPackageV1 ignored =
                    NativeLaunchPolicyPackageV1.Authenticate(
                        malformed,
                        correctPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using NativeLaunchPolicyPackageV1 ignored =
                    NativeLaunchPolicyPackageV1.Authenticate(
                        malformed,
                        shortPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using NativeLaunchPolicyPackageV1 ignored =
                    NativeLaunchPolicyPackageV1.Authenticate(
                        malformed,
                        longPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using NativeLaunchPolicyPackageV1 ignored =
                    NativeLaunchPolicyPackageV1.Authenticate(
                        tooShort,
                        correctPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            AssertThrows<ArgumentException>(() =>
            {
                using NativeLaunchPolicyPackageV1 ignored =
                    NativeLaunchPolicyPackageV1.Authenticate(
                        tooLong,
                        correctPin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            Assert(malformed.AsSpan().SequenceEqual(callerBackup),
                "authentication-order failures must not mutate caller bytes");
            return Task.CompletedTask;
        }
        finally
        {
            WipeNativeLaunchPolicyBytes(malformed);
            WipeNativeLaunchPolicyBytes(correctPin);
            WipeNativeLaunchPolicyBytes(wrongPin);
            WipeNativeLaunchPolicyBytes(shortPin);
            WipeNativeLaunchPolicyBytes(longPin);
            WipeNativeLaunchPolicyBytes(tooShort);
            WipeNativeLaunchPolicyBytes(tooLong);
            WipeNativeLaunchPolicyBytes(callerBackup);
        }
    }

    private static Task
        TestNativeLaunchPolicyPackageRejectsNoncanonicalAndMismatchedPolicies()
    {
        byte[] release = CreateGoldenNativeReleaseManifest();
        byte[] releasePin = ComputeReleaseManifestPin(release);
        byte[] modules = CreateGoldenNativeSystemModulePolicy();
        byte[] modulePin = ComputeNativeSystemModulePolicyPin(modules);
        byte[] canonical = EncodeNativeLaunchPolicyPackage(
            GoldenNativeLaunchPolicyGeneration,
            release,
            releasePin,
            modules,
            modulePin);
        try
        {
            AssertNativeLaunchPolicyFormatMutation(
                canonical,
                static value => value[0] ^= 1,
                "magic");
            AssertNativeLaunchPolicyFormatMutation(
                canonical,
                static value => BinaryPrimitives.WriteUInt16BigEndian(
                    value.AsSpan(8, 2), 2),
                "profile");
            AssertNativeLaunchPolicyFormatMutation(
                canonical,
                static value => value[10] = 1,
                "reserved field");
            AssertNativeLaunchPolicyFormatMutation(
                canonical,
                static value => value.AsSpan(12, 8).Clear(),
                "zero generation");
            AssertNativeLaunchPolicyFormatMutation(
                canonical,
                static value => BinaryPrimitives.WriteUInt32BigEndian(
                    value.AsSpan(20, 4), 97),
                "short nested release length");
            AssertNativeLaunchPolicyFormatMutation(
                canonical,
                static value => BinaryPrimitives.WriteUInt32BigEndian(
                    value.AsSpan(20, 4), 38_326),
                "excessive nested release length");
            AssertNativeLaunchPolicyFormatMutation(
                canonical,
                static value => BinaryPrimitives.WriteUInt32BigEndian(
                    value.AsSpan(24, 4), 249),
                "wrong nested module-policy length");

            byte[] truncated = canonical[..^1];
            byte[] trailing = new byte[canonical.Length + 1];
            canonical.CopyTo(trailing, 0);
            AssertNativeLaunchPolicyFormatFailure(truncated, "truncation");
            AssertNativeLaunchPolicyFormatFailure(trailing, "trailing byte");

            byte[] wrongNestedReleasePin = (byte[])canonical.Clone();
            wrongNestedReleasePin[28] ^= 0xff;
            AssertNativeLaunchPolicySecurityFailure(
                wrongNestedReleasePin,
                "nested release pin");
            byte[] wrongNestedModulePin = (byte[])canonical.Clone();
            wrongNestedModulePin[60] ^= 0xff;
            AssertNativeLaunchPolicySecurityFailure(
                wrongNestedModulePin,
                "nested module-policy pin");

            byte[] legacyRelease = CreateGoldenLegacyReleaseManifest();
            byte[] legacyPin = ComputeReleaseManifestPin(legacyRelease);
            byte[] crossProfile = EncodeNativeLaunchPolicyPackage(
                GoldenNativeLaunchPolicyGeneration,
                legacyRelease,
                legacyPin,
                modules,
                modulePin);
            try
            {
                AssertNativeLaunchPolicySecurityFailure(
                    crossProfile,
                    "closed nested release profile");
            }
            finally
            {
                WipeNativeLaunchPolicyBytes(legacyRelease);
                WipeNativeLaunchPolicyBytes(legacyPin);
            }

            byte[] malformedNestedRelease = (byte[])canonical.Clone();
            malformedNestedRelease[NativeLaunchPolicyReleaseOffset] ^= 1;
            RefreshNativeLaunchPolicyNestedReleasePin(malformedNestedRelease);
            AssertNativeLaunchPolicyFormatFailure(
                malformedNestedRelease,
                "authenticated malformed nested release manifest");

            byte[] malformedNestedModuleProfile = (byte[])canonical.Clone();
            int moduleOffset = GetNativeLaunchPolicyModuleOffset(
                malformedNestedModuleProfile);
            BinaryPrimitives.WriteUInt16LittleEndian(
                malformedNestedModuleProfile.AsSpan(moduleOffset + 8, 2),
                2);
            RefreshNativeLaunchPolicyNestedModulePin(
                malformedNestedModuleProfile);
            AssertNativeLaunchPolicyFormatFailure(
                malformedNestedModuleProfile,
                "authenticated malformed nested module profile");
            return Task.CompletedTask;
        }
        finally
        {
            WipeNativeLaunchPolicyBytes(release);
            WipeNativeLaunchPolicyBytes(releasePin);
            WipeNativeLaunchPolicyBytes(modules);
            WipeNativeLaunchPolicyBytes(modulePin);
            WipeNativeLaunchPolicyBytes(canonical);
        }
    }

    private static Task TestNativeLaunchPolicyPackageBoundsAndFailureRollback()
    {
        byte[] release = CreateGoldenNativeReleaseManifest();
        byte[] releasePin = ComputeReleaseManifestPin(release);
        byte[] modules = CreateGoldenNativeSystemModulePolicy();
        byte[] modulePin = ComputeNativeSystemModulePolicyPin(modules);
        byte[] package = EncodeNativeLaunchPolicyPackage(
            GoldenNativeLaunchPolicyGeneration,
            release,
            releasePin,
            modules,
            modulePin);
        byte[] pin = ComputeNativeLaunchPolicyPackagePin(package);
        byte[] packageBackup = (byte[])package.Clone();
        byte[] pinBackup = (byte[])pin.Clone();
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        ManualTimeProvider expiredClock = new(CanonicalTestUtcNow());
        MonotonicDeadline expired = MonotonicDeadline.Start(
            expiredClock,
            TestTimeout);
        expiredClock.Advance(TestTimeout);
        try
        {
            AssertThrows<OperationCanceledException>(() =>
            {
                using NativeLaunchPolicyPackageV1 ignored =
                    NativeLaunchPolicyPackageV1.Authenticate(
                        package,
                        pin,
                        NewArtifactDeadline(),
                        cancelled.Token);
            });
            AssertThrows<TimeoutException>(() =>
            {
                using NativeLaunchPolicyPackageV1 ignored =
                    NativeLaunchPolicyPackageV1.Authenticate(
                        package,
                        pin,
                        expired,
                        CancellationToken.None);
            });

            CaptureTimestampTimeProvider calibration = new(expireOnRead: null);
            MonotonicDeadline calibrationDeadline = MonotonicDeadline.Start(
                calibration,
                TestTimeout);
            using (NativeLaunchPolicyPackageV1 ignored =
                   NativeLaunchPolicyPackageV1.Authenticate(
                       package,
                       pin,
                       calibrationDeadline,
                       CancellationToken.None))
            {
            }

            int successfulReads = calibration.TimestampReads;
            Assert(successfulReads > 1,
                "package authentication must check its deadline after construction");
            CaptureTimestampTimeProvider late = new(successfulReads);
            MonotonicDeadline lateDeadline = MonotonicDeadline.Start(
                late,
                TestTimeout);
            AssertThrows<TimeoutException>(() =>
            {
                using NativeLaunchPolicyPackageV1 ignored =
                    NativeLaunchPolicyPackageV1.Authenticate(
                        package,
                        pin,
                        lateDeadline,
                        CancellationToken.None);
            });
            AssertEqual(successfulReads, late.TimestampReads,
                "late package-authentication deadline-check ordinal");
            Assert(package.AsSpan().SequenceEqual(packageBackup),
                "bounded package failures must not mutate caller bytes");
            Assert(pin.AsSpan().SequenceEqual(pinBackup),
                "bounded package failures must not mutate the caller's pin");

            using NativeLaunchPolicyPackageV1 clean =
                NativeLaunchPolicyPackageV1.Authenticate(
                    package,
                    pin,
                    NewArtifactDeadline(),
                    CancellationToken.None);
            AssertEqual(GoldenNativeLaunchPolicyGeneration, clean.Generation,
                "a clean authentication must succeed after bounded failures");
            return Task.CompletedTask;
        }
        finally
        {
            WipeNativeLaunchPolicyBytes(release);
            WipeNativeLaunchPolicyBytes(releasePin);
            WipeNativeLaunchPolicyBytes(modules);
            WipeNativeLaunchPolicyBytes(modulePin);
            WipeNativeLaunchPolicyBytes(package);
            WipeNativeLaunchPolicyBytes(pin);
            WipeNativeLaunchPolicyBytes(packageBackup);
            WipeNativeLaunchPolicyBytes(pinBackup);
        }
    }

    private static byte[] CreateGoldenNativeReleaseManifest()
    {
        return Convert.FromHexString(
            "48524352454C303102020001000000000020" +
            "4872634A6F624F627365727665722E4E6174697665466978747572652E657865" +
            "000000010020" +
            "4872634A6F624F627365727665722E4E6174697665466978747572652E657865" +
            "0000000000001000" +
            "112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00" +
            "AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00112233445566778899");
    }

    private static byte[] CreateGoldenLegacyReleaseManifest()
    {
        return Convert.FromHexString(
            "48524352454C303101010001000000000008726F6C652E657865" +
            "000000010008726F6C652E6578650000000000000002" +
            "112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00" +
            "AABBCCDDEEFF00112233445566778899AABBCCDDEEFF00112233445566778899");
    }

    private static byte[] EncodeNativeLaunchPolicyPackage(
        ulong generation,
        ReadOnlySpan<byte> releaseManifest,
        ReadOnlySpan<byte> releaseManifestPin,
        ReadOnlySpan<byte> nativeSystemModulePolicy,
        ReadOnlySpan<byte> nativeSystemModulePolicyPin)
    {
        using MemoryStream output = new();
        output.Write("HRCNLP01"u8);
        BootstrapBinary.WriteUInt16(
            output,
            (ushort)NativeLaunchPolicyProfile.SyntheticNativeFixture);
        BootstrapBinary.WriteUInt16(output, 0);
        BootstrapBinary.WriteUInt64(output, generation);
        BootstrapBinary.WriteUInt32(
            output,
            checked((uint)releaseManifest.Length));
        BootstrapBinary.WriteUInt32(
            output,
            checked((uint)nativeSystemModulePolicy.Length));
        output.Write(releaseManifestPin);
        output.Write(nativeSystemModulePolicyPin);
        output.Write(releaseManifest);
        output.Write(nativeSystemModulePolicy);
        return output.ToArray();
    }

    private static byte[] ComputeNativeLaunchPolicyPackagePin(
        ReadOnlySpan<byte> package)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);
        hash.AppendData(NativeLaunchPolicyPackagePinDomain);
        hash.AppendData(package);
        return hash.GetHashAndReset();
    }

    private static void AssertNativeLaunchPolicyFormatMutation(
        ReadOnlySpan<byte> canonical,
        Action<byte[]> mutation,
        string description)
    {
        byte[] malformed = canonical.ToArray();
        mutation(malformed);
        AssertNativeLaunchPolicyFormatFailure(malformed, description);
    }

    private static void AssertNativeLaunchPolicyFormatFailure(
        byte[] malformed,
        string description)
    {
        byte[] pin = ComputeNativeLaunchPolicyPackagePin(malformed);
        byte[] malformedBackup = (byte[])malformed.Clone();
        byte[] pinBackup = (byte[])pin.Clone();
        try
        {
            AssertThrows<FormatException>(() =>
            {
                using NativeLaunchPolicyPackageV1 ignored =
                    NativeLaunchPolicyPackageV1.Authenticate(
                        malformed,
                        pin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            Assert(malformed.AsSpan().SequenceEqual(malformedBackup),
                $"{description} rejection must not mutate caller bytes");
            Assert(pin.AsSpan().SequenceEqual(pinBackup),
                $"{description} rejection must not mutate the caller's pin");
        }
        finally
        {
            WipeNativeLaunchPolicyBytes(malformed);
            WipeNativeLaunchPolicyBytes(pin);
            WipeNativeLaunchPolicyBytes(malformedBackup);
            WipeNativeLaunchPolicyBytes(pinBackup);
        }
    }

    private static void AssertNativeLaunchPolicySecurityFailure(
        byte[] malformed,
        string description)
    {
        byte[] pin = ComputeNativeLaunchPolicyPackagePin(malformed);
        byte[] malformedBackup = (byte[])malformed.Clone();
        byte[] pinBackup = (byte[])pin.Clone();
        try
        {
            AssertThrows<SecurityException>(() =>
            {
                using NativeLaunchPolicyPackageV1 ignored =
                    NativeLaunchPolicyPackageV1.Authenticate(
                        malformed,
                        pin,
                        NewArtifactDeadline(),
                        CancellationToken.None);
            });
            Assert(malformed.AsSpan().SequenceEqual(malformedBackup),
                $"{description} rejection must not mutate caller bytes");
            Assert(pin.AsSpan().SequenceEqual(pinBackup),
                $"{description} rejection must not mutate the caller's pin");
        }
        finally
        {
            WipeNativeLaunchPolicyBytes(malformed);
            WipeNativeLaunchPolicyBytes(pin);
            WipeNativeLaunchPolicyBytes(malformedBackup);
            WipeNativeLaunchPolicyBytes(pinBackup);
        }
    }

    private static void RefreshNativeLaunchPolicyNestedReleasePin(byte[] package)
    {
        int releaseLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(
            package.AsSpan(20, 4)));
        byte[] pin = ComputeReleaseManifestPin(
            package.AsSpan(NativeLaunchPolicyReleaseOffset, releaseLength));
        try
        {
            pin.CopyTo(package, 28);
        }
        finally
        {
            WipeNativeLaunchPolicyBytes(pin);
        }
    }

    private static void RefreshNativeLaunchPolicyNestedModulePin(byte[] package)
    {
        int moduleOffset = GetNativeLaunchPolicyModuleOffset(package);
        int moduleLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(
            package.AsSpan(24, 4)));
        byte[] pin = ComputeNativeSystemModulePolicyPin(
            package.AsSpan(moduleOffset, moduleLength));
        try
        {
            pin.CopyTo(package, 60);
        }
        finally
        {
            WipeNativeLaunchPolicyBytes(pin);
        }
    }

    private static int GetNativeLaunchPolicyModuleOffset(
        ReadOnlySpan<byte> package)
    {
        int releaseLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(
            package.Slice(20, 4)));
        return checked(NativeLaunchPolicyFixedHeaderLength + releaseLength);
    }

    private static void WipeNativeLaunchPolicyBytes(byte[]? value)
    {
        if (value is not null)
        {
            CryptographicOperations.ZeroMemory(value);
        }
    }
}
