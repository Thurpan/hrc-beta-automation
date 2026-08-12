package net.hrcautomation.jobobserver;

import java.time.Duration;
import java.time.Instant;
import java.util.Collection;
import java.util.HashMap;
import java.util.IdentityHashMap;
import java.util.Map;
import java.util.Objects;
import java.util.UUID;
import java.util.function.LongFunction;
import java.util.function.LongSupplier;
import java.util.function.Supplier;

/**
 * Correlates an arm with the exact Java Job object seen at SCHEDULED. The raw
 * identity enters through the bounded adapter hand-off but never enters an
 * emitted event. Calls are synchronized because the mailbox worker and later
 * controller operations such as arm, expiry, and status queries can run
 * concurrently.
 */
final class ObserverCoordinator implements ObserverIngress {
    static final long MAX_ARM_TIMEOUT_NANOS = Duration.ofMinutes(5).toNanos();

    private final UUID sessionId;
    private final OperationProfileSet profiles;
    private final int requestCapacity;
    private final int jobCapacity;
    private final LongSupplier monotonicClock;
    private final Supplier<Instant> wallClock;
    private final ReplayBuffer replayBuffer;
    private final Map<UUID, ArmRequest> knownRequests = new HashMap<>();
    private final IdentityHashMap<Object, TrackedJob> jobs = new IdentityHashMap<>();
    private ArmRequest pendingArm;
    private FaultReason faultReason;
    private long nextJobId = 1;

    ObserverCoordinator(
            UUID sessionId,
            Collection<OperationProfile> profiles,
            int requestCapacity,
            int jobCapacity,
            int replayCapacity,
            LongSupplier monotonicClock,
            Supplier<Instant> wallClock) {
        this(
                sessionId,
                new OperationProfileSet(profiles),
                requestCapacity,
                jobCapacity,
                replayCapacity,
                monotonicClock,
                wallClock);
    }

    ObserverCoordinator(
            UUID sessionId,
            OperationProfileSet profiles,
            int requestCapacity,
            int jobCapacity,
            int replayCapacity,
            LongSupplier monotonicClock,
            Supplier<Instant> wallClock) {
        this.sessionId = Objects.requireNonNull(sessionId, "sessionId");
        this.profiles = Objects.requireNonNull(profiles, "profiles");
        if (requestCapacity < 1 || jobCapacity < 1) {
            throw new IllegalArgumentException("request and job capacities must be positive");
        }
        this.requestCapacity = requestCapacity;
        this.jobCapacity = jobCapacity;
        this.monotonicClock = Objects.requireNonNull(monotonicClock, "monotonicClock");
        this.wallClock = Objects.requireNonNull(wallClock, "wallClock");
        this.replayBuffer = new ReplayBuffer(replayCapacity);
    }

    synchronized ArmOutcome arm(
            UUID requestId,
            OperationKind operation,
            String expectedJobName,
            long timeoutNanos) {
        if (faultReason != null) {
            return ArmOutcome.FAULTED;
        }

        long nowNanos = monotonicClock.getAsLong();
        Instant nowUtc = wallClock.get();
        if (expireAt(nowNanos, nowUtc)) {
            return ArmOutcome.FAULTED;
        }
        if (requestId == null
                || operation == null
                || !operation.acceptsExpectedName(expectedJobName)
                || !profiles.contains(operation)
                || timeoutNanos <= 0
                || timeoutNanos > MAX_ARM_TIMEOUT_NANOS) {
            return ArmOutcome.REJECTED;
        }

        ArmRequest known = knownRequests.get(requestId);
        if (known != null) {
            if (known.sameIntent(
                    requestId, operation, expectedJobName, timeoutNanos)) {
                return ArmOutcome.IDEMPOTENT;
            }
            fault(FaultReason.REQUEST_ID_REUSED, nowUtc, nowNanos);
            return ArmOutcome.FAULTED;
        }
        if (pendingArm != null) {
            return ArmOutcome.BUSY;
        }
        if (jobs.size() >= jobCapacity) {
            fault(FaultReason.JOB_CAPACITY_EXCEEDED, nowUtc, nowNanos);
            return ArmOutcome.FAULTED;
        }
        if (nextJobId <= 0 || nextJobId == Long.MAX_VALUE) {
            fault(FaultReason.JOB_ID_EXHAUSTED, nowUtc, nowNanos);
            return ArmOutcome.FAULTED;
        }
        if (knownRequests.size() >= requestCapacity) {
            fault(FaultReason.REQUEST_CAPACITY_EXCEEDED, nowUtc, nowNanos);
            return ArmOutcome.FAULTED;
        }

        ArmRequest request = new ArmRequest(
                requestId,
                operation,
                expectedJobName,
                nowNanos,
                nowNanos + timeoutNanos,
                timeoutNanos,
                false);
        knownRequests.put(requestId, request);
        pendingArm = request;
        if (!emit(sequence -> new ArmAcceptedEvent(
                metadata(sequence, nowUtc, nowNanos),
                request.requestId(),
                request.operation(),
                request.expectedJobName(),
                request.deadlineNanos()))) {
            return ArmOutcome.FAULTED;
        }
        return ArmOutcome.ACCEPTED;
    }

