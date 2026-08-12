package net.hrcautomation.jobobserver;

import java.time.Duration;
import java.util.Objects;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicLong;
import java.util.concurrent.atomic.AtomicReference;
import java.util.concurrent.locks.LockSupport;
import org.eclipse.core.runtime.jobs.IJobChangeEvent;
import org.eclipse.core.runtime.jobs.Job;
import org.eclipse.core.runtime.jobs.JobChangeAdapter;

/**
 * Non-blocking callback admission gate around the concrete Eclipse adapter.
 * Startup callbacks are classified but never dispatched. Active callbacks are
 * admitted before they can touch the mailbox. Closing admissions is atomic
 * with admission count ownership.
 */
final class ListenerRegistrationGate extends JobChangeAdapter {
    private static final int PHASE_SHIFT = 61;
    private static final long COUNT_MASK = (1L << PHASE_SHIFT) - 1;
    private static final long PARK_SLICE_NANOS = 1_000_000L;

    enum Phase {
        STARTING,
        SEALING,
        ACTIVE,
        CLOSED
    }

    private enum CallbackKind {
        ABOUT_TO_RUN,
        AWAKE,
        DONE,
        RUNNING,
        SCHEDULED,
        SLEEPING
    }

    private final EclipseJobSourceClassifier classifier;
    private final EclipseJobChangeListener delegate;
    private final AtomicLong admission = new AtomicLong(encode(Phase.STARTING, 0));
    private final AtomicReference<ObserverLifecycleException.Reason> startupFailure =
            new AtomicReference<>();

    ListenerRegistrationGate(
            EclipseJobSourceClassifier classifier,
            EclipseJobChangeListener delegate) {
        this.classifier = Objects.requireNonNull(classifier, "classifier");
        this.delegate = Objects.requireNonNull(delegate, "delegate");
    }

    @Override
    public void aboutToRun(IJobChangeEvent event) {
        handle(CallbackKind.ABOUT_TO_RUN, event);
    }

    @Override
    public void awake(IJobChangeEvent event) {
        handle(CallbackKind.AWAKE, event);
    }

    @Override
    public void done(IJobChangeEvent event) {
        handle(CallbackKind.DONE, event);
    }

    @Override
    public void running(IJobChangeEvent event) {
        handle(CallbackKind.RUNNING, event);
    }

    @Override
    public void scheduled(IJobChangeEvent event) {
        handle(CallbackKind.SCHEDULED, event);
    }

    @Override
    public void sleeping(IJobChangeEvent event) {
        handle(CallbackKind.SLEEPING, event);
    }

    void sealStartup() throws ObserverLifecycleException {
        while (true) {
            long current = admission.get();
            if (phase(current) != Phase.STARTING) {
                throw new ObserverLifecycleException(
                        ObserverLifecycleException.Reason.ACTIVATOR_STATE_INVALID);
            }
            if (admission.compareAndSet(
                    current, encode(Phase.SEALING, count(current)))) {
                return;
            }
        }
    }

    ObserverLifecycleException.Reason activateAfterStartup(Duration timeout) {
        long timeoutNanos = positiveNanos(timeout);
        long started = System.nanoTime();
        while (true) {
            long current = admission.get();
            Phase currentPhase = phase(current);
            if (currentPhase != Phase.SEALING) {
                return ObserverLifecycleException.Reason.ACTIVATOR_STATE_INVALID;
            }
            if (count(current) == 0) {
                ObserverLifecycleException.Reason failure = startupFailure.get();
                if (failure != null) {
                    return failure;
                }
                if (admission.compareAndSet(
                        current, encode(Phase.ACTIVE, 0))) {
                    return null;
                }
                continue;
            }
            if (!pause(started, timeoutNanos)) {
                return ObserverLifecycleException.Reason.STARTUP_CALLBACK_TIMEOUT;
            }
        }
    }

    void closeAdmissions() {
        while (true) {
            long current = admission.get();
            if (phase(current) == Phase.CLOSED
                    || admission.compareAndSet(
                            current, encode(Phase.CLOSED, count(current)))) {
                return;
            }
        }
    }

    boolean awaitAdmittedInvocations(Duration timeout) {
        long timeoutNanos = positiveNanos(timeout);
        long started = System.nanoTime();
        while (count(admission.get()) != 0) {
            if (!pause(started, timeoutNanos)) {
                return false;
            }
        }
        return true;
    }

    Phase phase() {
        return phase(admission.get());
    }

