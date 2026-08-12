using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace HrcJobObserver.WindowsBootstrap;

internal enum BootstrapBrokerOutcome
{
    Claimed,
    Revoked,
}

internal enum BootstrapBrokerSessionState
{
    Created,
    Running,
    Claimed,
    Revoked,
    Failed,
    Disposed,
}

internal sealed record BootstrapBrokerSessionResult(
    BootstrapBrokerOutcome Outcome,
    Guid PublicationId);

/// <summary>
/// Runs one publication and one terminal claim-or-revoke decision. The
/// session never retries an exchange or republishes after uncertainty.
/// </summary>
internal sealed class BootstrapBrokerSession : IDisposable, IAsyncDisposable
{
    private readonly object lifecycleGate = new();
    private readonly object arbitrationGate = new();
    private readonly IBootstrapPublicationPublisher publisher;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan publicationLifetime;
    private readonly TimeSpan sessionLifetime;
    private readonly BootstrapBinding observerBinding;
    private readonly BootstrapBinding controllerBinding;
    private readonly BootstrapBinding brokerBinding;
    private readonly Guid brokerInstanceId;
    private readonly Func<Task, Task, Task, CancellationToken, Task>?
        beforeArbitrationTestHook;
    private CancellationTokenSource? lifetimeCancellation = new();
    private ProtectedNamedPipe? publishPipe;
    private ProtectedNamedPipe? claimPipe;
    private ProtectedNamedPipe? revokePipe;
    private ProtectedNamedPipe? receiptPipe;
    private readonly object publicationGate = new();
    private BootstrapPublicationLease? publicationLease;
    private Task<BootstrapPublicationRemovalStatus>? publicationRemovalTask;
    private MonotonicDeadline? publicationDeadline;
    private Task<Exception?>? cleanupTask;
    private Task<BootstrapBrokerSessionResult>? runTask;
    private Task? disposalTask;
    private SecretBuffer? brokerToken;
    private BootstrapBrokerSessionState state;
    private ArbitrationWinner winner;
    private int runStarted;
    private bool disposed;

    internal BootstrapBrokerSession(
        BootstrapBinding observerBinding,
        BootstrapBinding controllerBinding,
        BootstrapBinding brokerBinding,
        IBootstrapPublicationPublisher publisher,
        TimeProvider timeProvider,
        TimeSpan publicationLifetime,
        TimeSpan sessionLifetime,
        Func<Task, Task, Task, CancellationToken, Task>?
            beforeArbitrationTestHook = null)
    {
        ArgumentNullException.ThrowIfNull(observerBinding);
        ArgumentNullException.ThrowIfNull(controllerBinding);
        ArgumentNullException.ThrowIfNull(brokerBinding);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ValidateLifetime(publicationLifetime, nameof(publicationLifetime));
        ValidateLifetime(sessionLifetime, nameof(sessionLifetime));
        if (publicationLifetime > sessionLifetime)
        {
            throw new ArgumentOutOfRangeException(
                nameof(publicationLifetime),
                "The publication lifetime must not exceed the session lifetime.");
        }

        ValidateRoleBindings(
            observerBinding,
            controllerBinding,
            brokerBinding);
        using ProcessIdentityLease current = ProcessIdentityLease.Capture(
            checked((uint)Environment.ProcessId));
        if (!current.Matches(brokerBinding))
        {
            throw new SecurityException(
                "The broker binding does not identify the current process.");
        }

        this.observerBinding = observerBinding;
        this.controllerBinding = controllerBinding;
        this.brokerBinding = brokerBinding;
        this.publisher = publisher;
        this.timeProvider = timeProvider;
        this.publicationLifetime = publicationLifetime;
        this.sessionLifetime = sessionLifetime;
        this.beforeArbitrationTestHook = beforeArbitrationTestHook;
        brokerInstanceId = Guid.NewGuid();
        publishPipe = ProtectedNamedPipe.Create(observerBinding);
        state = BootstrapBrokerSessionState.Created;
    }

    internal string PublishPipeName
    {
        get
        {
            lock (lifecycleGate)
            {
                return publishPipe?.Name ?? throw new ObjectDisposedException(
                    nameof(BootstrapBrokerSession));
            }
        }
    }

    internal BootstrapBrokerSessionState State
    {
        get
        {
            lock (lifecycleGate)
            {
                return state;
            }
        }
    }

    internal Task<BootstrapBrokerSessionResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref runStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The one-shot broker session was already started.");
        }

        CancellationToken lifetimeToken;
        TaskCompletionSource<object?> startGate = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<BootstrapBrokerSessionResult> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (lifecycleGate)
        {
            if (disposed || lifetimeCancellation is null)
            {
                throw new ObjectDisposedException(
                    nameof(BootstrapBrokerSession));
            }

            state = BootstrapBrokerSessionState.Running;
            lifetimeToken = lifetimeCancellation.Token;
            runTask = completion.Task;
        }

        MonotonicDeadline sessionDeadline = default;
        Exception? setupFailure = null;
        try
        {
            sessionDeadline = MonotonicDeadline.Start(
                timeProvider,
                sessionLifetime);
        }
        catch (Exception exception)
        {
            setupFailure = exception;
        }

        _ = CompleteRunAsync(
            completion,
            startGate.Task,
            sessionDeadline,
            setupFailure,
            cancellationToken,
            lifetimeToken);
        startGate.TrySetResult(null);
        return completion.Task;
    }

    private async Task CompleteRunAsync(
        TaskCompletionSource<BootstrapBrokerSessionResult> completion,
        Task startGate,
        MonotonicDeadline sessionDeadline,
        Exception? setupFailure,
        CancellationToken cancellationToken,
        CancellationToken lifetimeToken)
    {
        try
        {
            await startGate.ConfigureAwait(false);
            BootstrapBrokerSessionResult result = await RunManagedAsync(
                    sessionDeadline,
                    setupFailure,
                    cancellationToken,
                    lifetimeToken)
                .ConfigureAwait(false);
            completion.TrySetResult(result);
        }
        catch (OperationCanceledException exception)
        {
            completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private async Task<BootstrapBrokerSessionResult> RunManagedAsync(
        MonotonicDeadline sessionDeadline,
        Exception? setupFailure,
        CancellationToken cancellationToken,
        CancellationToken lifetimeToken)
    {
        BootstrapBrokerSessionResult? result = null;
        Exception? primaryFailure = setupFailure;
        if (primaryFailure is null)
        {
            try
            {
                using CancellationTokenSource operation = CancellationTokenSource
                    .CreateLinkedTokenSource(cancellationToken, lifetimeToken);
                result = await RunCoreAsync(
                        sessionDeadline,
                        operation.Token)
                    .ConfigureAwait(false);
                SetTerminalState(result.Outcome == BootstrapBrokerOutcome.Claimed
                    ? BootstrapBrokerSessionState.Claimed
                    : BootstrapBrokerSessionState.Revoked);
            }
            catch (Exception exception)
            {
                primaryFailure = exception;
            }
        }

        if (primaryFailure is not null)
        {
            SetTerminalState(BootstrapBrokerSessionState.Failed);
        }

        Exception? cleanupFailure = await GetOrStartCleanupAsync()
            .ConfigureAwait(false);
        if (primaryFailure is not null)
        {
            if (cleanupFailure is not null)
            {
                throw new AggregateException(
                    "The bootstrap broker failed and terminal cleanup also failed.",
                    primaryFailure,
                    cleanupFailure);
            }

            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }

        if (cleanupFailure is not null)
        {
            SetTerminalState(BootstrapBrokerSessionState.Failed);
            ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }

        return result ?? throw new InvalidOperationException(
            "The bootstrap broker completed without a terminal result.");
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource<object?> completion;
        TaskCompletionSource<object?> startGate = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lock (lifecycleGate)
        {
            if (disposalTask is not null)
            {
                return new ValueTask(disposalTask);
            }

            disposed = true;
            if (state is BootstrapBrokerSessionState.Created or
                BootstrapBrokerSessionState.Running)
            {
                state = BootstrapBrokerSessionState.Disposed;
            }

            completion = new TaskCompletionSource<object?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            disposalTask = completion.Task;
        }

        _ = CompleteDisposalAsync(completion, startGate.Task);
        startGate.TrySetResult(null);
        return new ValueTask(completion.Task);
    }

    private async Task CompleteDisposalAsync(
        TaskCompletionSource<object?> completion,
        Task startGate)
    {
        try
        {
            await startGate.ConfigureAwait(false);
            await DisposeManagedAsync().ConfigureAwait(false);
            completion.TrySetResult(null);
        }
        catch (OperationCanceledException exception)
        {
            // DisposeAsync has no caller cancellation contract. An OCE here
            // is a cleanup/disposal failure and must remain faulted.
            completion.TrySetException(exception);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private async Task DisposeManagedAsync()
    {
        CancellationTokenSource? cancellation;
        Task<BootstrapBrokerSessionResult>? running;
        lock (lifecycleGate)
        {
            cancellation = lifetimeCancellation;
            lifetimeCancellation = null;
            running = runTask;
        }

        Exception? disposalFailure = null;
        Exception? cancellationRequestFailure = null;
        try
        {
            try
            {
                cancellation?.Cancel();
            }
            catch (Exception exception)
            {
                cancellationRequestFailure = exception;
            }

            if (running is not null)
            {
                try
                {
                    _ = await running.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // RunAsync remains the authoritative protocol-failure
                    // channel. Disposal waits for its terminal cleanup.
                }
            }
        }
        finally
        {
            // RunAsync is authoritative for protocol failures, but disposal
            // independently promises terminal cleanup. Observe the shared
            // cleanup result even when RunAsync already reported a failure.
            disposalFailure = await GetOrStartCleanupAsync()
                .ConfigureAwait(false);

            Exception? cancellationFailure = null;
            try
            {
                cancellation?.Dispose();
            }
            catch (Exception exception)
            {
                cancellationFailure = exception;
            }

            List<Exception> failures = new();
            if (cancellationRequestFailure is not null)
            {
                failures.Add(cancellationRequestFailure);
            }

            if (disposalFailure is not null)
            {
                failures.Add(disposalFailure);
            }

            if (cancellationFailure is not null)
            {
                failures.Add(cancellationFailure);
            }

            if (failures.Count == 1)
            {
                ExceptionDispatchInfo.Capture(failures[0]).Throw();
            }

            if (failures.Count > 1)
            {
                throw new AggregateException(
                    "Bootstrap disposal encountered multiple failures.",
                    failures);
            }
        }
    }

    private async Task<BootstrapBrokerSessionResult> RunCoreAsync(
        MonotonicDeadline sessionDeadline,
        CancellationToken cancellationToken)
    {
        ProtectedNamedPipe publish = RequirePipe(publishPipe, "publish");
        await AcceptAsync(
                publish,
                sessionDeadline,
                publicationDeadline: null,
                cancellationToken)
            .ConfigureAwait(false);
        byte[] publishFrame = await ReceiveAsync(
                publish,
                sessionDeadline,
                publicationDeadline: null,
                cancellationToken)
            .ConfigureAwait(false);
        using PublishRequest request = Decode<PublishRequest>(
            publishFrame,
            BootstrapMessageType.PublishRequest,
            BootstrapRole.Observer,
            BootstrapRole.Broker);

        brokerToken = SecretBuffer.CreateOwned(request.Token.Bytes);
        request.DisposeSecret();
        claimPipe = ProtectedNamedPipe.Create(controllerBinding);
        revokePipe = ProtectedNamedPipe.Create(observerBinding);

        DateTimeOffset createdUtc = CanonicalUtc(timeProvider.GetUtcNow());
        DateTimeOffset expiresUtc = createdUtc + publicationLifetime;
        Guid publicationId = Guid.NewGuid();
        BootstrapDescriptor descriptor = BootstrapDescriptor.Create(
            createdUtc,
            expiresUtc,
            publicationId,
            brokerInstanceId,
            request.PublicationNonce,
            request.Endpoint,
            claimPipe.Name,
            observerBinding,
            brokerBinding,
            brokerToken.Bytes,
            publicationLifetime);
        byte[] encodedDescriptor = descriptor.EncodeCanonical();
        byte[] descriptorDigest = SHA256.HashData(encodedDescriptor);
        MonotonicDeadline activePublicationDeadline =
            sessionDeadline.CapFromNow(publicationLifetime);
        publicationDeadline = activePublicationDeadline;
        try
        {
            BootstrapPublishResult publication = await publisher.TryPublishAsync(
                    encodedDescriptor,
                    activePublicationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            if (publication.Status == BootstrapPublishStatus.Occupied &&
                publication.Lease is null)
            {
                throw new InvalidOperationException(
                    "The bootstrap publication store is occupied.");
            }

            if (publication.Status != BootstrapPublishStatus.Published ||
                publication.Lease is null)
            {
                throw new InvalidOperationException(
                    "The bootstrap publication publisher returned an invalid result.");
            }

            await InstallPublicationLeaseAsync(
                    publication.Lease,
                    activePublicationDeadline)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            using SensitiveFrame acknowledgement = BootstrapProtocol.Encode(
                new PublishAck(
                    request.RequestId,
                    publicationId,
                    descriptorDigest,
                    encodedDescriptor,
                    revokePipe.Name));
            await SendAsync(
                    publish,
                    acknowledgement.Bytes,
                    sessionDeadline,
                    activePublicationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encodedDescriptor);
        }

        DisposePipe(ref publishPipe);

        using CancellationTokenSource claimCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using CancellationTokenSource revokeCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task<ClaimRequest> claimWorker = ReceiveClaimAsync(
            RequirePipe(claimPipe, "claim"),
            publicationId,
            descriptorDigest,
            sessionDeadline,
            activePublicationDeadline,
            claimCancellation.Token);
        Task<RevokeRequest> revokeWorker = ReceiveRevokeAsync(
            RequirePipe(revokePipe, "revoke"),
            publicationId,
            descriptorDigest,
            sessionDeadline,
            activePublicationDeadline,
            revokeCancellation.Token);

        Task completed = await Task.WhenAny(claimWorker, revokeWorker)
            .ConfigureAwait(false);
        if (beforeArbitrationTestHook is not null)
        {
            await beforeArbitrationTestHook(
                    claimWorker,
                    revokeWorker,
                    completed,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (ReferenceEquals(completed, claimWorker))
        {
            ClaimRequest claim = await claimWorker.ConfigureAwait(false);
            await RequireCompletedCompetitorValidAsync(revokeWorker)
                .ConfigureAwait(false);
            await SelectWinnerAsync(
                    ArbitrationWinner.Claim,
                    activePublicationDeadline)
                .ConfigureAwait(false);
            revokeCancellation.Cancel();
            await DrainLosingWorkerAsync(
                    revokeWorker,
                    revokeCancellation.Token,
                    sessionDeadline,
                    activePublicationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            DisposePipe(ref revokePipe);
            return await CompleteClaimAsync(
                    claim,
                    publicationId,
                    descriptorDigest,
                    sessionDeadline,
                    activePublicationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        RevokeRequest revoke = await revokeWorker.ConfigureAwait(false);
        await RequireCompletedCompetitorValidAsync(claimWorker)
            .ConfigureAwait(false);
        await SelectWinnerAsync(
                ArbitrationWinner.Revoke,
                activePublicationDeadline)
            .ConfigureAwait(false);
        claimCancellation.Cancel();
        await DrainLosingWorkerAsync(
                claimWorker,
                claimCancellation.Token,
                sessionDeadline,
                activePublicationDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        DisposePipe(ref claimPipe);
        return await CompleteRevokeAsync(
                revoke,
                publicationId,
                descriptorDigest,
                sessionDeadline,
                activePublicationDeadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<BootstrapBrokerSessionResult> CompleteClaimAsync(
        ClaimRequest claim,
        Guid publicationId,
        ReadOnlyMemory<byte> descriptorDigest,
        MonotonicDeadline sessionDeadline,
        MonotonicDeadline publicationDeadline,
        CancellationToken cancellationToken)
    {
        SecretBuffer token = RequireBrokerToken();
        byte[] receiptNonce = RandomValue32();
        try
        {
            receiptPipe = ProtectedNamedPipe.Create(controllerBinding);
            await SendClaimGrantAsync(
                    claim,
                    publicationId,
                    descriptorDigest,
                    receiptNonce,
                    token,
                    sessionDeadline,
                    publicationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            DisposePipe(ref claimPipe);

            ProtectedNamedPipe receipt = RequirePipe(receiptPipe, "receipt");
            await AcceptAsync(
                    receipt,
                    sessionDeadline,
                    publicationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            byte[] receiptFrame = await ReceiveAsync(
                    receipt,
                    sessionDeadline,
                    publicationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            using (ClaimReceipt accepted = Decode<ClaimReceipt>(
                       receiptFrame,
                       BootstrapMessageType.ClaimReceipt,
                       BootstrapRole.Controller,
                       BootstrapRole.Broker))
            {
                ValidateReceipt(
                    accepted,
                    claim,
                    publicationId,
                    descriptorDigest.Span,
                    receiptNonce,
                    token.Bytes);
            }

            DisposeBrokerToken();
            using SensitiveFrame final = BootstrapProtocol.Encode(
                new ClaimFinalAck(
                    claim.RequestId,
                    publicationId,
                    descriptorDigest.Span,
                    claim.ControllerNonce,
                    receiptNonce));
            await SendAsync(
                    receipt,
                    final.Bytes,
                    sessionDeadline,
                    publicationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            return new BootstrapBrokerSessionResult(
                BootstrapBrokerOutcome.Claimed,
                publicationId);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(receiptNonce);
        }
    }

    private async Task SendClaimGrantAsync(
        ClaimRequest claim,
        Guid publicationId,
        ReadOnlyMemory<byte> descriptorDigest,
        ReadOnlyMemory<byte> receiptNonce,
        SecretBuffer token,
        MonotonicDeadline sessionDeadline,
        MonotonicDeadline publicationDeadline,
        CancellationToken cancellationToken)
    {
        using ClaimGrant grant = new(
            claim.RequestId,
            publicationId,
            descriptorDigest.Span,
            claim.ControllerNonce,
            receiptNonce.Span,
            RequirePipe(receiptPipe, "receipt").Name,
            token.Bytes);
        using SensitiveFrame grantFrame = BootstrapProtocol.Encode(grant);
        await SendAsync(
                RequirePipe(claimPipe, "claim"),
                grantFrame.Bytes,
                sessionDeadline,
                publicationDeadline,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<BootstrapBrokerSessionResult> CompleteRevokeAsync(
        RevokeRequest revoke,
        Guid publicationId,
        ReadOnlyMemory<byte> descriptorDigest,
        MonotonicDeadline sessionDeadline,
        MonotonicDeadline publicationDeadline,
        CancellationToken cancellationToken)
    {
        DisposeBrokerToken();
        using SensitiveFrame acknowledgement = BootstrapProtocol.Encode(
            new RevokeAck(
                revoke.RequestId,
                publicationId,
                descriptorDigest.Span,
                revoke.RevocationNonce));
        await SendAsync(
                RequirePipe(revokePipe, "revoke"),
                acknowledgement.Bytes,
                sessionDeadline,
                publicationDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        return new BootstrapBrokerSessionResult(
            BootstrapBrokerOutcome.Revoked,
            publicationId);
    }

    private async Task<ClaimRequest> ReceiveClaimAsync(
        ProtectedNamedPipe pipe,
        Guid publicationId,
        ReadOnlyMemory<byte> descriptorDigest,
        MonotonicDeadline sessionDeadline,
        MonotonicDeadline publicationDeadline,
        CancellationToken cancellationToken)
    {
        await AcceptAsync(
                pipe,
                sessionDeadline,
                publicationDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        byte[] frame = await ReceiveAsync(
                pipe,
                sessionDeadline,
                publicationDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        ClaimRequest claim = Decode<ClaimRequest>(
            frame,
            BootstrapMessageType.ClaimRequest,
            BootstrapRole.Controller,
            BootstrapRole.Broker);
        ValidateClaim(claim, publicationId, descriptorDigest.Span);
        return claim;
    }

    private async Task<RevokeRequest> ReceiveRevokeAsync(
        ProtectedNamedPipe pipe,
        Guid publicationId,
        ReadOnlyMemory<byte> descriptorDigest,
        MonotonicDeadline sessionDeadline,
        MonotonicDeadline publicationDeadline,
        CancellationToken cancellationToken)
    {
        await AcceptAsync(
                pipe,
                sessionDeadline,
                publicationDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        byte[] frame = await ReceiveAsync(
                pipe,
                sessionDeadline,
                publicationDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        RevokeRequest revoke = Decode<RevokeRequest>(
            frame,
            BootstrapMessageType.RevokeRequest,
            BootstrapRole.Observer,
            BootstrapRole.Broker);
        ValidateRevoke(revoke, publicationId, descriptorDigest.Span);
        return revoke;
    }

    private static async Task RequireCompletedCompetitorValidAsync<T>(
        Task<T> competitor)
    {
        if (competitor.IsCompleted)
        {
            _ = await competitor.ConfigureAwait(false);
        }
    }

    private async Task SelectWinnerAsync(
        ArbitrationWinner candidate,
        MonotonicDeadline deadline)
    {
        lock (arbitrationGate)
        {
            if (winner != ArbitrationWinner.None)
            {
                throw new InvalidOperationException(
                    "The bootstrap publication already has a terminal winner.");
            }

            winner = candidate;
        }

        BootstrapPublicationRemovalStatus removal =
            await RemovePublicationIfOwnedAsync(deadline)
                .ConfigureAwait(false);
        if (removal == BootstrapPublicationRemovalStatus.RemovedAfterDeadline)
        {
            throw new TimeoutException(
                "The exact bootstrap publication was removed after its deadline.");
        }

        if (removal != BootstrapPublicationRemovalStatus.Removed)
        {
            throw new InvalidOperationException(
                "The exact bootstrap publication returned an unknown removal status.");
        }
    }

    private async Task DrainLosingWorkerAsync<T>(
        Task<T> worker,
        CancellationToken losingWorkerCancellation,
        MonotonicDeadline sessionDeadline,
        MonotonicDeadline publicationDeadline,
        CancellationToken cancellationToken)
    {
        TimeSpan remaining = GetRemaining(
            sessionDeadline,
            publicationDeadline);
        try
        {
            _ = await worker.WaitAsync(
                    remaining,
                    timeProvider,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            losingWorkerCancellation.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested &&
            !sessionDeadline.IsExpired() &&
            !publicationDeadline.IsExpired() &&
            exception is OperationCanceledException)
        {
            // The selected winner explicitly cancelled the other worker.
        }
    }

    private async Task AcceptAsync(
        ProtectedNamedPipe pipe,
        MonotonicDeadline sessionDeadline,
        MonotonicDeadline? publicationDeadline,
        CancellationToken cancellationToken)
    {
        TimeSpan remaining = GetRemaining(sessionDeadline, publicationDeadline);
        using CancellationTokenSource deadline = new(remaining, timeProvider);
        using CancellationTokenSource operation = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            await pipe.AcceptAndAuthenticateAsync(
                    remaining,
                    operation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested &&
                deadline.IsCancellationRequested)
        {
            throw new TimeoutException(
                "The bootstrap session deadline expired during accept.",
                exception);
        }
    }

    private async Task<byte[]> ReceiveAsync(
        ProtectedNamedPipe pipe,
        MonotonicDeadline sessionDeadline,
        MonotonicDeadline? publicationDeadline,
        CancellationToken cancellationToken)
    {
        TimeSpan remaining = GetRemaining(sessionDeadline, publicationDeadline);
        using CancellationTokenSource deadline = new(remaining, timeProvider);
        using CancellationTokenSource operation = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            return await pipe.ReceiveFrameAsync(
                    remaining,
                    operation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested &&
                deadline.IsCancellationRequested)
        {
            throw new TimeoutException(
                "The bootstrap session deadline expired during receive.",
                exception);
        }
    }

    private async Task SendAsync(
        ProtectedNamedPipe pipe,
        ReadOnlyMemory<byte> frame,
        MonotonicDeadline sessionDeadline,
        MonotonicDeadline? publicationDeadline,
        CancellationToken cancellationToken)
    {
        TimeSpan remaining = GetRemaining(sessionDeadline, publicationDeadline);
        using CancellationTokenSource deadline = new(remaining, timeProvider);
        using CancellationTokenSource operation = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            await pipe.SendFrameAsync(
                    frame,
                    remaining,
                    operation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested &&
                deadline.IsCancellationRequested)
        {
            throw new TimeoutException(
                "The bootstrap session deadline expired during send.",
                exception);
        }
    }

    private static T Decode<T>(
        byte[] frame,
        BootstrapMessageType messageType,
        BootstrapRole sender,
        BootstrapRole receiver)
        where T : class
    {
        object decoded = BootstrapProtocol.DecodeOwned(
            frame,
            messageType,
            sender,
            receiver);
        if (decoded is not T value)
        {
            if (decoded is IDisposable disposable)
            {
                disposable.Dispose();
            }

            throw new FormatException(
                "The bootstrap decoder returned an unexpected message type.");
        }

        return value;
    }

    private static void ValidateClaim(
        ClaimRequest claim,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest)
    {
        if (claim.PublicationId != publicationId ||
            !CryptographicOperations.FixedTimeEquals(
                claim.DescriptorDigest,
                descriptorDigest))
        {
            throw new SecurityException(
                "The claim request does not match the exact publication.");
        }
    }

    private static void ValidateRevoke(
        RevokeRequest revoke,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest)
    {
        if (revoke.PublicationId != publicationId ||
            !CryptographicOperations.FixedTimeEquals(
                revoke.DescriptorDigest,
                descriptorDigest))
        {
            throw new SecurityException(
                "The revoke request does not match the exact publication.");
        }
    }

    private static void ValidateReceipt(
        ClaimReceipt receipt,
        ClaimRequest claim,
        Guid publicationId,
        ReadOnlySpan<byte> descriptorDigest,
        ReadOnlySpan<byte> receiptNonce,
        ReadOnlySpan<byte> token)
    {
        bool transcriptMatches =
            receipt.RequestId == claim.RequestId &&
            receipt.PublicationId == publicationId &&
            CryptographicOperations.FixedTimeEquals(
                receipt.DescriptorDigest,
                descriptorDigest) &&
            CryptographicOperations.FixedTimeEquals(
                receipt.ControllerNonce,
                claim.ControllerNonce) &&
            CryptographicOperations.FixedTimeEquals(
                receipt.ReceiptNonce,
                receiptNonce);
        if (!transcriptMatches ||
            !BootstrapProtocol.VerifyClaimReceiptProof(
                token,
                publicationId,
                descriptorDigest,
                claim.ControllerNonce,
                receiptNonce,
                receipt.PossessionProof))
        {
            throw new SecurityException(
                "The claim receipt transcript or possession proof is invalid.");
        }
    }

    private async Task InstallPublicationLeaseAsync(
        BootstrapPublicationLease lease,
        MonotonicDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(lease);
        bool reject;
        lock (lifecycleGate)
        {
            reject = disposed || lifetimeCancellation is null;
            lock (publicationGate)
            {
                if (publicationLease is not null ||
                    publicationRemovalTask is not null)
                {
                    throw new InvalidOperationException(
                        "A bootstrap publication lease is already installed.");
                }

                publicationLease = lease;
            }
        }

        if (reject)
        {
            BootstrapPublicationRemovalStatus removal =
                await RemovePublicationIfOwnedAsync(deadline)
                    .ConfigureAwait(false);
            if (removal ==
                BootstrapPublicationRemovalStatus.RemovedAfterDeadline)
            {
                throw new TimeoutException(
                    "A publication committed during disposal and was removed after its deadline.");
            }

            throw new ObjectDisposedException(
                nameof(BootstrapBrokerSession),
                "The session was disposed before publication ownership could be installed.");
        }
    }

    private Task<BootstrapPublicationRemovalStatus>
        RemovePublicationIfOwnedAsync(MonotonicDeadline deadline)
    {
        TaskCompletionSource<BootstrapPublicationRemovalStatus>? completion =
            null;
        BootstrapPublicationLease? owned = null;
        lock (publicationGate)
        {
            if (publicationRemovalTask is not null)
            {
                return publicationRemovalTask;
            }

            if (publicationLease is null)
            {
                return Task.FromResult(
                    BootstrapPublicationRemovalStatus.Removed);
            }

            owned = publicationLease;
            completion = new TaskCompletionSource<
                BootstrapPublicationRemovalStatus>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            publicationRemovalTask = completion.Task;
        }

        _ = CompleteInstalledLeaseRemovalAsync(
            completion,
            owned,
            deadline);
        return completion.Task;
    }

    private async Task CompleteInstalledLeaseRemovalAsync(
        TaskCompletionSource<BootstrapPublicationRemovalStatus> completion,
        BootstrapPublicationLease lease,
        MonotonicDeadline deadline)
    {
        try
        {
            BootstrapPublicationRemovalStatus result =
                await RemoveInstalledLeaseAsync(lease, deadline)
                    .ConfigureAwait(false);
            completion.TrySetResult(result);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private async Task<BootstrapPublicationRemovalStatus>
        RemoveInstalledLeaseAsync(
            BootstrapPublicationLease lease,
            MonotonicDeadline deadline)
    {
        BootstrapPublicationRemovalStatus result =
            await lease.RemoveExactAsync(deadline).ConfigureAwait(false);
        if (result is not BootstrapPublicationRemovalStatus.Removed and
            not BootstrapPublicationRemovalStatus.RemovedAfterDeadline)
        {
            throw new InvalidOperationException(
                "The publication lease returned an unknown removal status.");
        }

        lock (publicationGate)
        {
            if (ReferenceEquals(publicationLease, lease))
            {
                publicationLease = null;
            }
        }

        return result;
    }

    private Task<Exception?> GetOrStartCleanupAsync()
    {
        TaskCompletionSource<Exception?>? completion = null;
        lock (publicationGate)
        {
            if (cleanupTask is not null)
            {
                return cleanupTask;
            }

            completion = new TaskCompletionSource<Exception?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            cleanupTask = completion.Task;
        }

        _ = CompleteCleanupAsync(completion);
        return completion.Task;
    }

    private async Task CompleteCleanupAsync(
        TaskCompletionSource<Exception?> completion)
    {
        try
        {
            Exception? result = await TryCleanupCoreAsync()
                .ConfigureAwait(false);
            completion.TrySetResult(result);
        }
        catch (Exception exception)
        {
            // The cleanup task carries failures as its result so RunAsync can
            // preserve an independent primary protocol failure alongside it.
            completion.TrySetResult(exception);
        }
    }

    private async Task<Exception?> TryCleanupCoreAsync()
    {
        List<Exception> failures = new();
        Task<BootstrapPublicationRemovalStatus>? removalTask = null;
        MonotonicDeadline? deadline = publicationDeadline;
        if (deadline is MonotonicDeadline removalDeadline)
        {
            try
            {
                removalTask = RemovePublicationIfOwnedAsync(removalDeadline);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        try
        {
            DisposeBrokerToken();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            CloseProtocolPipes();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        if (removalTask is not null)
        {
            try
            {
                BootstrapPublicationRemovalStatus removal =
                    await removalTask.ConfigureAwait(false);
                if (removal ==
                    BootstrapPublicationRemovalStatus.RemovedAfterDeadline)
                {
                    failures.Add(new TimeoutException(
                        "Publication removal completed after its deadline."));
                }
                else if (removal !=
                    BootstrapPublicationRemovalStatus.Removed)
                {
                    failures.Add(new InvalidOperationException(
                        "Publication cleanup returned an unknown removal status."));
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        return failures.Count switch
        {
            0 => null,
            1 => failures[0],
            _ => new AggregateException(
                "Multiple terminal bootstrap cleanup operations failed.",
                failures),
        };
    }

    private void DisposeBrokerToken()
    {
        SecretBuffer? token = brokerToken;
        brokerToken = null;
        token?.Dispose();
    }

    private SecretBuffer RequireBrokerToken()
    {
        return brokerToken ?? throw new InvalidOperationException(
            "The broker no longer owns a bearer token.");
    }

    private void CloseProtocolPipes()
    {
        List<Exception> failures = new();
        DisposePipeCollect(ref receiptPipe, failures);
        DisposePipeCollect(ref revokePipe, failures);
        DisposePipeCollect(ref claimPipe, failures);
        DisposePipeCollect(ref publishPipe, failures);
        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(
                "Multiple bootstrap pipe closures failed.",
                failures);
        }
    }

    private static void DisposePipeCollect(
        ref ProtectedNamedPipe? pipe,
        List<Exception> failures)
    {
        try
        {
            DisposePipe(ref pipe);
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }

    private static void DisposePipe(ref ProtectedNamedPipe? pipe)
    {
        ProtectedNamedPipe? owned = Interlocked.Exchange(ref pipe, null);
        owned?.Dispose();
    }

    private static ProtectedNamedPipe RequirePipe(
        ProtectedNamedPipe? pipe,
        string phase)
    {
        return pipe ?? throw new ObjectDisposedException(
            nameof(BootstrapBrokerSession),
            $"The {phase} pipe is not available.");
    }

    private void SetTerminalState(BootstrapBrokerSessionState terminal)
    {
        lock (lifecycleGate)
        {
            if (!disposed)
            {
                state = terminal;
            }
        }
    }

    private TimeSpan GetRemaining(
        MonotonicDeadline sessionDeadline,
        MonotonicDeadline? publicationDeadline)
    {
        TimeSpan remaining = sessionDeadline.GetRemaining();
        if (publicationDeadline is MonotonicDeadline publication)
        {
            TimeSpan publicationRemaining = publication.GetRemaining();
            if (publicationRemaining < remaining)
            {
                remaining = publicationRemaining;
            }
        }

        return remaining <= ProtectedNamedPipe.MaximumOperationTime
            ? remaining
            : ProtectedNamedPipe.MaximumOperationTime;
    }

    private static DateTimeOffset CanonicalUtc(DateTimeOffset value)
    {
        DateTimeOffset utc = value.ToUniversalTime();
        long ticks = utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private static byte[] RandomValue32()
    {
        using SecretBuffer value = SecretBuffer.CreateRandom32();
        byte[] result = new byte[SecretBuffer.Length];
        value.CopyTo(result);
        return result;
    }

    private static void ValidateLifetime(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero ||
            value > ProtectedNamedPipe.MaximumOperationTime)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void ValidateRoleBindings(
        BootstrapBinding observer,
        BootstrapBinding controller,
        BootstrapBinding broker)
    {
        if (observer.ProcessId == controller.ProcessId ||
            observer.ProcessId == broker.ProcessId ||
            controller.ProcessId == broker.ProcessId)
        {
            throw new ArgumentException(
                "The observer, controller, and broker must be distinct processes.");
        }

        ValidateSharedContext(observer, broker, "observer");
        ValidateSharedContext(controller, broker, "controller");
    }

    private static void ValidateSharedContext(
        BootstrapBinding role,
        BootstrapBinding broker,
        string roleName)
    {
        if (!string.Equals(role.UserSid, broker.UserSid, StringComparison.Ordinal) ||
            !string.Equals(role.LogonSid, broker.LogonSid, StringComparison.Ordinal) ||
            role.TokenSessionId != broker.TokenSessionId ||
            role.ProcessSessionId != broker.ProcessSessionId)
        {
            throw new ArgumentException(
                $"The {roleName} and broker must share one security context.");
        }
    }

    private enum ArbitrationWinner
    {
        None,
        Claim,
        Revoke,
    }

}
