using System;
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
internal sealed class BootstrapBrokerSession : IDisposable
{
    private readonly object lifecycleGate = new();
    private readonly object arbitrationGate = new();
    private readonly InMemoryBootstrapPublicationStore store;
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
    private BootstrapPublicationRegistration? publicationRegistration;
    private SecretBuffer? brokerToken;
    private BootstrapBrokerSessionState state;
    private ArbitrationWinner winner;
    private int runStarted;
    private bool disposed;

    internal BootstrapBrokerSession(
        BootstrapBinding observerBinding,
        BootstrapBinding controllerBinding,
        BootstrapBinding brokerBinding,
        InMemoryBootstrapPublicationStore store,
        TimeProvider timeProvider,
        TimeSpan publicationLifetime,
        TimeSpan sessionLifetime,
        Func<Task, Task, Task, CancellationToken, Task>?
            beforeArbitrationTestHook = null)
    {
        ArgumentNullException.ThrowIfNull(observerBinding);
        ArgumentNullException.ThrowIfNull(controllerBinding);
        ArgumentNullException.ThrowIfNull(brokerBinding);
        ArgumentNullException.ThrowIfNull(store);
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
        this.store = store;
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

    internal async Task<BootstrapBrokerSessionResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref runStarted, 1) != 0)
        {
            throw new InvalidOperationException(
                "The one-shot broker session was already started.");
        }

        CancellationToken lifetimeToken;
        lock (lifecycleGate)
        {
            if (disposed || lifetimeCancellation is null)
            {
                throw new ObjectDisposedException(
                    nameof(BootstrapBrokerSession));
            }

            state = BootstrapBrokerSessionState.Running;
            lifetimeToken = lifetimeCancellation.Token;
        }

        using CancellationTokenSource operation = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken, lifetimeToken);
        MonotonicDeadline sessionDeadline = MonotonicDeadline.Start(
            timeProvider,
            sessionLifetime);
        try
        {
            BootstrapBrokerSessionResult result = await RunCoreAsync(
                    sessionDeadline,
                    operation.Token)
                .ConfigureAwait(false);
            SetTerminalState(result.Outcome == BootstrapBrokerOutcome.Claimed
                ? BootstrapBrokerSessionState.Claimed
                : BootstrapBrokerSessionState.Revoked);
            return result;
        }
        catch
        {
            SetTerminalState(BootstrapBrokerSessionState.Failed);
            throw;
        }
        finally
        {
            RemovePublicationIfOwned();
            DisposeBrokerToken();
            CloseProtocolPipes();
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? cancellation;
        lock (lifecycleGate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            cancellation = lifetimeCancellation;
            lifetimeCancellation = null;
            if (state is BootstrapBrokerSessionState.Created or
                BootstrapBrokerSessionState.Running)
            {
                state = BootstrapBrokerSessionState.Disposed;
            }
        }

        try
        {
            cancellation?.Cancel();
        }
        finally
        {
            CloseProtocolPipes();
            RemovePublicationIfOwned();
            DisposeBrokerToken();
            cancellation?.Dispose();
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
        MonotonicDeadline publicationDeadline = MonotonicDeadline.Start(
            timeProvider,
            publicationLifetime);
        try
        {
            if (!store.TryPublish(
                    encodedDescriptor,
                    out BootstrapPublicationRegistration? registration) ||
                registration is null)
            {
                throw new InvalidOperationException(
                    "The bootstrap publication store is occupied.");
            }

            publicationRegistration = registration;
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
                    publicationDeadline,
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
            publicationDeadline,
            claimCancellation.Token);
        Task<RevokeRequest> revokeWorker = ReceiveRevokeAsync(
            RequirePipe(revokePipe, "revoke"),
            publicationId,
            descriptorDigest,
            sessionDeadline,
            publicationDeadline,
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
            SelectWinner(ArbitrationWinner.Claim, publicationRegistration);
            revokeCancellation.Cancel();
            await DrainLosingWorkerAsync(
                    revokeWorker,
                    revokeCancellation.Token,
                    sessionDeadline,
                    publicationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
            DisposePipe(ref revokePipe);
            return await CompleteClaimAsync(
                    claim,
                    publicationId,
                    descriptorDigest,
                    sessionDeadline,
                    publicationDeadline,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        RevokeRequest revoke = await revokeWorker.ConfigureAwait(false);
        await RequireCompletedCompetitorValidAsync(claimWorker)
            .ConfigureAwait(false);
        SelectWinner(ArbitrationWinner.Revoke, publicationRegistration);
        claimCancellation.Cancel();
        await DrainLosingWorkerAsync(
                claimWorker,
                claimCancellation.Token,
                sessionDeadline,
                publicationDeadline,
                cancellationToken)
            .ConfigureAwait(false);
        DisposePipe(ref claimPipe);
        return await CompleteRevokeAsync(
                revoke,
                publicationId,
                descriptorDigest,
                sessionDeadline,
                publicationDeadline,
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

    private void SelectWinner(
        ArbitrationWinner candidate,
        BootstrapPublicationRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (arbitrationGate)
        {
            if (winner != ArbitrationWinner.None)
            {
                throw new InvalidOperationException(
                    "The bootstrap publication already has a terminal winner.");
            }

            if (!ReferenceEquals(publicationRegistration, registration) ||
                !store.TryRemove(registration))
            {
                throw new InvalidOperationException(
                    "The exact bootstrap publication could not be removed.");
            }

            publicationRegistration = null;
            winner = candidate;
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
            !sessionDeadline.IsExpired(timeProvider) &&
            !publicationDeadline.IsExpired(timeProvider) &&
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

    private void RemovePublicationIfOwned()
    {
        BootstrapPublicationRegistration? registration =
            publicationRegistration;
        publicationRegistration = null;
        if (registration is not null)
        {
            _ = store.TryRemove(registration);
        }
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
        DisposePipe(ref receiptPipe);
        DisposePipe(ref revokePipe);
        DisposePipe(ref claimPipe);
        DisposePipe(ref publishPipe);
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
        TimeSpan remaining = sessionDeadline.GetRemaining(timeProvider);
        if (publicationDeadline is MonotonicDeadline publication)
        {
            TimeSpan publicationRemaining = publication.GetRemaining(timeProvider);
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

    private readonly record struct MonotonicDeadline(
        long StartedTimestamp,
        TimeSpan Lifetime)
    {
        internal static MonotonicDeadline Start(
            TimeProvider provider,
            TimeSpan lifetime)
        {
            return new MonotonicDeadline(provider.GetTimestamp(), lifetime);
        }

        internal TimeSpan GetRemaining(TimeProvider provider)
        {
            TimeSpan elapsed = provider.GetElapsedTime(
                StartedTimestamp,
                provider.GetTimestamp());
            if (elapsed < TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    "The monotonic time provider moved backwards.");
            }

            TimeSpan remaining = Lifetime - elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException(
                    "The bootstrap session deadline expired.");
            }

            return remaining;
        }

        internal bool IsExpired(TimeProvider provider)
        {
            return provider.GetElapsedTime(
                StartedTimestamp,
                provider.GetTimestamp()) >= Lifetime;
        }
    }
}
