using System;
using System.Threading;
using System.Threading.Tasks;

namespace HrcJobObserver.WindowsBootstrap;

/// <summary>
/// One absolute process-local monotonic budget. Implementations must not
/// restart this budget at an asynchronous or native-call boundary.
/// </summary>
internal readonly struct MonotonicDeadline
{
    private readonly TimeProvider provider;
    private readonly long startedTimestamp;
    private readonly TimeSpan lifetime;

    private MonotonicDeadline(
        TimeProvider provider,
        long startedTimestamp,
        TimeSpan lifetime)
    {
        this.provider = provider;
        this.startedTimestamp = startedTimestamp;
        this.lifetime = lifetime;
    }

    internal static MonotonicDeadline Start(
        TimeProvider provider,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        }

        return new MonotonicDeadline(
            provider,
            provider.GetTimestamp(),
            lifetime);
    }

    internal TimeSpan GetRemaining()
    {
        TimeSpan elapsed = GetElapsed();
        TimeSpan remaining = lifetime - elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException(
                "The bootstrap operation deadline expired.");
        }

        return remaining;
    }

    internal bool IsExpired()
    {
        return GetElapsed() >= lifetime;
    }

    internal MonotonicDeadline CapFromNow(TimeSpan maximumLifetime)
    {
        if (maximumLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLifetime));
        }

        long now = provider.GetTimestamp();
        TimeSpan elapsed = GetElapsedAt(now);
        TimeSpan remaining = lifetime - elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException(
                "The bootstrap operation deadline expired.");
        }

        return new MonotonicDeadline(
            provider,
            now,
            remaining < maximumLifetime ? remaining : maximumLifetime);
    }

    private TimeSpan GetElapsed()
    {
        return GetElapsedAt(provider.GetTimestamp());
    }

    private TimeSpan GetElapsedAt(long currentTimestamp)
    {
        TimeSpan elapsed = provider.GetElapsedTime(
            startedTimestamp,
            currentTimestamp);
        if (elapsed < TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "The monotonic time provider moved backwards.");
        }

        return elapsed;
    }
}

internal enum BootstrapPublishStatus
{
    Unknown,
    Published,
    Occupied,
}

internal enum BootstrapPublicationRemovalStatus
{
    Unknown = 0,
    Removed = 1,
    RemovedAfterDeadline = 2,
}

internal readonly record struct BootstrapPublishResult
{
    private BootstrapPublishResult(
        BootstrapPublishStatus status,
        BootstrapPublicationLease? lease)
    {
        Status = status;
        Lease = lease;
    }

    internal BootstrapPublishStatus Status { get; }

    internal BootstrapPublicationLease? Lease { get; }

    internal static BootstrapPublishResult Published(
        BootstrapPublicationLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return new BootstrapPublishResult(
            BootstrapPublishStatus.Published,
            lease);
    }

    internal static BootstrapPublishResult Occupied()
    {
        return new BootstrapPublishResult(
            BootstrapPublishStatus.Occupied,
            null);
    }
}

/// <summary>
/// Broker-side write contract. A successful publish owns any retained bytes
/// before the first suspension and returns the only authority that can remove
/// that exact publication. A failed publish must leave no late mutation.
/// </summary>
internal interface IBootstrapPublicationPublisher
{
    ValueTask<BootstrapPublishResult> TryPublishAsync(
        ReadOnlyMemory<byte> canonicalDescriptor,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken);
}

/// <summary>
/// Store-affine authority for one exact publication. Removal is
/// non-abandonable: it must resolve exact absence or throw an indeterminate
/// failure. A deadline overrun is reported only after absence is verified.
/// </summary>
internal abstract class BootstrapPublicationLease
{
    private readonly object removalGate = new();
    private Task<BootstrapPublicationRemovalStatus>? removalTask;

    protected internal abstract ValueTask<BootstrapPublicationRemovalStatus>
        RemoveExactCoreAsync(MonotonicDeadline deadline);

    internal ValueTask<BootstrapPublicationRemovalStatus> RemoveExactAsync(
        MonotonicDeadline deadline)
    {
        TaskCompletionSource<BootstrapPublicationRemovalStatus>? completion =
            null;
        lock (removalGate)
        {
            if (removalTask is not null)
            {
                return new ValueTask<BootstrapPublicationRemovalStatus>(
                    removalTask);
            }

            completion = new TaskCompletionSource<
                BootstrapPublicationRemovalStatus>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            removalTask = completion.Task;
        }

        _ = CompleteRemovalAsync(completion, deadline);
        return new ValueTask<BootstrapPublicationRemovalStatus>(
            completion.Task);
    }

    private async Task CompleteRemovalAsync(
        TaskCompletionSource<BootstrapPublicationRemovalStatus> completion,
        MonotonicDeadline deadline)
    {
        try
        {
            BootstrapPublicationRemovalStatus result =
                await RemoveExactCoreAsync(deadline).ConfigureAwait(false);
            completion.TrySetResult(result);
        }
        catch (OperationCanceledException exception)
        {
            completion.TrySetException(exception);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }
}
