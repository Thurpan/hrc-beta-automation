package net.hrcautomation.jobobserver;

import java.util.Objects;
import java.util.UUID;

/** RUNNING evidence observed after the session had already faulted. */
record JobRunningRejectedEvent(
        EventMetadata metadata,
        UUID requestId,
        OperationKind operation,
        long jobId,
        JobDescriptor job,
        FaultReason rejectionReason) implements ObserverEvent {

    JobRunningRejectedEvent {
        Objects.requireNonNull(metadata, "metadata");
        Objects.requireNonNull(requestId, "requestId");
        Objects.requireNonNull(operation, "operation");
        if (jobId <= 0) {
            throw new IllegalArgumentException("jobId must be positive");
        }
        Objects.requireNonNull(job, "job");
        if (rejectionReason != FaultReason.TERMINAL_EVENT_REJECTED) {
            throw new IllegalArgumentException("RUNNING rejection requires a prior session fault");
        }
    }
}