    @Override
    public synchronized void accept(LifecycleInput input) {
        if (input == null) {
            if (faultReason == null) {
                fault(FaultReason.LIFECYCLE_BEFORE_SCHEDULED,
                        wallClock.get(), monotonicClock.getAsLong());
            }
            return;
        }

        TrackedJob tracked = jobs.get(input.identity());
        if (faultReason != null) {
            if (tracked != null) {
                if (input.kind() == LifecycleInput.Kind.RUNNING) {
                    acceptRunning(input, tracked);
                } else if (input.kind() == LifecycleInput.Kind.DONE) {
                    acceptDone(input, tracked);
                }
            }
            return;
        }
        if (expireAt(input.observedNanos(), input.observedUtc())) {
            if (tracked != null) {
                if (input.kind() == LifecycleInput.Kind.RUNNING) {
                    acceptRunning(input, tracked);
                } else if (input.kind() == LifecycleInput.Kind.DONE) {
                    acceptDone(input, tracked);
                }
            }
            return;
        }

        switch (input.kind()) {
            case SCHEDULED -> acceptScheduled(input);
            case RUNNING -> acceptRunning(input, tracked);
            case DONE -> acceptDone(input, tracked);
        }
    }

    @Override
    public synchronized void rejectSourceMismatch(
            Instant observedUtc, long observedNanos) {
        Objects.requireNonNull(observedUtc, "observedUtc");
        if (faultReason != null || expireAt(observedNanos, observedUtc)) {
            return;
        }
        if (pendingArm != null && pendingArm.observedBeforeArm(observedNanos)) {
            fault(FaultReason.EVENT_BEFORE_ARM, observedUtc, observedNanos);
            return;
        }
        fault(FaultReason.JOB_MISMATCH, observedUtc, observedNanos);
    }

    @Override
    public synchronized void failInfrastructure(InfrastructureFailure failure) {
        failInfrastructure(failure, wallClock.get(), monotonicClock.getAsLong());
    }

    @Override
    public synchronized void failInfrastructure(
            InfrastructureFailure failure, Instant observedUtc, long observedNanos) {
        Objects.requireNonNull(failure, "failure");
        Objects.requireNonNull(observedUtc, "observedUtc");
        FaultReason reason = switch (failure) {
            case CALLBACK_CAPTURE_FAILED -> FaultReason.CALLBACK_CAPTURE_FAILED;
            case CALLBACK_QUEUE_OVERFLOW -> FaultReason.CALLBACK_QUEUE_OVERFLOW;
            case CALLBACK_DISPATCH_FAILED -> FaultReason.CALLBACK_DISPATCH_FAILED;
        };
        fault(reason, observedUtc, observedNanos);
    }

    synchronized boolean expire() {
        if (faultReason != null) {
            return false;
        }
        return expireAt(monotonicClock.getAsLong(), wallClock.get());
    }

