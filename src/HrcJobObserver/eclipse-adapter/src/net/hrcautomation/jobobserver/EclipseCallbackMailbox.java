package net.hrcautomation.jobobserver;

import java.time.Duration;
import java.util.Objects;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Semaphore;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicLong;
import java.util.concurrent.atomic.AtomicReference;

/**
 * Fixed-capacity, single-use bridge between Eclipse callbacks and the core.
 * Callbacks acquire an entry lease before reading callback data, then reserve
 * and complete a numbered slot without waiting. One daemon worker dispatches
 * completed slots in ticket order and is the sole callback-path
 * {@link ObserverIngress} caller.
 */
final class EclipseCallbackMailbox {
    private static final long ADMISSIONS_ACTIVE = Long.MIN_VALUE;
    private static final long FAILURE_PENDING = 1L << 62;
    private static final long LEASE_COUNT_MASK = FAILURE_PENDING - 1;

    private enum State {
        NEW,
        ACTIVE,
        CLOSING,
        CLOSED
    }

    private final int capacity;
    private final ObserverIngress ingress;
    private final ConcurrentHashMap<Long, CallbackEnvelope> completed =
            new ConcurrentHashMap<>();
    private final ConcurrentHashMap<Long, Boolean> openTickets =
            new ConcurrentHashMap<>();
    private final Semaphore wakeups = new Semaphore(0);
    private final AtomicBoolean wakePending = new AtomicBoolean();
    private final AtomicInteger reserved = new AtomicInteger();
    private final AtomicLong admissionGate = new AtomicLong();
    private final AtomicLong nextCallbackTicket = new AtomicLong(1);
    private final AtomicReference<InfrastructureIncident> firstFailure =
            new AtomicReference<>();
    private final AtomicReference<State> state = new AtomicReference<>(State.NEW);
    private final AtomicBoolean failureNotificationAttempted = new AtomicBoolean();
    private final AtomicBoolean failureNotificationSucceeded = new AtomicBoolean();
    private final CountDownLatch workerTerminated = new CountDownLatch(1);
    private final Runnable admissionReadProbe;
    private final Thread worker;

    EclipseCallbackMailbox(int capacity, ObserverIngress ingress) {
        this(capacity, ingress, () -> { });
    }

    EclipseCallbackMailbox(
            int capacity, ObserverIngress ingress, Runnable admissionReadProbe) {
        if (capacity < 1) {
            throw new IllegalArgumentException("mailbox capacity must be positive");
        }
        this.capacity = capacity;
        this.ingress = Objects.requireNonNull(ingress, "ingress");
        this.admissionReadProbe = Objects.requireNonNull(
                admissionReadProbe, "admissionReadProbe");
        worker = new Thread(this::pump, "hrc-job-observer-mailbox");
        worker.setDaemon(true);
    }

    synchronized void start() {
        if (!state.compareAndSet(State.NEW, State.ACTIVE)) {
            throw new IllegalStateException("mailbox can start only once");
        }
        if (!admissionGate.compareAndSet(0, ADMISSIONS_ACTIVE)) {
            state.set(State.CLOSED);
            workerTerminated.countDown();
            throw new IllegalStateException("mailbox admission gate was not empty");
        }
        worker.start();
    }

