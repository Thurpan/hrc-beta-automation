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