    /**
     * Confirms that an accepted arm is still valid at the current observer
     * time. The expiry check, request match, and final lease start share the
     * coordinator lock. This method does not itself authorise external input.
     */
    synchronized boolean confirmArmHealthy(UUID requestId) {
        Objects.requireNonNull(requestId, "requestId");
        long nowNanos = monotonicClock.getAsLong();
        Instant nowUtc = wallClock.get();
        if (faultReason == null) {
            expireAt(nowNanos, nowUtc);
        }
        if (faultReason == null
                && (pendingArm == null
                        || !pendingArm.requestId().equals(requestId))) {
            fault(FaultReason.ARM_CONFIRMATION_LOST, nowUtc, nowNanos);
        }
        if (faultReason == null) {
            ArmRequest confirmed = pendingArm.confirmedAt(nowNanos);
            pendingArm = confirmed;
            knownRequests.put(requestId, confirmed);
            if (!emit(sequence -> new ArmConfirmedEvent(
                    metadata(sequence, nowUtc, nowNanos),
                    confirmed.requestId(),
                    confirmed.operation(),
                    confirmed.expectedJobName(),
                    confirmed.deadlineNanos()))) {
                return false;
            }
        }
        return faultReason == null;
    }

    synchronized boolean isFaulted() {
        return faultReason != null;
    }

    synchronized FaultReason faultReason() {
        return faultReason;
    }

    UUID sessionId() {
        return sessionId;
    }

    int replayCapacity() {
        return replayBuffer.capacity();
    }

    synchronized ObserverCoreSnapshot checkpoint(long lastSeenSequence) {
        if (lastSeenSequence < 0) {
            throw new IllegalArgumentException(
                    "last seen sequence must not be negative");
        }
        if (faultReason == null) {
            expireAt(monotonicClock.getAsLong(), wallClock.get());
        }
        return new ObserverCoreSnapshot(
                replayBuffer.replayAfter(lastSeenSequence), faultReason);
    }

    ReplayQuery replayAfter(long lastSeenSequence) {
        return replayBuffer.replayAfter(lastSeenSequence);
    }

    private void acceptScheduled(LifecycleInput input) {
        if (jobs.containsKey(input.identity())) {
            fault(FaultReason.DUPLICATE_SCHEDULED, input.observedUtc(), input.observedNanos());
            return;
        }
        OperationProfile sourceProfile = profileForClass(input.job());
        if (sourceProfile == null) {
            if (pendingArm != null && pendingArm.expectedJobName().equals(input.job().name())) {
                fault(FaultReason.JOB_MISMATCH, input.observedUtc(), input.observedNanos());
            }
            return;
        }
        if (pendingArm != null && pendingArm.observedBeforeArm(input.observedNanos())) {
            fault(FaultReason.EVENT_BEFORE_ARM, input.observedUtc(), input.observedNanos());
            return;
        }
        if (!sourceProfile.sourceMatches(input.job())) {
            fault(FaultReason.JOB_MISMATCH, input.observedUtc(), input.observedNanos());
            return;
        }
        if (pendingArm == null) {
            fault(FaultReason.UNEXPECTED_RELEVANT_JOB,
                    input.observedUtc(), input.observedNanos());
            return;
        }

        OperationProfile armedProfile = profiles.forOperation(pendingArm.operation());
        if (sourceProfile.operation() != pendingArm.operation()
                || !armedProfile.matches(pendingArm, input.job())) {
            fault(FaultReason.JOB_MISMATCH, input.observedUtc(), input.observedNanos());
            return;
        }
        if (jobs.size() >= jobCapacity) {
            fault(FaultReason.JOB_CAPACITY_EXCEEDED,
                    input.observedUtc(), input.observedNanos());
            return;
        }
        if (nextJobId <= 0 || nextJobId == Long.MAX_VALUE) {
            fault(FaultReason.JOB_ID_EXHAUSTED, input.observedUtc(), input.observedNanos());
            return;
        }

        long jobId = nextJobId++;
        TrackedJob newJob = new TrackedJob(
                pendingArm, jobId, input.job(), input.observedNanos());
        jobs.put(input.identity(), newJob);
        pendingArm = null;
        emit(sequence -> new JobScheduledEvent(
                metadata(sequence, input.observedUtc(), input.observedNanos()),
                newJob.arm.requestId(),
                newJob.arm.operation(),
                newJob.jobId,
                newJob.descriptor));
    }

