using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security;
using System.Security.Cryptography;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Strict, source/test-only structural audit for the current synthetic native
/// fixture profile. The caller supplies the independently trusted file digest
/// and exact manifest bytes. This is neither a general PE parser nor release
/// provenance, and it does not prove the fixture's machine-code semantics.
/// </summary>
internal sealed class NativeFixturePeAudit : IDisposable
{
    internal const int ExactImageLength = 4_096;
    internal const int ExactManifestLength = 510;
    private const int Sha256Length = 32;
    private const int PeOffset = 0xd8;
    private const int CoffOffset = PeOffset + 4;
    private const int OptionalOffset = CoffOffset + 20;
    private const int OptionalHeaderLength = 0xf0;
    private const int SectionTableOffset = OptionalOffset + OptionalHeaderLength;
    private const int ChecksumOffset = OptionalOffset + 64;
    private const uint ImageScnMemExecute = 0x2000_0000;
    private const uint ImageScnMemWrite = 0x8000_0000;

    private static readonly byte[] ExpectedReproducibleBuildId =
        Convert.FromHexString(
            "3ba123e6d4167f80d4f2e48f9e4eb33f" +
            "2e58547e66f7ac1ac9da2692de334c5b");

    private static readonly SectionProfile[] ExpectedSections =
    {
        new(".text", 0x0d6, 0x1000, 0x200, 0x400, 0x6000_0020),
        new(".rdata", 0x3ce, 0x2000, 0x400, 0x600, 0x4000_0040),
        new(".pdata", 0x00c, 0x3000, 0x200, 0xa00, 0x4000_0040),
        new(".rsrc", 0x260, 0x4000, 0x400, 0xc00, 0x4000_0040),
    };

    private static readonly CoffGroupProfile[] ExpectedCoffGroups =
    {
        new(0x1000, 0x0d6, ".text$mn"),
        new(0x2000, 0x020, ".idata$5"),
        new(0x2020, 0x1dc, ".rdata"),
        new(0x21fc, 0x020, ".rdata$voltmd"),
        new(0x221c, 0x12c, ".rdata$zzzdbg"),
        new(0x2348, 0x008, ".xdata"),
        new(0x2350, 0x014, ".idata$2"),
        new(0x2364, 0x014, ".idata$3"),
        new(0x2378, 0x020, ".idata$4"),
        new(0x2398, 0x036, ".idata$6"),
        new(0x3000, 0x00c, ".pdata"),
        new(0x4000, 0x060, ".rsrc$01"),
        new(0x4060, 0x200, ".rsrc$02"),
    };

    private byte[]? imageSnapshot;
    private byte[]? manifestSnapshot;
    private byte[]? imageSha256;
    private byte[]? reproducibleBuildId;

    private NativeFixturePeAudit(
        byte[] imageSnapshot,
        byte[] manifestSnapshot,
        byte[] imageSha256,
        byte[] reproducibleBuildId)
    {
        this.imageSnapshot = imageSnapshot;
        this.manifestSnapshot = manifestSnapshot;
        this.imageSha256 = imageSha256;
        this.reproducibleBuildId = reproducibleBuildId;
    }

    /// <summary>
    /// The fixture deliberately has no dynamic indirect-control-flow path.
    /// GuardFlags is therefore required to be zero. The audit does not infer or
    /// prove that source-level boundary from machine code.
    /// </summary>
    internal bool RequiresNoDynamicIndirectControlFlow => true;

    internal bool HasGuardCfInstrumentation => false;

    internal bool ProvesMachineCodeSemantics => false;

    internal bool IsEligibleForTrustedLaunch => false;

    internal byte[] CopyImageSha256()
    {
        return CopyOwned(imageSha256, "native fixture audit");
    }

    internal byte[] CopyReproducibleBuildId()
    {
        return CopyOwned(reproducibleBuildId, "native fixture audit");
    }

