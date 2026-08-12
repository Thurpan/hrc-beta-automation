package net.hrcautomation.jobobserver;

import java.time.Instant;

/** Narrow input boundary used by the callback adapter's mailbox worker. */
interface ObserverIngress {
    void accept(LifecycleInput input);

    void rejectSourceMismatch(Instant observedUtc, long observedNanos);

    void failInfrastructure(InfrastructureFailure failure);

    void failInfrastructure(
            InfrastructureFailure failure, Instant observedUtc, long observedNanos);
}