    private void acceptRunning(LifecycleInput input, TrackedJob tracked) {
        if (tracked == null) {
            faultIfRelevant(input, FaultReason.LIFECYCLE_BEFORE_SCHEDULED);
            return;
        }
        if (!tracked.descriptor.equals(input.job())) {
            fault(FaultReason.JOB_DESCRIPTOR_CHANGED,
                    input.observedUtc(), input.observedNanos());
            return;
        }
        if (tracked.observedBeforeLast(input.observedNanos())) {
            fault(FaultReason.CALLBACK_TIME_REGRESSED,
                    input.observedUtc(), input.observedNanos());
            return;
        }
        if (tracked.phase != Phase.SCHEDULED) {
            fault(FaultReason.DUPLICATE_RUNNING, input.observedUtc(), input.observedNanos());
            return;
        }

        tracked.phase = Phase.RUNNING;
        tracked.lastObservedNanos = input.observedNanos();
        if (faultReason == null) {
            emit(sequence -> new JobRunningEvent(
                    metadata(sequence, input.observedUtc(), input.observedNanos()),
                    tracked.arm.requestId(),
                    tracked.arm.operation(),
                    tracked.jobId,
                    tracked.descriptor));
        } else {
            emit(sequence -> new JobRunningRejectedEvent(
                    metadata(sequence, input.observedUtc(), input.observedNanos()),
                    tracked.arm.requestId(),
                    tracked.arm.operation(),
                    tracked.jobId,
                    tracked.descriptor,
                    FaultReason.TERMINAL_EVENT_REJECTED));
        }
    }

    private void acceptDone(LifecycleInput input, TrackedJob tracked) {
        if (tracked == null) {
            if (faultReason == null) {
                faultIfRelevant(input, FaultReason.LIFECYCLE_BEFORE_SCHEDULED);
            }
            return;
        }
        if (!tracked.descriptor.equals(input.job())) {
            if (faultReason == null) {
                fault(FaultReason.JOB_DESCRIPTOR_CHANGED,
                        input.observedUtc(), input.observedNanos());
            }
            return;
        }
        if (tracked.observedBeforeLast(input.observedNanos())) {
            if (faultReason == null) {
                fault(FaultReason.CALLBACK_TIME_REGRESSED,
                        input.observedUtc(), input.observedNanos());
            }
            return;
        }
        if (tracked.phase == Phase.DONE) {
            if (faultReason == null) {
                fault(FaultReason.DUPLICATE_DONE, input.observedUtc(), input.observedNanos());
            }
            return;
        }
        if (input.status() == null) {
            if (faultReason == null) {
                fault(FaultReason.MISSING_TERMINAL_STATUS,
                        input.observedUtc(), input.observedNanos());
            }
            return;
        }

        TerminalResult result = input.status().terminalResult();
        boolean runningSeen = tracked.phase == Phase.RUNNING;
        FaultReason rejectionReason = null;
        if (result == TerminalResult.UNKNOWN) {
            rejectionReason = FaultReason.UNKNOWN_TERMINAL_STATUS;
        } else if (input.status().pluginOmitted()) {
            rejectionReason = FaultReason.STATUS_PLUGIN_OMITTED;
        } else if (!runningSeen && result != TerminalResult.CANCEL) {
            rejectionReason = FaultReason.DONE_BEFORE_RUNNING;
        } else if (faultReason != null) {
            rejectionReason = FaultReason.TERMINAL_EVENT_REJECTED;
        }
        tracked.phase = Phase.DONE;
        tracked.lastObservedNanos = input.observedNanos();
        boolean emitted;
        if (rejectionReason == null) {
            emitted = emit(sequence -> new JobTerminalEvent(
                    metadata(sequence, input.observedUtc(), input.observedNanos()),
                    tracked.arm.requestId(),
                    tracked.arm.operation(),
                    tracked.jobId,
                    tracked.descriptor,
                    result,
                    input.status().severity(),
                    input.status().ok(),
                    input.status().code(),
                    input.status().plugin(),
                    input.status().pluginOmitted(),
                    runningSeen));
        } else {
            FaultReason finalRejectionReason = rejectionReason;
            emitted = emit(sequence -> new JobTerminalRejectedEvent(
                    metadata(sequence, input.observedUtc(), input.observedNanos()),
                    tracked.arm.requestId(),
                    tracked.arm.operation(),
                    tracked.jobId,
                    tracked.descriptor,
                    result,
                    input.status().severity(),
                    input.status().ok(),
                    input.status().code(),
                    input.status().plugin(),
                    input.status().pluginOmitted(),
                    runningSeen,
                    finalRejectionReason));
        }
        if (!emitted || faultReason != null) {
            return;
        }
        if (rejectionReason != null) {
            if (faultReason == null) {
                fault(rejectionReason, input.observedUtc(), input.observedNanos());
            }
        }
    }

