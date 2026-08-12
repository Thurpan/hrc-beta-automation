using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// Identifies one exact store insertion. The type intentionally exposes no
/// value identity because removal must use the object returned by insertion.
/// </summary>
internal sealed class BootstrapPublicationRegistration :
    BootstrapPublicationLease
{
    private readonly InMemoryBootstrapPublicationStore owner;
    private readonly object entryIdentity;

    internal BootstrapPublicationRegistration(
        InMemoryBootstrapPublicationStore owner,
        object entryIdentity)
    {
        this.owner = owner;
        this.entryIdentity = entryIdentity;
    }

    internal InMemoryBootstrapPublicationStore Owner => owner;

    protected internal override ValueTask<BootstrapPublicationRemovalStatus>
        RemoveExactCoreAsync(MonotonicDeadline deadline)
    {
        return owner.RemoveExactAsync(entryIdentity, deadline);
    }
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
internal sealed class InMemoryBootstrapPublicationStore :
    IBootstrapPublicationPublisher,
    IDisposable
{
    private readonly object gate = new();
    private Entry? current;
    private bool disposed;

    public ValueTask<BootstrapPublishResult> TryPublishAsync(
        ReadOnlyMemory<byte> canonicalDescriptor,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.GetRemaining();
        byte[]? canonical = CanonicalClone(canonicalDescriptor.Span);
        try
        {
            lock (gate)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(
                        nameof(InMemoryBootstrapPublicationStore));
                }

                if (current is not null)
                {
                    return ValueTask.FromResult(
                        BootstrapPublishResult.Occupied());
                }

                cancellationToken.ThrowIfCancellationRequested();
                _ = deadline.GetRemaining();
                object identity = new();
                BootstrapPublicationRegistration registration = new(
                    this,
                    identity);
                current = new Entry(identity, registration, canonical);
                canonical = null;
                return ValueTask.FromResult(
                    BootstrapPublishResult.Published(registration));
            }
        }
        finally
        {
            if (canonical is not null)
            {
                CryptographicOperations.ZeroMemory(canonical);
            }
        }
    }

    internal bool TryPublish(
        ReadOnlySpan<byte> descriptor,
        out BootstrapPublicationRegistration? registration)
    {
        byte[] copy = descriptor.ToArray();
        try
        {
            BootstrapPublishResult result = TryPublishAsync(
                    copy,
                    MonotonicDeadline.Start(
                        TimeProvider.System,
                        TimeSpan.FromMinutes(1)),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            registration = result.Lease as BootstrapPublicationRegistration;
            return result.Status == BootstrapPublishStatus.Published;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(copy);
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
        if (!ReferenceEquals(registration.Owner, this) ||
            !Owns(registration))
        {
            return false;
        }
        try
        {
            _ = registration.RemoveExactAsync(
                    MonotonicDeadline.Start(
                        TimeProvider.System,
                        TimeSpan.FromMinutes(1)))
                .GetAwaiter()
                .GetResult();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    internal bool Owns(BootstrapPublicationRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (gate)
        {
            ThrowIfDisposed();
            return current is not null &&
                ReferenceEquals(current.Registration, registration);
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

            if (current is not null)
            {
                throw new InvalidOperationException(
                    "The publication store cannot be disposed while a lease is active.");
            }

            disposed = true;
        }
    }

    internal ValueTask<BootstrapPublicationRemovalStatus> RemoveExactAsync(
        object entryIdentity,
        MonotonicDeadline deadline)
    {
        bool expiredBefore = deadline.IsExpired();
        Entry removed;
        lock (gate)
        {
            ThrowIfDisposed();
            if (current is null ||
                !ReferenceEquals(current.Identity, entryIdentity))
            {
                throw new InvalidOperationException(
                    "The publication lease no longer owns the exact store entry.");
            }

            removed = current;
            current = null;
        }

        removed.Dispose();
        bool expiredAfter = deadline.IsExpired();
        return ValueTask.FromResult(
            expiredBefore || expiredAfter
                ? BootstrapPublicationRemovalStatus.RemovedAfterDeadline
                : BootstrapPublicationRemovalStatus.Removed);
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
            object identity,
            BootstrapPublicationRegistration registration,
            byte[] descriptor)
        {
            Identity = identity;
            Registration = registration;
            this.descriptor = descriptor;
        }

        internal object Identity { get; }

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
