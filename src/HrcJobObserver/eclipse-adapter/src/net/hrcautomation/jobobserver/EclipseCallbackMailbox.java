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
    private static final int TICKET_SHIFT = 31;
    private static final long COUNTER_MASK = (1L << TICKET_SHIFT) - 1;
    private static final long LEASE_COUNT_MASK = COUNTER_MASK;
    private static final long TICKET_MASK = COUNTER_MASK << TICKET_SHIFT;
    private static final long TICKET_INCREMENT = 1L << TICKET_SHIFT;

    private enum State {
        NEW,
        ACTIVE,
        CLOSING,
        CLOSED
    }

    private final int capacity;
    private final ObserverIngress ingress;
    private final ConcurrentHashMap<Long, OrderedEnvelope> completed =
            new ConcurrentHashMap<>();
    private final ConcurrentHashMap<Long, Boolean> openTickets =
            new ConcurrentHashMap<>();
    private final Semaphore wakeups = new Semaphore(0);
    private final AtomicBoolean wakePending = new AtomicBoolean();
    private final AtomicInteger reserved = new AtomicInteger();
    private final AtomicLong admissionGate = new AtomicLong();
    private final AtomicBoolean controlPending = new AtomicBoolean();
    private final AtomicReference<InfrastructureIncident> firstFailure =
            new AtomicReference<>();
    private final AtomicReference<State> state = new AtomicReference<>(State.NEW);
    private final AtomicBoolean failureNotificationAttempted = new AtomicBoolean();
    private final AtomicBoolean failureNotificationSucceeded = new AtomicBoolean();
    private final CountDownLatch workerTerminated = new CountDownLatch(1);
    private final Runnable admissionReadProbe;
    private final Runnable controlReleaseProbe;
    private final Thread worker;

    EclipseCallbackMailbox(int capacity, ObserverIngress ingress) {
        this(capacity, ingress, () -> { }, () -> { });
    }

    EclipseCallbackMailbox(
            int capacity, ObserverIngress ingress, Runnable admissionReadProbe) {
        this(capacity, ingress, admissionReadProbe, () -> { });
    }

    EclipseCallbackMailbox(
            int capacity,
            ObserverIngress ingress,
            Runnable admissionReadProbe,
            Runnable controlReleaseProbe) {
        if (capacity < 1) {
            throw new IllegalArgumentException("mailbox capacity must be positive");
        }
        this.capacity = capacity;
        this.ingress = Objects.requireNonNull(ingress, "ingress");
        this.admissionReadProbe = Objects.requireNonNull(
                admissionReadProbe, "admissionReadProbe");
        this.controlReleaseProbe = Objects.requireNonNull(
                controlReleaseProbe, "controlReleaseProbe");
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
            long priorTicket = ticketCounter(current);
            if (count == LEASE_COUNT_MASK || priorTicket == COUNTER_MASK) {
                latch(InfrastructureIncident.unobserved(
                        InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
                return null;
            }
            long updated = current + 1 + TICKET_INCREMENT;
            if (admissionGate.compareAndSet(current, updated)) {
                entry.assignTicket(priorTicket + 1);
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
        long ticket = entry.ticket();
        if (openTickets.putIfAbsent(ticket, Boolean.TRUE) != null) {
            reserved.decrementAndGet();
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            finishEntered(entry);
            return false;
        }
        if (!entry.admit()) {
            openTickets.remove(ticket);
            reserved.decrementAndGet();
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            finishEntered(entry);
            return false;
        }
        return true;
    }

    <T> MailboxControlHandle<T> submitControl(MailboxControlAction<T> action) {
        Objects.requireNonNull(action, "action");
        MailboxControlHandle<T> handle = new MailboxControlHandle<>(this);
        ControlEnvelope<T> envelope = new ControlEnvelope<>(handle, action);
        if (!controlPending.compareAndSet(false, true)) {
            throw new MailboxControlException(
                    MailboxControlFailure.MAILBOX_UNAVAILABLE);
        }
        long ticket = acquireOrderedProducerLease();
        if (ticket == 0) {
            releaseControlCapacity(handle);
            throw new MailboxControlException(
                    MailboxControlFailure.MAILBOX_UNAVAILABLE);
        }
        try {
            handle.queue(ticket);
            if (completed.putIfAbsent(ticket, envelope) != null) {
                releaseControlCapacity(handle);
                handle.fail(MailboxControlFailure.ACTION_FAILED);
                latch(InfrastructureIncident.unobserved(
                        InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            }
        } catch (VirtualMachineError | ThreadDeath fatal) {
            releaseControlCapacity(handle);
            handle.fail(MailboxControlFailure.ACTION_FAILED);
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            throw fatal;
        } catch (Throwable failure) {
            releaseControlCapacity(handle);
            handle.fail(MailboxControlFailure.ACTION_FAILED);
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
        } finally {
            releaseCallbackLease();
            signalWorker();
        }
        return handle;
    }

    void timeoutControl(
            MailboxControlHandle<?> handle,
            MailboxControlHandle.TimeoutDisposition disposition) {
        Objects.requireNonNull(handle, "handle");
        Objects.requireNonNull(disposition, "disposition");
        if (disposition == MailboxControlHandle.TimeoutDisposition.IN_FLIGHT
                || disposition
                        == MailboxControlHandle.TimeoutDisposition.COMPLETED_AFTER_WAIT) {
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
        }
        signalWorker();
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
        OrderedEnvelope previous = completed.putIfAbsent(
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

    boolean dispatchesTo(ObserverIngress candidate) {
        return ingress == Objects.requireNonNull(candidate, "candidate");
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
                    OrderedEnvelope envelope = completed.remove(expectedTicket);
                    if (envelope != null) {
                        expectedTicket++;
                        processEnvelope(envelope);
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

    private void processEnvelope(OrderedEnvelope envelope) {
        if (envelope instanceof CallbackEnvelope callback) {
            reserved.decrementAndGet();
            if (callback.payload() == null) {
                return;
            }
            try {
                dispatch(callback.payload());
            } catch (VirtualMachineError | ThreadDeath fatal) {
                latch(observedDispatchFailure(callback.payload()));
                throw fatal;
            } catch (Throwable failure) {
                latch(observedDispatchFailure(callback.payload()));
            }
            return;
        }
        if (envelope instanceof ControlEnvelope<?> control) {
            processControl(control);
            return;
        }
        latch(InfrastructureIncident.unobserved(
                InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
    }

    private <T> void processControl(ControlEnvelope<T> control) {
        MailboxControlHandle<T> handle = control.handle();
        boolean terminalPublished = false;
        try {
            if (!handle.claim()) {
                return;
            }
            MailboxHealthSnapshot before = healthSnapshot();
            if (!before.healthy()) {
                releaseControlCapacity(handle);
                handle.fail(MailboxControlFailure.MAILBOX_UNAVAILABLE);
                terminalPublished = true;
                return;
            }
            T value;
            try {
                value = Objects.requireNonNull(
                        control.action().execute(before), "control result");
            } catch (VirtualMachineError | ThreadDeath fatal) {
                releaseControlCapacity(handle);
                handle.fail(MailboxControlFailure.ACTION_FAILED);
                terminalPublished = true;
                latch(InfrastructureIncident.unobserved(
                        InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
                throw fatal;
            } catch (Throwable failure) {
                releaseControlCapacity(handle);
                handle.fail(MailboxControlFailure.ACTION_FAILED);
                terminalPublished = true;
                latch(InfrastructureIncident.unobserved(
                        InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
                return;
            }
            releaseControlCapacity(handle);
            MailboxControlResult<T> completed = new MailboxControlResult<>(
                    control.ticket(), value, before, healthSnapshot());
            handle.complete(completed);
            terminalPublished = true;
        } catch (VirtualMachineError | ThreadDeath fatal) {
            releaseControlCapacity(handle);
            if (!terminalPublished) {
                handle.fail(MailboxControlFailure.ACTION_FAILED);
            }
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            throw fatal;
        } catch (Throwable failure) {
            releaseControlCapacity(handle);
            if (!terminalPublished) {
                handle.fail(MailboxControlFailure.ACTION_FAILED);
            }
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
        } finally {
            releaseControlCapacity(handle);
        }
    }

    private void releaseControlCapacity(MailboxControlHandle<?> handle) {
        if (!handle.releaseCapacityOwnership()) {
            return;
        }
        if (!controlPending.compareAndSet(true, false)) {
            latch(InfrastructureIncident.unobserved(
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
            return;
        }
        controlReleaseProbe.run();
    }

    private MailboxHealthSnapshot healthSnapshot() {
        InfrastructureIncident incident = firstFailure.get();
        long gate = admissionGate.get();
        return new MailboxHealthSnapshot(
                incident,
                failurePending(gate),
                state.get() != State.ACTIVE || !admissionsActive(gate));
    }

    private long acquireOrderedProducerLease() {
        long current = admissionGate.get();
        while (admissionsActive(current) && !failurePending(current)) {
            long count = current & LEASE_COUNT_MASK;
            long priorTicket = ticketCounter(current);
            if (count == LEASE_COUNT_MASK || priorTicket == COUNTER_MASK) {
                latch(InfrastructureIncident.unobserved(
                        InfrastructureFailure.CALLBACK_DISPATCH_FAILED));
                return 0;
            }
            if (admissionGate.compareAndSet(
                    current, current + 1 + TICKET_INCREMENT)) {
                return priorTicket + 1;
            }
            current = admissionGate.get();
        }
        return 0;
    }

    private static long ticketCounter(long gate) {
        return (gate & TICKET_MASK) >>> TICKET_SHIFT;
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
            OrderedEnvelope removed = completed.remove(ticket);
            if (removed instanceof CallbackEnvelope) {
                discarded++;
            } else if (removed instanceof ControlEnvelope<?> control) {
                control.handle().fail(MailboxControlFailure.MAILBOX_UNAVAILABLE);
                releaseControlCapacity(control.handle());
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

    private sealed interface OrderedEnvelope
            permits CallbackEnvelope, ControlEnvelope {
        long ticket();
    }

    private record CallbackEnvelope(long ticket, CapturedLifecycle payload)
            implements OrderedEnvelope {
        CallbackEnvelope {
            if (ticket <= 0) {
                throw new IllegalArgumentException("callback ticket must be positive");
            }
        }
    }

    private record ControlEnvelope<T>(
            MailboxControlHandle<T> handle,
            MailboxControlAction<T> action) implements OrderedEnvelope {
        ControlEnvelope {
            Objects.requireNonNull(handle, "handle");
            Objects.requireNonNull(action, "action");
        }

        @Override
        public long ticket() {
            return handle.ticket();
        }
    }
}