    private void faultIfRelevant(LifecycleInput input, FaultReason reason) {
        OperationProfile profile = profileForClass(input.job());
        if (profile == null) {
            if (pendingArm != null && pendingArm.expectedJobName().equals(input.job().name())) {
                fault(FaultReason.JOB_MISMATCH, input.observedUtc(), input.observedNanos());
            }
            return;
        }
        if (!profile.sourceMatches(input.job())) {
            fault(FaultReason.JOB_MISMATCH, input.observedUtc(), input.observedNanos());
        } else {
            fault(reason, input.observedUtc(), input.observedNanos());
        }
    }

    private OperationProfile profileForClass(JobDescriptor job) {
        return profiles.forClassName(job.className());
    }

    private boolean expireAt(long observedNanos, Instant observedUtc) {
        if (pendingArm != null && pendingArm.expiredAt(observedNanos)) {
            fault(FaultReason.ARM_DEADLINE_EXPIRED, observedUtc, observedNanos);
            return true;
        }
        return false;
    }

    private void fault(FaultReason reason, Instant observedUtc, long observedNanos) {
        if (faultReason != null) {
            return;
        }
        faultReason = reason;
        try {
            replayBuffer.append(sequence -> new ObserverFaultEvent(
                    metadata(sequence, observedUtc, observedNanos), reason));
        } catch (RuntimeException eventFailure) {
            faultReason = FaultReason.EVENT_EMISSION_FAILURE;
        }
    }

    private boolean emit(LongFunction<ObserverEvent> eventFactory) {
        try {
            replayBuffer.append(eventFactory);
            return true;
        } catch (RuntimeException eventFailure) {
            faultReason = FaultReason.EVENT_EMISSION_FAILURE;
            return false;
        }
    }

    private EventMetadata metadata(
            long sequence, Instant observedUtc, long observedNanos) {
        return new EventMetadata(sequence, observedUtc, observedNanos, sessionId);
    }

    private enum Phase {
        SCHEDULED,
        RUNNING,
        DONE
    }

    private static final class TrackedJob {
        private final ArmRequest arm;
        private final long jobId;
        private final JobDescriptor descriptor;
        private long lastObservedNanos;
        private Phase phase = Phase.SCHEDULED;

        private TrackedJob(
                ArmRequest arm,
                long jobId,
                JobDescriptor descriptor,
                long lastObservedNanos) {
            this.arm = arm;
            this.jobId = jobId;
            this.descriptor = descriptor;
            this.lastObservedNanos = lastObservedNanos;
        }

        private boolean observedBeforeLast(long observedNanos) {
            return observedNanos - lastObservedNanos < 0;
        }
    }
}
