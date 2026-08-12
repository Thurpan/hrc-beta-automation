using System;
using System.Security.Cryptography;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Identifies one exact store insertion. The type intentionally exposes no
/// value identity because removal must use the object returned by insertion.
/// </summary>
internal sealed class BootstrapPublicationRegistration
{
}

/// <summary>
/// Owns an independent canonical descriptor snapshot. Disposal wipes the
/// complete snapshot buffer.
/// </summary>
internal sealed class BootstrapPublicationSnapshot : IDisposable
{
    private byte[]? descriptor;

    internal BootstrapPublicationSnapshot(byte[] descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        this.descriptor = descriptor;
    }

    internal ReadOnlySpan<byte> Descriptor => descriptor ??
        throw new ObjectDisposedException(nameof(BootstrapPublicationSnapshot));

    public void Dispose()
    {
        byte[]? owned = descriptor;
        descriptor = null;
        if (owned is not null)
        {
            CryptographicOperations.ZeroMemory(owned);
        }
    }
}

/// <summary>
/// Stores at most one canonical descriptor. Each publication receives an
/// opaque registration so an old owner cannot remove a later equal value.
/// </summary>
internal sealed class InMemoryBootstrapPublicationStore : IDisposable
{
    private readonly object gate = new();
    private Entry? current;
    private bool disposed;

    internal bool TryPublish(
        ReadOnlySpan<byte> descriptor,
        out BootstrapPublicationRegistration? registration)
    {
        byte[] canonical = CanonicalClone(descriptor);
        lock (gate)
        {
            if (disposed)
            {
                CryptographicOperations.ZeroMemory(canonical);
                throw new ObjectDisposedException(
                    nameof(InMemoryBootstrapPublicationStore));
            }

            if (current is not null)
            {
                CryptographicOperations.ZeroMemory(canonical);
                registration = null;
                return false;
            }

            registration = new BootstrapPublicationRegistration();
            current = new Entry(registration, canonical);
            return true;
        }
    }

    internal bool TryRead(out BootstrapPublicationSnapshot? snapshot)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (current is null)
            {
                snapshot = null;
                return false;
            }

            snapshot = new BootstrapPublicationSnapshot(
                current.Descriptor.ToArray());
            return true;
        }
    }

    internal bool TryRemove(BootstrapPublicationRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (gate)
        {
            ThrowIfDisposed();
            if (current is null ||
                !ReferenceEquals(current.Registration, registration))
            {
                return false;
            }

            Entry removed = current;
            current = null;
            removed.Dispose();
            return true;
        }
    }

    public void Dispose()
    {
        Entry? removed;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            removed = current;
            current = null;
        }

        removed?.Dispose();
    }

    private static byte[] CanonicalClone(ReadOnlySpan<byte> descriptor)
    {
        byte[] source = descriptor.ToArray();
        try
        {
            BootstrapDescriptor parsed = BootstrapDescriptor.Parse(source);
            byte[] canonical = parsed.EncodeCanonical();
            if (!canonical.AsSpan().SequenceEqual(source))
            {
                CryptographicOperations.ZeroMemory(canonical);
                throw new ArgumentException(
                    "The descriptor must use its canonical encoding.",
                    nameof(descriptor));
            }

            return canonical;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(source);
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(
                nameof(InMemoryBootstrapPublicationStore));
        }
    }

    private sealed class Entry : IDisposable
    {
        private byte[]? descriptor;

        internal Entry(
            BootstrapPublicationRegistration registration,
            byte[] descriptor)
        {
            Registration = registration;
            this.descriptor = descriptor;
        }

        internal BootstrapPublicationRegistration Registration { get; }

        internal ReadOnlySpan<byte> Descriptor => descriptor ??
            throw new ObjectDisposedException(nameof(Entry));

        public void Dispose()
        {
            byte[]? owned = descriptor;
            descriptor = null;
            if (owned is not null)
            {
                CryptographicOperations.ZeroMemory(owned);
            }
        }
    }
}