    /** Begins one callback before any callback data is read; null means reject. */
    CallbackEntry beginCallback() {
        long current = admissionGate.get();
        if (!admissionsActive(current) || failurePending(current)) {
            return null;
        }
        CallbackEntry entry = new CallbackEntry(this);
        admissionReadProbe.run();
        while (admissionsActive(current) && !failurePending(current)) {
            long count = current & LEASE_COUNT_MASK;
            if (count == LEASE_COUNT_MASK) {
                latch(InfrastructureIncident.unobserved(
                        InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
                return null;
            }
            if (admissionGate.compareAndSet(current, current + 1)) {
                return entry;
            }
            current = admissionGate.get();
        }
        return null;
    }

    /** Reserves capacity and a ticket for an already-entered callback. */
    boolean admitCallback(CallbackEntry entry, ObservationTime observed) {
        requireOwned(entry);
        Objects.requireNonNull(observed, "observed");
        if (state.get() != State.ACTIVE) {
            latch(InfrastructureIncident.observed(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED, observed));
            finishEntered(entry);
            return false;
        }
        if (firstFailure.get() != null) {
            finishEntered(entry);
            return false;
        }
        if (!reserve()) {
            latch(InfrastructureIncident.observed(
                    InfrastructureFailure.CALLBACK_QUEUE_OVERFLOW, observed));
            finishEntered(entry);
            return false;
        }
        if (state.get() != State.ACTIVE) {
            reserved.decrementAndGet();
            latch(InfrastructureIncident.observed(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED, observed));
            finishEntered(entry);
            return false;
        }
        if (firstFailure.get() != null) {
            reserved.decrementAndGet();
            finishEntered(entry);
            return false;
        }
        long ticket = nextCallbackTicket.getAndIncrement();
        if (ticket <= 0) {
            reserved.decrementAndGet();
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            finishEntered(entry);
            return false;
        }
        if (openTickets.putIfAbsent(ticket, Boolean.TRUE) != null) {
            reserved.decrementAndGet();
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            finishEntered(entry);
            return false;
        }
        if (!entry.admit(ticket)) {
            openTickets.remove(ticket);
            reserved.decrementAndGet();
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            finishEntered(entry);
            return false;
        }
        return true;
    }

    /** Completes one ticket. A null payload records an intentionally ignored Job. */
    void completeCallback(CallbackEntry entry, CapturedLifecycle payload) {
        requireOwned(entry);
        long ticket = entry.admittedTicket();
        if (ticket == 0 || !entry.terminate()) {
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            return;
        }
        if (openTickets.remove(ticket) == null) {
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            releaseCallbackLease();
            signalWorker();
            return;
        }
        if (firstFailure.get() != null || state.get() == State.CLOSED) {
            releaseCallbackLease();
            reserved.decrementAndGet();
            signalWorker();
            return;
        }
        CallbackEnvelope previous = completed.putIfAbsent(
                ticket, new CallbackEnvelope(ticket, payload));
        if (previous != null) {
            releaseCallbackLease();
            reserved.decrementAndGet();
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            signalWorker();
            return;
        }
        releaseCallbackLease();
        if (firstFailure.get() != null || state.get() == State.CLOSED) {
            if (completed.remove(ticket) != null) {
                reserved.decrementAndGet();
            }
        }
        signalWorker();
    }

    void failCallback(CallbackEntry entry, InfrastructureIncident incident) {
        requireOwned(entry);
        Objects.requireNonNull(incident, "incident");
        long ticket = entry.admittedTicket();
        if (!entry.terminate()) {
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            signalWorker();
            return;
        }
        latch(incident);
        if (ticket > 0) {
            if (openTickets.remove(ticket) == null) {
                latch(InfrastructureIncident.unobserved(
                        InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            } else {
                reserved.decrementAndGet();
            }
        }
        releaseCallbackLease();
        signalWorker();
    }

    Optional<InfrastructureIncident> firstFailure() {
        return Optional.ofNullable(firstFailure.get());
    }

    synchronized MailboxCloseResult closeAndAwait(Duration timeout) {
        Objects.requireNonNull(timeout, "timeout");
        long timeoutNanos;
        try {
            timeoutNanos = timeout.toNanos();
        } catch (ArithmeticException failure) {
            throw new IllegalArgumentException("close timeout is too large", failure);
        }
        if (timeoutNanos <= 0) {
            throw new IllegalArgumentException("close timeout must be positive");
        }

        State current = state.get();
        if (current == State.NEW) {
            state.set(State.CLOSED);
            workerTerminated.countDown();
            return result(true);
        }
        if (current == State.CLOSED) {
            return result(workerTerminated.getCount() == 0);
        }
        closeAdmissions();
        state.compareAndSet(State.ACTIVE, State.CLOSING);
        signalWorker();

        boolean terminated = false;
        try {
            terminated = workerTerminated.await(timeoutNanos, TimeUnit.NANOSECONDS);
        } catch (InterruptedException interrupted) {
            Thread.currentThread().interrupt();
        }
        if (!terminated) {
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            worker.interrupt();
        }
        return result(terminated);
    }

    private boolean reserve() {
        while (true) {
            int current = reserved.get();
            if (current >= capacity) {
                return false;
            }
            if (reserved.compareAndSet(current, current + 1)) {
                return true;
            }
        }
    }

    private void requireOwned(CallbackEntry entry) {
        Objects.requireNonNull(entry, "entry");
        if (!entry.belongsTo(this)) {
            throw new IllegalArgumentException("callback entry belongs to another mailbox");
        }
    }

    private void finishEntered(CallbackEntry entry) {
        if (entry.terminate()) {
            releaseCallbackLease();
            signalWorker();
        } else {
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
        }
    }

    private void pump() {
        long expectedTicket = 1;
        try {
            while (true) {
                InfrastructureIncident incident = firstFailure.get();
                if (incident != null) {
                    if (leasedCallbackCount() == 0) {
                        discardCompleted();
                        notifyFailure();
                        return;
                    }
                } else {
                    long gate = admissionGate.get();
                    if (failurePending(gate)) {
                        wakeups.acquire();
                        wakePending.set(false);
                        continue;
                    }
                    CallbackEnvelope envelope = completed.remove(expectedTicket);
                    if (envelope != null) {
                        expectedTicket++;
                        reserved.decrementAndGet();
                        if (envelope.payload() != null) {
                            try {
                                dispatch(envelope.payload());
                            } catch (VirtualMachineError | ThreadDeath fatal) {
                                latch(observedDispatchFailure(envelope.payload()));
                                discardCompleted();
                                notifyFailure();
                                return;
                            } catch (Throwable failure) {
                                latch(observedDispatchFailure(envelope.payload()));
                            }
                        }
                        continue;
                    }
                    if (state.get() == State.CLOSING) {
                        gate = admissionGate.get();
                        if (!admissionsActive(gate)
                                && !failurePending(gate)
                                && (gate & LEASE_COUNT_MASK) == 0
                                && completed.isEmpty()) {
                            return;
                        }
                    }
                }
                wakeups.acquire();
                wakePending.set(false);
            }
        } catch (InterruptedException interrupted) {
            Thread.currentThread().interrupt();
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            discardCompleted();
            notifyFailure();
        } catch (VirtualMachineError | ThreadDeath fatal) {
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            discardCompleted();
            notifyFailure();
        } catch (Throwable failure) {
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            discardCompleted();
            notifyFailure();
        } finally {
            state.set(State.CLOSED);
            workerTerminated.countDown();
        }
    }

    private void dispatch(CapturedLifecycle item) {
        if (item instanceof ProfiledLifecycle profiled) {
            ingress.accept(profiled.input());
        } else if (item instanceof SourceMismatchLifecycle mismatch) {
            ingress.rejectSourceMismatch(
                    mismatch.observedUtc(), mismatch.observedNanos());
        } else {
            throw new IllegalStateException("unknown callback payload type");
        }
    }

    private InfrastructureIncident observedDispatchFailure(CapturedLifecycle item) {
        return InfrastructureIncident.observed(
                InfrastructureFailure.CALLBACK_DISPATCH_FAILED,
                new ObservationTime(item.observedUtc(), item.observedNanos()));
    }

    private void latch(InfrastructureIncident incident) {
        Objects.requireNonNull(incident, "incident");
        markFailurePending();
        signalWorker();
        firstFailure.compareAndSet(null, incident);
        signalWorker();
    }

    private void closeAdmissions() {
        while (true) {
            long current = admissionGate.get();
            if (!admissionsActive(current)
                    || admissionGate.compareAndSet(
                            current, current & ~ADMISSIONS_ACTIVE)) {
                return;
            }
        }
    }

    private void markFailurePending() {
        while (true) {
            long current = admissionGate.get();
            long failed = (current & ~ADMISSIONS_ACTIVE) | FAILURE_PENDING;
            if (current == failed || admissionGate.compareAndSet(current, failed)) {
                return;
            }
        }
    }

    private static boolean admissionsActive(long gate) {
        return (gate & ADMISSIONS_ACTIVE) != 0;
    }

    private static boolean failurePending(long gate) {
        return (gate & FAILURE_PENDING) != 0;
    }

    private void releaseCallbackLease() {
        while (true) {
            long current = admissionGate.get();
            long count = current & LEASE_COUNT_MASK;
            if (count == 0) {
                latch(InfrastructureIncident.unobserved(
                        InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
                return;
            }
            if (admissionGate.compareAndSet(current, current - 1)) {
                return;
            }
        }
    }

    private void signalWorker() {
        if (wakePending.compareAndSet(false, true)) {
            wakeups.release();
        }
    }

    private void discardCompleted() {
        int discarded = 0;
        for (Long ticket : completed.keySet()) {
            if (completed.remove(ticket) != null) {
                discarded++;
            }
        }
        reserved.addAndGet(-discarded);
    }

    int retainedCallbackCount() {
        return completed.size() + openTickets.size();
    }

    int reservedCallbackCount() {
        return reserved.get();
    }

    long leasedCallbackCount() {
        return admissionGate.get() & LEASE_COUNT_MASK;
    }

    int callbacksInFlightCount() {
        return Math.toIntExact(leasedCallbackCount());
    }

    int pendingWakeupCount() {
        return wakeups.availablePermits();
    }

    private void notifyFailure() {
        InfrastructureIncident incident = firstFailure.get();
        if (incident == null
                || !failureNotificationAttempted.compareAndSet(false, true)) {
            return;
        }
        try {
            if (incident.hasObservation()) {
                ingress.failInfrastructure(
                        incident.failure(),
                        incident.observedUtc(),
                        incident.observedNanos());
            } else {
                ingress.failInfrastructure(incident.failure());
            }
            failureNotificationSucceeded.set(true);
        } catch (VirtualMachineError | ThreadDeath fatal) {
            // The independent incident latch remains authoritative.
        } catch (Throwable ignored) {
            // The independent incident latch remains authoritative.
        }
    }

    private MailboxCloseResult result(boolean terminated) {
        return new MailboxCloseResult(
                firstFailure.get(),
                failureNotificationAttempted.get(),
                failureNotificationSucceeded.get(),
                terminated);
    }

    private record CallbackEnvelope(long ticket, CapturedLifecycle payload) {
        CallbackEnvelope {
            if (ticket <= 0) {
                throw new IllegalArgumentException("callback ticket must be positive");
            }
        }
    }
}
