package net.hrcautomation.jobobserver;

import java.time.Instant;
import java.util.Objects;

/**
 * Callback-captured lifecycle data. This class deliberately keeps the raw Job
 * identity out of generated equality, hashing, and string representations.
 */
final class LifecycleInput {
    enum Kind {
        SCHEDULED,
        RUNNING,
        DONE
    }

    private final Kind kind;
    private final Object identity;
    private final JobDescriptor job;
    private final StatusSnapshot status;
    private final Instant observedUtc;
    private final long observedNanos;

    private LifecycleInput(
            Kind kind,
            Object identity,
            JobDescriptor job,
            StatusSnapshot status,
            Instant observedUtc,
            long observedNanos) {
        this.kind = Objects.requireNonNull(kind, "kind");
        this.identity = Objects.requireNonNull(identity, "identity");
        this.job = Objects.requireNonNull(job, "job");
        this.observedUtc = Objects.requireNonNull(observedUtc, "observedUtc");
        if (kind != Kind.DONE && status != null) {
            throw new IllegalArgumentException("status is only permitted for DONE");
        }
        this.status = status;
        this.observedNanos = observedNanos;
    }

    static LifecycleInput scheduled(
            Object identity, JobDescriptor job, Instant observedUtc, long observedNanos) {
        return new LifecycleInput(
                Kind.SCHEDULED, identity, job, null, observedUtc, observedNanos);
    }

    static LifecycleInput running(
            Object identity, JobDescriptor job, Instant observedUtc, long observedNanos) {
        return new LifecycleInput(
                Kind.RUNNING, identity, job, null, observedUtc, observedNanos);
    }

    static LifecycleInput done(
            Object identity,
            JobDescriptor job,
            StatusSnapshot status,
            Instant observedUtc,
            long observedNanos) {
        return new LifecycleInput(
                Kind.DONE, identity, job, status, observedUtc, observedNanos);
    }

    Kind kind() {
        return kind;
    }

    Object identity() {
        return identity;
    }

    JobDescriptor job() {
        return job;
    }

    StatusSnapshot status() {
        return status;
    }

    Instant observedUtc() {
        return observedUtc;
    }

    long observedNanos() {
        return observedNanos;
    }
}
