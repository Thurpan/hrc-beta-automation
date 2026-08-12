using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>Shared canonical binary primitives for bootstrap formats.</summary>
internal static class BootstrapBinary
{
    internal const int UuidLength = 16;
    internal const int Value32Length = 32;
    internal static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal static void ValidateGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "The identifier must not be empty.",
                parameterName);
        }
    }

    internal static void ValidateValue32(
        ReadOnlySpan<byte> value,
        string parameterName)
    {
        if (value.Length != Value32Length)
        {
            throw new ArgumentException(
                "The value must be exactly 32 bytes.",
                parameterName);
        }

        Span<byte> zero = stackalloc byte[Value32Length];
        if (System.Security.Cryptography.CryptographicOperations
            .FixedTimeEquals(value, zero))
        {
            throw new ArgumentException(
                "The value must not be all zero.",
                parameterName);
        }
    }

    internal static byte[] CopyValue32(
        ReadOnlySpan<byte> value,
        string parameterName)
    {
        ValidateValue32(value, parameterName);
        return value.ToArray();
    }

    internal static byte[] CopyExact32(
        ReadOnlySpan<byte> value,
        string parameterName)
    {
        ValidateExact32(value, parameterName);

        return value.ToArray();
    }

    internal static void ValidateExact32(
        ReadOnlySpan<byte> value,
        string parameterName)
    {
        if (value.Length != Value32Length)
        {
            throw new ArgumentException(
                "The value must be exactly 32 bytes.",
                parameterName);
        }
    }

    internal static byte[] EncodeUtf8(
        string value,
        int maximumBytes,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length == 0 || !value.IsNormalized(NormalizationForm.FormC))
        {
            throw new ArgumentException(
                "The value must be non-empty canonical Unicode.",
                parameterName);
        }

        byte[] encoded;
        try
        {
            encoded = StrictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException(
                "The value is not valid Unicode.",
                parameterName,
                exception);
        }

        if (encoded.Length > maximumBytes)
        {
            throw new ArgumentException(
                "The encoded value exceeds its byte limit.",
                parameterName);
        }

        return encoded;
    }

    internal static string DecodeUtf8(
        ReadOnlySpan<byte> encoded,
        int maximumBytes,
        string fieldName)
    {
        if (encoded.Length == 0 || encoded.Length > maximumBytes)
        {
            throw new FormatException(
                $"The {fieldName} length is invalid.");
        }

        string value;
        try
        {
            value = StrictUtf8.GetString(encoded);
        }
        catch (DecoderFallbackException exception)
        {
            throw new FormatException(
                $"The {fieldName} is not canonical UTF-8.",
                exception);
        }

        if (!value.IsNormalized(NormalizationForm.FormC) ||
            !StrictUtf8.GetBytes(value).AsSpan().SequenceEqual(encoded))
        {
            throw new FormatException(
                $"The {fieldName} is not canonical UTF-8.");
        }

        return value;
    }

    internal static void WriteGuid(Stream destination, Guid value)
    {
        Span<byte> encoded = stackalloc byte[UuidLength];
        if (!value.TryWriteBytes(encoded))
        {
            throw new InvalidOperationException("Encoding a UUID failed.");
        }

        encoded[..4].Reverse();
        encoded.Slice(4, 2).Reverse();
        encoded.Slice(6, 2).Reverse();
        destination.Write(encoded);
    }

    internal static Guid ReadGuid(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length != UuidLength)
        {
            throw new FormatException("The UUID length is invalid.");
        }

        Span<byte> dotNetOrder = stackalloc byte[UuidLength];
        encoded.CopyTo(dotNetOrder);
        dotNetOrder[..4].Reverse();
        dotNetOrder.Slice(4, 2).Reverse();
        dotNetOrder.Slice(6, 2).Reverse();
        Guid value = new(dotNetOrder);
        if (value == Guid.Empty)
        {
            throw new FormatException("The UUID must not be empty.");
        }

        return value;
    }

    internal static void WriteUInt16(Stream destination, ushort value)
    {
        Span<byte> encoded = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(encoded, value);
        destination.Write(encoded);
    }

    internal static void WriteUInt32(Stream destination, uint value)
    {
        Span<byte> encoded = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(encoded, value);
        destination.Write(encoded);
    }

    internal static void WriteUInt64(Stream destination, ulong value)
    {
        Span<byte> encoded = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(encoded, value);
        destination.Write(encoded);
    }

    internal static void WriteInt64(Stream destination, long value)
    {
        Span<byte> encoded = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(encoded, value);
        destination.Write(encoded);
    }
}

/// <summary>A bounded reader that rejects truncation and trailing data.</summary>
internal ref struct BootstrapBinaryReader
{
    private readonly ReadOnlySpan<byte> source;
    private int offset;

    internal BootstrapBinaryReader(ReadOnlySpan<byte> source)
    {
        this.source = source;
        offset = 0;
    }

    internal int Remaining => source.Length - offset;

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

    internal byte ReadByte(string fieldName)
    {
        return ReadBytes(1, fieldName)[0];
    }

    internal ushort ReadUInt16(string fieldName)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(
            ReadBytes(sizeof(ushort), fieldName));
    }

    internal uint ReadUInt32(string fieldName)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(
            ReadBytes(sizeof(uint), fieldName));
    }

    internal ulong ReadUInt64(string fieldName)
    {
        return BinaryPrimitives.ReadUInt64BigEndian(
            ReadBytes(sizeof(ulong), fieldName));
    }

    internal long ReadInt64(string fieldName)
    {
        return BinaryPrimitives.ReadInt64BigEndian(
            ReadBytes(sizeof(long), fieldName));
    }

    internal Guid ReadGuid(string fieldName)
    {
        try
        {
            return BootstrapBinary.ReadGuid(
                ReadBytes(BootstrapBinary.UuidLength, fieldName));
        }
        catch (FormatException exception)
        {
            throw new FormatException(
                $"The {fieldName} is invalid.",
                exception);
        }
    }

    internal string ReadUtf8(
        int maximumBytes,
        string fieldName)
    {
        ushort length = ReadUInt16(fieldName + " length");
        return BootstrapBinary.DecodeUtf8(
            ReadBytes(length, fieldName),
            maximumBytes,
            fieldName);
    }

    internal void RequireZero(int length, string fieldName)
    {
        ReadOnlySpan<byte> value = ReadBytes(length, fieldName);
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] != 0)
            {
                throw new FormatException(
                    $"The {fieldName} must be zero.");
            }
        }
    }

    internal void RequireEnd()
    {
        if (Remaining != 0)
        {
            throw new FormatException(
                "The bootstrap value has trailing bytes.");
        }
    }
}