    internal static NativeFixturePeAudit Open(
        ReadOnlySpan<byte> image,
        ReadOnlySpan<byte> exactManifest,
        ReadOnlySpan<byte> expectedImageSha256)
    {
        if (image.Length != ExactImageLength)
        {
            throw new ArgumentException(
                "The native fixture image must have its exact bounded length.",
                nameof(image));
        }

        if (exactManifest.Length != ExactManifestLength)
        {
            throw new ArgumentException(
                "The native fixture manifest must have its exact bounded length.",
                nameof(exactManifest));
        }

        if (expectedImageSha256.Length != Sha256Length)
        {
            throw new ArgumentException(
                "The expected native fixture digest must be exactly SHA-256 sized.",
                nameof(expectedImageSha256));
        }

        // No parsing occurs through caller-owned spans. Every input is copied
        // only after its strict public bound has been checked.
        byte[] ownedImage = image.ToArray();
        byte[] ownedManifest = exactManifest.ToArray();
        byte[] ownedExpectedDigest = expectedImageSha256.ToArray();
        byte[]? actualDigest = null;
        byte[]? reproBuildId = null;
        try
        {
            actualDigest = SHA256.HashData(ownedImage);
            if (!CryptographicOperations.FixedTimeEquals(
                    actualDigest,
                    ownedExpectedDigest))
            {
                throw new SecurityException(
                    "The native fixture image did not match its caller-supplied digest.");
            }

            reproBuildId = AuditStructuralProfile(ownedImage, ownedManifest);
            NativeFixturePeAudit result = new(
                ownedImage,
                ownedManifest,
                actualDigest,
                reproBuildId);
            ownedImage = Array.Empty<byte>();
            ownedManifest = Array.Empty<byte>();
            actualDigest = null;
            reproBuildId = null;
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ownedImage);
            CryptographicOperations.ZeroMemory(ownedManifest);
            CryptographicOperations.ZeroMemory(ownedExpectedDigest);
            if (actualDigest is not null)
            {
                CryptographicOperations.ZeroMemory(actualDigest);
            }

            if (reproBuildId is not null)
            {
                CryptographicOperations.ZeroMemory(reproBuildId);
            }
        }
    }

    public void Dispose()
    {
        Wipe(ref imageSnapshot);
        Wipe(ref manifestSnapshot);
        Wipe(ref imageSha256);
        Wipe(ref reproducibleBuildId);
    }

    private static byte[] AuditStructuralProfile(
        byte[] image,
        byte[] exactManifest)
    {
        ReadOnlySpan<byte> bytes = image;
        RequireUInt16(bytes, 0, 0x5a4d, "DOS signature");
        RequireUInt32(bytes, 0x3c, PeOffset, "PE header offset");
        RequireUInt32(bytes, PeOffset, 0x0000_4550, "PE signature");

        RequireUInt16(bytes, CoffOffset, 0x8664, "COFF machine");
        RequireUInt16(bytes, CoffOffset + 2, 4, "COFF section count");
        uint timeDateStamp = ReadUInt32(bytes, CoffOffset + 4, "COFF timestamp");
        Require(timeDateStamp != 0, "The reproducible COFF timestamp is zero.");
        RequireUInt32(bytes, CoffOffset + 8, 0, "COFF symbol-table pointer");
        RequireUInt32(bytes, CoffOffset + 12, 0, "COFF symbol count");
        RequireUInt16(
            bytes,
            CoffOffset + 16,
            OptionalHeaderLength,
            "optional-header size");
        RequireUInt16(bytes, CoffOffset + 18, 0x0022, "COFF characteristics");

        AuditOptionalHeader(bytes);
        SectionProfile[] sections = AuditSections(bytes);
        AuditEntryPoint(sections);
        AuditImports(bytes, sections);
        AuditLoadConfiguration(bytes, sections);
        byte[] reproducibleId = AuditDebugDirectory(
            bytes,
            sections,
            timeDateStamp);
        try
        {
            AuditResources(bytes, sections, exactManifest);
            AuditExceptionDirectory(bytes, sections);
            AuditChecksum(bytes);
            return reproducibleId;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(reproducibleId);
            throw;
        }
    }

    private static void AuditOptionalHeader(ReadOnlySpan<byte> bytes)
    {
        RequireUInt16(bytes, OptionalOffset, 0x020b, "PE32+ magic");
        RequireByte(bytes, OptionalOffset + 2, 14, "linker major version");
        RequireByte(bytes, OptionalOffset + 3, 44, "linker minor version");
        RequireUInt32(bytes, OptionalOffset + 4, 0x200, "size of code");
        RequireUInt32(
            bytes,
            OptionalOffset + 8,
            0xa00,
            "size of initialized data");
        RequireUInt32(
            bytes,
            OptionalOffset + 12,
            0,
            "size of uninitialized data");
        RequireUInt32(bytes, OptionalOffset + 16, 0x1070, "entry point");
        RequireUInt32(bytes, OptionalOffset + 20, 0x1000, "base of code");
        RequireUInt64(bytes, OptionalOffset + 24, 0x0000_0001_4000_0000UL,
            "image base");
        RequireUInt32(bytes, OptionalOffset + 32, 0x1000, "section alignment");
        RequireUInt32(bytes, OptionalOffset + 36, 0x200, "file alignment");
        RequireVersion(bytes, OptionalOffset + 40, 6, 2, "operating system");
        RequireVersion(bytes, OptionalOffset + 44, 0, 0, "image");
        RequireVersion(bytes, OptionalOffset + 48, 6, 2, "subsystem");
        RequireUInt32(bytes, OptionalOffset + 52, 0, "Win32 version");
        RequireUInt32(bytes, OptionalOffset + 56, 0x5000, "size of image");
        RequireUInt32(bytes, OptionalOffset + 60, 0x400, "size of headers");
        RequireUInt16(bytes, OptionalOffset + 68, 2, "Windows GUI subsystem");
        RequireUInt16(bytes, OptionalOffset + 70, 0x8160,
            "DLL characteristics");
        RequireUInt64(bytes, OptionalOffset + 72, 0x100000, "stack reserve");
        RequireUInt64(bytes, OptionalOffset + 80, 0x1000, "stack commit");
        RequireUInt64(bytes, OptionalOffset + 88, 0x100000, "heap reserve");
        RequireUInt64(bytes, OptionalOffset + 96, 0x1000, "heap commit");
        RequireUInt32(bytes, OptionalOffset + 104, 0, "loader flags");
        RequireUInt32(bytes, OptionalOffset + 108, 16, "data-directory count");

        RequireDirectory(bytes, 0, 0, 0);
        RequireDirectory(bytes, 1, 0x2350, 0x28);
        RequireDirectory(bytes, 2, 0x4000, 0x260);
        RequireDirectory(bytes, 3, 0x3000, 0x0c);
        RequireDirectory(bytes, 4, 0, 0);
        RequireDirectory(bytes, 5, 0, 0);
        RequireDirectory(bytes, 6, 0x21a8, 0x54);
        RequireDirectory(bytes, 7, 0, 0);
        RequireDirectory(bytes, 8, 0, 0);
        RequireDirectory(bytes, 9, 0, 0);
        RequireDirectory(bytes, 10, 0x2020, 0x148);
        RequireDirectory(bytes, 11, 0, 0);
        RequireDirectory(bytes, 12, 0x2000, 0x20);
        RequireDirectory(bytes, 13, 0, 0);
        RequireDirectory(bytes, 14, 0, 0);
        RequireDirectory(bytes, 15, 0, 0);
    }

    private static SectionProfile[] AuditSections(ReadOnlySpan<byte> bytes)
    {
        SectionProfile[] sections = new SectionProfile[ExpectedSections.Length];
        int expectedRawStart = 0x400;
        for (int index = 0; index < sections.Length; index++)
        {
            int offset = CheckedAdd(
                SectionTableOffset,
                checked(index * 40),
                "section header");
            SectionProfile expected = ExpectedSections[index];
            string name = ReadFixedAsciiName(bytes, offset, 8, "section name");
            Require(string.Equals(name, expected.Name, StringComparison.Ordinal),
                $"The section at index {index} has an unexpected name.");
            RequireUInt32(bytes, offset + 8, expected.VirtualSize,
                $"{expected.Name} virtual size");
            RequireUInt32(bytes, offset + 12, expected.VirtualAddress,
                $"{expected.Name} virtual address");
            RequireUInt32(bytes, offset + 16, expected.RawSize,
                $"{expected.Name} raw size");
            RequireUInt32(bytes, offset + 20, expected.RawOffset,
                $"{expected.Name} raw offset");
            RequireUInt32(bytes, offset + 24, 0,
                $"{expected.Name} relocation pointer");
            RequireUInt32(bytes, offset + 28, 0,
                $"{expected.Name} line-number pointer");
            RequireUInt16(bytes, offset + 32, 0,
                $"{expected.Name} relocation count");
            RequireUInt16(bytes, offset + 34, 0,
                $"{expected.Name} line-number count");
            RequireUInt32(bytes, offset + 36, expected.Characteristics,
                $"{expected.Name} characteristics");
            Require((expected.Characteristics &
                    (ImageScnMemExecute | ImageScnMemWrite)) !=
                    (ImageScnMemExecute | ImageScnMemWrite),
                $"The {expected.Name} section is writable and executable.");
            Require(expected.RawOffset == expectedRawStart,
                "The section raw ranges are not contiguous.");
            expectedRawStart = CheckedAdd(
                expectedRawStart,
                checked((int)expected.RawSize),
                "section raw range");
            RequireRange(bytes, checked((int)expected.RawOffset),
                checked((int)expected.RawSize), $"{expected.Name} raw data");
            sections[index] = expected;
        }

        Require(expectedRawStart == bytes.Length,
            "The native fixture contains an overlay or an unaccounted raw gap.");
        return sections;
    }

    private static void AuditEntryPoint(SectionProfile[] sections)
    {
        const uint entryPoint = 0x1070;
        SectionProfile section = FindSection(sections, entryPoint, 1,
            "entry point");
        Require((section.Characteristics & ImageScnMemExecute) != 0 &&
                (section.Characteristics & ImageScnMemWrite) == 0,
            "The entry point is not in a non-writable executable section.");
    }

    private static void AuditImports(
        ReadOnlySpan<byte> bytes,
        SectionProfile[] sections)
    {
        int descriptor = RvaToOffset(sections, 0x2350, 0x28,
            "import directory");
        RequireUInt32(bytes, descriptor, 0x2378, "import lookup table");
        RequireUInt32(bytes, descriptor + 4, 0, "import timestamp");
        RequireUInt32(bytes, descriptor + 8, 0, "import forwarder chain");
        RequireUInt32(bytes, descriptor + 12, 0x23c0, "import module name");
        RequireUInt32(bytes, descriptor + 16, 0x2000,
            "import address table");
        RequireAllZero(bytes, descriptor + 20, 20,
            "terminal import descriptor");

        string module = ReadAsciiZAtRva(bytes, sections, 0x23c0, 32,
            "import module name");
        Require(string.Equals(module, "KERNEL32.dll", StringComparison.Ordinal),
            "The native fixture imports an unexpected module.");

        int lookup = RvaToOffset(sections, 0x2378, 0x20,
            "import lookup table");
        int address = RvaToOffset(sections, 0x2000, 0x20,
            "import address table");
        ImportProfile[] expectedImports =
        {
            new(0x23aa, 0x0186, "ExitProcess"),
            new(0x23b8, 0x05c8, "Sleep"),
            new(0x2398, 0x0200, "GetCommandLineW"),
        };
        HashSet<string> imports = new(StringComparer.Ordinal);
        for (int index = 0; index < 3; index++)
        {
            ImportProfile expected = expectedImports[index];
            int thunkOffset = checked(index * sizeof(ulong));
            ulong lookupValue = ReadUInt64(bytes, lookup + thunkOffset,
                "import lookup thunk");
            ulong addressValue = ReadUInt64(bytes, address + thunkOffset,
                "import address thunk");
            Require(lookupValue == addressValue,
                "The import lookup and address thunks differ.");
            Require((lookupValue & 0x8000_0000_0000_0000UL) == 0 &&
                    lookupValue <= uint.MaxValue,
                "Ordinal or out-of-range imports are forbidden.");
            uint nameRva = checked((uint)lookupValue);
            Require(nameRva == expected.NameRva,
                $"The import name RVA in slot {index} is unexpected.");
            int hintOffset = RvaToOffset(sections, nameRva, 2,
                "import hint/name");
            ushort hint = ReadUInt16(bytes, hintOffset, "import hint");
            string name = ReadAsciiZAtRva(
                bytes,
                sections,
                CheckedAddRva(nameRva, 2, "import name"),
                64,
                "import name");
            Require(imports.Add(name), "Duplicate imports are forbidden.");
            Require(string.Equals(name, expected.Name, StringComparison.Ordinal),
                $"The import name in slot {index} is unexpected.");
            Require(hint == expected.Hint,
                $"The import hint in slot {index} is unexpected.");
        }

        RequireUInt64(bytes, lookup + 24, 0, "terminal import lookup thunk");
        RequireUInt64(bytes, address + 24, 0, "terminal import address thunk");
        Require(imports.Count == 3 &&
                imports.Contains("ExitProcess") &&
                imports.Contains("GetCommandLineW") &&
                imports.Contains("Sleep"),
            "The native fixture import set is incomplete.");
    }

    private static void AuditLoadConfiguration(
        ReadOnlySpan<byte> bytes,
        SectionProfile[] sections)
    {
        int offset = RvaToOffset(sections, 0x2020, 0x148,
            "load configuration");
        RequireUInt32(bytes, offset, 0x148, "load-configuration size");
        RequireUInt16(bytes, offset + 0x4e, 0x0800,
            "dependent load flags");
        for (int index = 0; index < 0x148; index++)
        {
            bool admitted = index < sizeof(uint) ||
                index is 0x4e or 0x4f;
            if (!admitted && bytes[offset + index] != 0)
            {
                throw new FormatException(
                    "An unsupported load-configuration field is nonzero.");
            }
        }

        // GuardFlags at 0x90 is included in the all-zero rule. Zero is admitted
        // only because this fixture's external source boundary forbids dynamic
        // indirect control flow; this parser does not prove that boundary.
        RequireUInt32(bytes, offset + 0x90, 0, "GuardFlags");
    }

    private static byte[] AuditDebugDirectory(
        ReadOnlySpan<byte> bytes,
        SectionProfile[] sections,
        uint timeDateStamp)
    {
        int directory = RvaToOffset(sections, 0x21a8, 0x54,
            "debug directory");
        DebugProfile[] profiles =
        {
            new(13, 0x104, 0x221c, 0x81c),
            new(16, 0x024, 0x2320, 0x920),
            new(20, 0x004, 0x2344, 0x944),
        };

        for (int index = 0; index < profiles.Length; index++)
        {
            DebugProfile profile = profiles[index];
            int entry = directory + checked(index * 28);
            RequireUInt32(bytes, entry, 0, "debug characteristics");
            RequireUInt32(bytes, entry + 4, timeDateStamp, "debug timestamp");
            RequireUInt16(bytes, entry + 8, 0, "debug major version");
            RequireUInt16(bytes, entry + 10, 0, "debug minor version");
            RequireUInt32(bytes, entry + 12, profile.Type, "debug type");
            RequireUInt32(bytes, entry + 16, profile.Size, "debug data size");
            RequireUInt32(bytes, entry + 20, profile.Rva, "debug data RVA");
            RequireUInt32(bytes, entry + 24, checked((uint)profile.RawOffset),
                "debug data raw offset");
            int mapped = RvaToOffset(sections, profile.Rva, profile.Size,
                "debug data");
            Require(mapped == profile.RawOffset,
                "Debug RVA and raw-pointer mappings differ.");
        }

        AuditCoffGroups(bytes.Slice(0x81c, 0x104), sections);
        ReadOnlySpan<byte> repro = bytes.Slice(0x920, 0x24);
        RequireUInt32(repro, 0, Sha256Length, "REPRO digest length");
        byte[] buildId = repro.Slice(4, Sha256Length).ToArray();
        try
        {
            Require(buildId.AsSpan().SequenceEqual(ExpectedReproducibleBuildId),
                "The native fixture REPRO build identity is unexpected.");

            Require(BinaryPrimitives.ReadUInt32LittleEndian(buildId.AsSpan(28)) ==
                    timeDateStamp,
                "The COFF timestamp is not bound to the REPRO build identity.");
            RequireUInt32(bytes, 0x944, 1, "extended DLL characteristics");
            return buildId;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(buildId);
            throw;
        }
    }

    private static void AuditCoffGroups(
        ReadOnlySpan<byte> data,
        SectionProfile[] sections)
    {
        // The linker emits the documented COFFGRP flavor as a zero signature
        // followed by variable, four-byte-aligned group records.
        RequireUInt32(data, 0, 0, "COFFGRP signature");
        int cursor = sizeof(uint);
        foreach (CoffGroupProfile expected in ExpectedCoffGroups)
        {
            uint rva = ReadUInt32(data, cursor, "COFFGRP RVA");
            uint size = ReadUInt32(data, cursor + 4, "COFFGRP size");
            cursor = CheckedAdd(cursor, 8, "COFFGRP record");
            int terminator = cursor;
            while (terminator < data.Length && data[terminator] != 0)
            {
                byte value = data[terminator];
                Require(value is >= 0x21 and <= 0x7e,
                    "A COFFGRP name is not printable ASCII.");
                terminator++;
            }

            Require(terminator < data.Length,
                "A COFFGRP name is unterminated.");
            string name = System.Text.Encoding.ASCII.GetString(
                data.Slice(cursor, terminator - cursor));
            cursor = CheckedAdd(terminator, 1, "COFFGRP terminator");
            while ((cursor & 3) != 0)
            {
                RequireByte(data, cursor, 0, "COFFGRP padding");
                cursor++;
            }

            Require(rva == expected.Rva && size == expected.Size &&
                    string.Equals(name, expected.Name, StringComparison.Ordinal),
                "The exact COFFGRP record changed.");
            FindSection(sections, rva, size, "COFFGRP range");
        }

        Require(cursor == data.Length,
            "The COFFGRP record contains trailing or missing data.");
    }

    private static void AuditResources(
        ReadOnlySpan<byte> bytes,
        SectionProfile[] sections,
        ReadOnlySpan<byte> exactManifest)
    {
        int resource = RvaToOffset(sections, 0x4000, 0x260,
            "resource directory");
        RequireResourceDirectoryHeader(bytes, resource, "resource root");
        RequireUInt32(bytes, resource + 16, 24, "resource type ID");
        RequireUInt32(bytes, resource + 20, 0x8000_0018,
            "resource type child");

        RequireResourceDirectoryHeader(bytes, resource + 0x18,
            "manifest-name directory");
        RequireUInt32(bytes, resource + 0x28, 1, "manifest resource ID");
        RequireUInt32(bytes, resource + 0x2c, 0x8000_0030,
            "manifest-name child");

        RequireResourceDirectoryHeader(bytes, resource + 0x30,
            "manifest-language directory");
        RequireUInt32(bytes, resource + 0x40, 0,
            "neutral manifest language ID");
        RequireUInt32(bytes, resource + 0x44, 0x48,
            "manifest data entry");

        RequireUInt32(bytes, resource + 0x48, 0x4060, "manifest data RVA");
        RequireUInt32(bytes, resource + 0x4c,
            checked((uint)exactManifest.Length), "manifest data size");
        RequireUInt32(bytes, resource + 0x50, 0, "manifest code page");
        RequireUInt32(bytes, resource + 0x54, 0, "manifest reserved field");
        RequireAllZero(bytes, resource + 0x58, 8,
            "manifest directory padding");

        int manifestOffset = RvaToOffset(
            sections,
            0x4060,
            checked((uint)exactManifest.Length),
            "manifest bytes");
        Require(bytes.Slice(manifestOffset, exactManifest.Length)
                .SequenceEqual(exactManifest),
            "The embedded manifest does not match the exact caller-supplied bytes.");
        int manifestEnd = CheckedAdd(
            manifestOffset,
            exactManifest.Length,
            "manifest end");
        RequireAllZero(bytes, manifestEnd,
            CheckedAdd(resource, 0x260, "resource end") - manifestEnd,
            "resource padding");
    }

    private static void RequireResourceDirectoryHeader(
        ReadOnlySpan<byte> bytes,
        int offset,
        string field)
    {
        RequireAllZero(bytes, offset, 12, field);
        RequireUInt16(bytes, offset + 12, 0, $"{field} named-entry count");
        RequireUInt16(bytes, offset + 14, 1, $"{field} ID-entry count");
    }

    private static void AuditExceptionDirectory(
        ReadOnlySpan<byte> bytes,
        SectionProfile[] sections)
    {
        int exception = RvaToOffset(sections, 0x3000, 0x0c,
            "exception directory");
        uint begin = ReadUInt32(bytes, exception, "runtime-function begin");
        uint end = ReadUInt32(bytes, exception + 4, "runtime-function end");
        uint unwindRva = ReadUInt32(
            bytes,
            exception + 8,
            "runtime-function unwind RVA");
        Require(begin == 0x1070 && end == 0x10d6 && unwindRva == 0x2348,
            "The exact runtime-function range changed.");
        FindSection(sections, begin, checked(end - begin),
            "runtime-function code range");
        int unwind = RvaToOffset(sections, unwindRva, 8, "unwind record");
        ReadOnlySpan<byte> expected =
            new byte[] { 0x01, 0x06, 0x02, 0x00, 0x06, 0x32, 0x02, 0x30 };
        Require(bytes.Slice(unwind, expected.Length).SequenceEqual(expected),
            "The exact version-1 unwind record changed.");
    }

    private static void AuditChecksum(ReadOnlySpan<byte> bytes)
    {
        uint stored = ReadUInt32(bytes, ChecksumOffset, "PE checksum");
        Require(stored != 0, "The PE checksum is absent.");
        ulong sum = 0;
        for (int offset = 0; offset < bytes.Length; offset += 2)
        {
            if (offset == ChecksumOffset || offset == ChecksumOffset + 2)
            {
                continue;
            }

            uint word = bytes[offset];
            if (offset + 1 < bytes.Length)
            {
                word |= checked((uint)bytes[offset + 1] << 8);
            }

            sum = checked(sum + word);
            sum = (sum & 0xffffUL) + (sum >> 16);
        }

        sum = (sum & 0xffffUL) + (sum >> 16);
        sum = (sum & 0xffffUL) + (sum >> 16);
        uint computed = checked((uint)(sum + checked((uint)bytes.Length)));
        Require(stored == computed, "The PE checksum is invalid.");
    }

    private static SectionProfile FindSection(
        SectionProfile[] sections,
        uint rva,
        uint length,
        string field)
    {
        ulong requestedEnd = checked((ulong)rva + length);
        foreach (SectionProfile section in sections)
        {
            ulong sectionEnd = checked((ulong)section.VirtualAddress +
                section.VirtualSize);
            if (rva >= section.VirtualAddress && requestedEnd <= sectionEnd)
            {
                return section;
            }
        }

        throw new FormatException($"The {field} is outside a mapped section.");
    }

    private static int RvaToOffset(
        SectionProfile[] sections,
        uint rva,
        uint length,
        string field)
    {
        SectionProfile section = FindSection(sections, rva, length, field);
        uint delta = checked(rva - section.VirtualAddress);
        Require(checked((ulong)delta + length) <= section.RawSize,
            $"The {field} is not backed by raw file data.");
        return checked((int)checked(section.RawOffset + delta));
    }

    private static string ReadAsciiZAtRva(
        ReadOnlySpan<byte> bytes,
        SectionProfile[] sections,
        uint rva,
        int maximumLength,
        string field)
    {
        int start = RvaToOffset(sections, rva, 1, field);
        SectionProfile section = FindSection(sections, rva, 1, field);
        int available = checked((int)(section.VirtualSize -
            checked(rva - section.VirtualAddress)));
        int admitted = Math.Min(available, maximumLength);
        int length = 0;
        while (length < admitted && bytes[start + length] != 0)
        {
            byte value = bytes[start + length];
            Require(value is >= 0x21 and <= 0x7e,
                $"The {field} is not printable ASCII.");
            length++;
        }

        Require(length > 0 && length < admitted,
            $"The {field} is empty, unterminated, or too long.");
        return System.Text.Encoding.ASCII.GetString(bytes.Slice(start, length));
    }

    private static string ReadFixedAsciiName(
        ReadOnlySpan<byte> bytes,
        int offset,
        int length,
        string field)
    {
        ReadOnlySpan<byte> encoded = Slice(bytes, offset, length, field);
        int terminator = encoded.IndexOf((byte)0);
        int nameLength = terminator < 0 ? encoded.Length : terminator;
        Require(nameLength > 0, $"The {field} is empty.");
        for (int index = 0; index < nameLength; index++)
        {
            Require(encoded[index] is >= 0x21 and <= 0x7e,
                $"The {field} is not printable ASCII.");
        }

        if (terminator >= 0)
        {
            for (int index = terminator; index < encoded.Length; index++)
            {
                Require(encoded[index] == 0,
                    $"The {field} has nonzero terminator padding.");
            }
        }

        return System.Text.Encoding.ASCII.GetString(encoded[..nameLength]);
    }

    private static void RequireDirectory(
        ReadOnlySpan<byte> bytes,
        int index,
        uint expectedRva,
        uint expectedSize)
    {
        int offset = checked(OptionalOffset + 112 + checked(index * 8));
        RequireUInt32(bytes, offset, expectedRva,
            $"data-directory {index} RVA");
        RequireUInt32(bytes, offset + 4, expectedSize,
            $"data-directory {index} size");
    }

    private static void RequireVersion(
        ReadOnlySpan<byte> bytes,
        int offset,
        ushort major,
        ushort minor,
        string field)
    {
        RequireUInt16(bytes, offset, major, $"{field} major version");
        RequireUInt16(bytes, offset + 2, minor, $"{field} minor version");
    }

    private static void RequireAllZero(
        ReadOnlySpan<byte> bytes,
        int offset,
        int length,
        string field)
    {
        ReadOnlySpan<byte> value = Slice(bytes, offset, length, field);
        foreach (byte item in value)
        {
            if (item != 0)
            {
                throw new FormatException($"The {field} is not zero.");
            }
        }
    }

    private static void RequireByte(
        ReadOnlySpan<byte> bytes,
        int offset,
        byte expected,
        string field)
    {
        byte actual = Slice(bytes, offset, 1, field)[0];
        Require(actual == expected, $"The {field} is unexpected.");
    }

    private static ushort ReadUInt16(
        ReadOnlySpan<byte> bytes,
        int offset,
        string field)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(
            Slice(bytes, offset, sizeof(ushort), field));
    }

    private static uint ReadUInt32(
        ReadOnlySpan<byte> bytes,
        int offset,
        string field)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(
            Slice(bytes, offset, sizeof(uint), field));
    }

    private static ulong ReadUInt64(
        ReadOnlySpan<byte> bytes,
        int offset,
        string field)
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(
            Slice(bytes, offset, sizeof(ulong), field));
    }

    private static void RequireUInt16(
        ReadOnlySpan<byte> bytes,
        int offset,
        ushort expected,
        string field)
    {
        Require(ReadUInt16(bytes, offset, field) == expected,
            $"The {field} is unexpected.");
    }

    private static void RequireUInt32(
        ReadOnlySpan<byte> bytes,
        int offset,
        uint expected,
        string field)
    {
        Require(ReadUInt32(bytes, offset, field) == expected,
            $"The {field} is unexpected.");
    }

    private static void RequireUInt64(
        ReadOnlySpan<byte> bytes,
        int offset,
        ulong expected,
        string field)
    {
        Require(ReadUInt64(bytes, offset, field) == expected,
            $"The {field} is unexpected.");
    }

    private static ReadOnlySpan<byte> Slice(
        ReadOnlySpan<byte> bytes,
        int offset,
        int length,
        string field)
    {
        RequireRange(bytes, offset, length, field);
        return bytes.Slice(offset, length);
    }

    private static void RequireRange(
        ReadOnlySpan<byte> bytes,
        int offset,
        int length,
        string field)
    {
        if (offset < 0 || length < 0)
        {
            throw new FormatException($"The {field} range is negative.");
        }

        int end;
        try
        {
            end = checked(offset + length);
        }
        catch (OverflowException exception)
        {
            throw new FormatException($"The {field} range overflowed.", exception);
        }

        if (end > bytes.Length)
        {
            throw new FormatException($"The {field} range is outside the image.");
        }
    }

    private static int CheckedAdd(int left, int right, string field)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException exception)
        {
            throw new FormatException($"The {field} offset overflowed.", exception);
        }
    }

    private static uint CheckedAddRva(uint left, uint right, string field)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException exception)
        {
            throw new FormatException($"The {field} RVA overflowed.", exception);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new FormatException(message);
        }
    }

    private static byte[] CopyOwned(byte[]? value, string owner)
    {
        if (value is null)
        {
            throw new ObjectDisposedException(owner);
        }

        return (byte[])value.Clone();
    }

    private static void Wipe(ref byte[]? value)
    {
        byte[]? owned = value;
        value = null;
        if (owned is not null)
        {
            CryptographicOperations.ZeroMemory(owned);
        }
    }

    private readonly record struct SectionProfile(
        string Name,
        uint VirtualSize,
        uint VirtualAddress,
        uint RawSize,
        uint RawOffset,
        uint Characteristics);

    private readonly record struct DebugProfile(
        uint Type,
        uint Size,
        uint Rva,
        int RawOffset);

    private readonly record struct CoffGroupProfile(
        uint Rva,
        uint Size,
        string Name);

    private readonly record struct ImportProfile(
        uint NameRva,
        ushort Hint,
        string Name);
}
