package net.hrcautomation.jobobserver;

import java.time.Instant;
import java.util.Objects;

/** First adapter infrastructure failure, without exception text or stack data. */
final class InfrastructureIncident {
    private final InfrastructureFailure failure;
    private final Instant observedUtc;
    private final long observedNanos;

    private InfrastructureIncident(
            InfrastructureFailure failure, Instant observedUtc, long observedNanos) {
        this.failure = Objects.requireNonNull(failure, "failure");
        this.observedUtc = observedUtc;
        this.observedNanos = observedNanos;
    }

    static InfrastructureIncident unobserved(InfrastructureFailure failure) {
        return new InfrastructureIncident(failure, null, 0);
    }

    static InfrastructureIncident observed(
            InfrastructureFailure failure, ObservationTime observed) {
        Objects.requireNonNull(observed, "observed");
        return new InfrastructureIncident(
                failure, observed.utc(), observed.monotonicNanos());
    }

    InfrastructureFailure failure() {
        return failure;
    }

    boolean hasObservation() {
        return observedUtc != null;
    }

    Instant observedUtc() {
        if (observedUtc == null) {
            throw new IllegalStateException("incident has no callback observation");
        }
        return observedUtc;
    }

    long observedNanos() {
        if (observedUtc == null) {
            throw new IllegalStateException("incident has no callback observation");
        }
        return observedNanos;
    }
}
