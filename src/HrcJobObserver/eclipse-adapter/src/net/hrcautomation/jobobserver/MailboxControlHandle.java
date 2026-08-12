package net.hrcautomation.jobobserver;

import java.time.Duration;
import java.util.Objects;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;

/** Single-use bounded waiter for one ordered mailbox control action. */
final class MailboxControlHandle<T> {
    enum TimeoutDisposition {
        CANCELLED,
        IN_FLIGHT,
        COMPLETED_AFTER_WAIT
    }

    private enum State {
        NEW,
        QUEUED,
        CLAIMED,
        COMPLETED,
        FAILED
    }

    private final EclipseCallbackMailbox owner;
    private final Runnable timeoutTransitionProbe;
    private final AtomicBoolean capacityOwned = new AtomicBoolean(true);
    private final CountDownLatch finished = new CountDownLatch(1);
    private State state = State.NEW;
    private long ticket;
    private MailboxControlResult<T> result;
    private MailboxControlFailure failure;

    MailboxControlHandle(EclipseCallbackMailbox owner) {
        this(owner, () -> { });
    }

    MailboxControlHandle(
            EclipseCallbackMailbox owner, Runnable timeoutTransitionProbe) {
        this.owner = Objects.requireNonNull(owner, "owner");
        this.timeoutTransitionProbe = Objects.requireNonNull(
                timeoutTransitionProbe, "timeoutTransitionProbe");
    }

    synchronized void queue(long assignedTicket) {
        if (assignedTicket <= 0 || state != State.NEW) {
            throw new IllegalStateException("control handle cannot be queued");
        }
        ticket = assignedTicket;
        state = State.QUEUED;
    }

    synchronized long ticket() {
        if (ticket <= 0) {
            throw new IllegalStateException("control handle has no ticket");
        }
        return ticket;
    }

    synchronized boolean claim() {
        if (state != State.QUEUED) {
            return false;
        }
        state = State.CLAIMED;
        return true;
    }

    synchronized void complete(MailboxControlResult<T> completed) {
        if (state != State.CLAIMED) {
            return;
        }
        result = Objects.requireNonNull(completed, "completed");
        state = State.COMPLETED;
        finished.countDown();
    }

    synchronized boolean isCompleted() {
        return state == State.COMPLETED;
    }

    boolean releaseCapacityOwnership() {
        return capacityOwned.compareAndSet(true, false);
    }

    private synchronized TimeoutDisposition timeoutAfterWait() {
        if (state == State.COMPLETED) {
            result = null;
            failure = MailboxControlFailure.TIMED_OUT;
            state = State.FAILED;
            return TimeoutDisposition.COMPLETED_AFTER_WAIT;
        }
        if (state == State.QUEUED) {
            failure = MailboxControlFailure.TIMED_OUT;
            state = State.FAILED;
            finished.countDown();
            return TimeoutDisposition.CANCELLED;
        }
        if (state == State.CLAIMED) {
            failure = MailboxControlFailure.TIMED_OUT;
            state = State.FAILED;
            finished.countDown();
            return TimeoutDisposition.IN_FLIGHT;
        }
        return TimeoutDisposition.CANCELLED;
    }

    synchronized boolean fail(MailboxControlFailure reason) {
        Objects.requireNonNull(reason, "reason");
        if (state == State.COMPLETED || state == State.FAILED) {
            return false;
        }
        failure = reason;
        state = State.FAILED;
        finished.countDown();
        return true;
    }

    MailboxControlResult<T> await(Duration timeout) {
        Objects.requireNonNull(timeout, "timeout");
        long nanos;
        try {
            nanos = timeout.toNanos();
        } catch (ArithmeticException failure) {
            throw new IllegalArgumentException("control timeout is too large", failure);
        }
        if (nanos <= 0) {
            throw new IllegalArgumentException("control timeout must be positive");
        }
        boolean signalled = false;
        try {
            signalled = finished.await(nanos, TimeUnit.NANOSECONDS);
        } catch (InterruptedException interrupted) {
            Thread.currentThread().interrupt();
        }
        if (!signalled) {
            timeoutTransitionProbe.run();
            owner.timeoutControl(this, timeoutAfterWait());
        }
        synchronized (this) {
            if (state == State.COMPLETED) {
                return result;
            }
            MailboxControlFailure reason = failure == null
                    ? MailboxControlFailure.TIMED_OUT
                    : failure;
            throw new MailboxControlException(reason);
        }
    }
}