    long callbacksInFlight() {
        return count(admission.get());
    }

    ObserverLifecycleException.Reason startupFailure() {
        return startupFailure.get();
    }

    private void handle(CallbackKind kind, IJobChangeEvent event) {
        Invocation invocation = enter();
        if (invocation == null) {
            return;
        }
        try (invocation) {
            if (invocation.phase() == Phase.STARTING
                    || invocation.phase() == Phase.SEALING) {
                classifyStartup(event);
                return;
            }
            if (invocation.phase() == Phase.ACTIVE) {
                dispatch(kind, event);
            }
        }
    }

    private void classifyStartup(IJobChangeEvent event) {
        try {
            Job job = Objects.requireNonNull(event, "event").getJob();
            EclipseJobSourceClassifier.Classification classification =
                    classifier.classify(job);
            if (classification == EclipseJobSourceClassifier.Classification.MATCH) {
                startupFailure.compareAndSet(
                        null,
                        ObserverLifecycleException.Reason.RELEVANT_JOB_PRESENT);
            } else if (classification
                    == EclipseJobSourceClassifier.Classification.SOURCE_MISMATCH) {
                startupFailure.compareAndSet(
                        null, ObserverLifecycleException.Reason.SOURCE_MISMATCH);
            }
        } catch (VirtualMachineError | ThreadDeath fatal) {
            startupFailure.compareAndSet(
                    null, ObserverLifecycleException.Reason.STARTUP_CALLBACK_FAILED);
            throw fatal;
        } catch (Throwable failure) {
            startupFailure.compareAndSet(
                    null, ObserverLifecycleException.Reason.STARTUP_CALLBACK_FAILED);
        }
    }

    private void dispatch(CallbackKind kind, IJobChangeEvent event) {
        switch (kind) {
            case DONE -> delegate.done(event);
            case RUNNING -> delegate.running(event);
            case SCHEDULED -> delegate.scheduled(event);
            case ABOUT_TO_RUN, AWAKE, SLEEPING -> {
                // These callbacks are deliberately outside the observer schema.
            }
        }
    }

    private Invocation enter() {
        while (true) {
            long current = admission.get();
            Phase currentPhase = phase(current);
            if (currentPhase == Phase.CLOSED) {
                return null;
            }
            long currentCount = count(current);
            if (currentCount == COUNT_MASK) {
                startupFailure.compareAndSet(
                        null, ObserverLifecycleException.Reason.STARTUP_CALLBACK_FAILED);
                return null;
            }
            if (admission.compareAndSet(current, current + 1)) {
                return new Invocation(currentPhase);
            }
        }
    }

    private void release() {
        while (true) {
            long current = admission.get();
            if (count(current) == 0) {
                startupFailure.compareAndSet(
                        null, ObserverLifecycleException.Reason.STARTUP_CALLBACK_FAILED);
                return;
            }
            if (admission.compareAndSet(current, current - 1)) {
                return;
            }
        }
    }

    private static boolean pause(long started, long timeoutNanos) {
        if (Thread.currentThread().isInterrupted()) {
            return false;
        }
        long elapsed = System.nanoTime() - started;
        long remaining = timeoutNanos - elapsed;
        if (remaining <= 0) {
            return false;
        }
        LockSupport.parkNanos(Math.min(PARK_SLICE_NANOS, remaining));
        return !Thread.currentThread().isInterrupted();
    }

    private static long positiveNanos(Duration timeout) {
        Objects.requireNonNull(timeout, "timeout");
        long nanos;
        try {
            nanos = timeout.toNanos();
        } catch (ArithmeticException failure) {
            throw new IllegalArgumentException("timeout is too large", failure);
        }
        if (nanos <= 0) {
            throw new IllegalArgumentException("timeout must be positive");
        }
        return nanos;
    }

    private static long encode(Phase phase, long count) {
        return ((long) phase.ordinal() << PHASE_SHIFT) | count;
    }

    private static Phase phase(long value) {
        int ordinal = (int) (value >>> PHASE_SHIFT);
        return Phase.values()[ordinal];
    }

    private static long count(long value) {
        return value & COUNT_MASK;
    }

    private final class Invocation implements AutoCloseable {
        private final Phase phase;
        private final AtomicBoolean owned = new AtomicBoolean(true);

        private Invocation(Phase phase) {
            this.phase = phase;
        }

        private Phase phase() {
            return phase;
        }

        @Override
        public void close() {
            if (owned.compareAndSet(true, false)) {
                release();
            }
        }
    }
}
