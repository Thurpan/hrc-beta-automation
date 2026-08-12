using System;
using System.Security.Cryptography;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>Owns one non-string secret and wipes its backing array on disposal.</summary>
internal sealed class SecretBuffer : IDisposable
{
    internal const int Length = 32;
    private byte[]? bytes;

    private SecretBuffer(byte[] bytes)
    {
        this.bytes = bytes;
    }

    internal ReadOnlySpan<byte> Bytes => bytes ??
        throw new ObjectDisposedException(nameof(SecretBuffer));

    internal static SecretBuffer CreateRandom32()
    {
        byte[] value = new byte[Length];
        Span<byte> zero = stackalloc byte[Length];
        do
        {
            RandomNumberGenerator.Fill(value);
        }
        while (CryptographicOperations.FixedTimeEquals(
            value,
            zero));
        return new SecretBuffer(value);
    }

    /// <summary>
    /// Copies a caller-owned secret into a separately owned, wipeable buffer.
    /// The caller remains responsible for the source buffer's lifetime.
    /// </summary>
    internal static SecretBuffer CreateOwned(ReadOnlySpan<byte> source)
    {
        if (source.Length != Length)
        {
            throw new ArgumentException(
                "The secret must be exactly 32 bytes.",
                nameof(source));
        }

        Span<byte> zero = stackalloc byte[Length];
        if (CryptographicOperations.FixedTimeEquals(source, zero))
        {
            throw new ArgumentException(
                "The secret must not be all zero.",
                nameof(source));
        }

        return new SecretBuffer(source.ToArray());
    }

    internal void CopyTo(Span<byte> destination)
    {
        byte[] value = bytes ??
            throw new ObjectDisposedException(nameof(SecretBuffer));
        if (destination.Length != Length)
        {
            throw new ArgumentException(
                "The destination must be exactly 32 bytes.",
                nameof(destination));
        }

        value.CopyTo(destination);
    }

    public void Dispose()
    {
        byte[]? value = bytes;
        bytes = null;
        if (value is not null)
        {
            CryptographicOperations.ZeroMemory(value);
        }
    }
}
