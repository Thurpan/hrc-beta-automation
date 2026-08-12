package net.hrcautomation.jobobserver;

import java.util.Objects;
import java.util.UUID;

record JobScheduledEvent(
        EventMetadata metadata,
        UUID requestId,
        OperationKind operation,
        long jobId,
        JobDescriptor job) implements ObserverEvent {

    JobScheduledEvent {
        Objects.requireNonNull(metadata, "metadata");
        Objects.requireNonNull(requestId, "requestId");
        Objects.requireNonNull(operation, "operation");
        if (jobId <= 0) {
            throw new IllegalArgumentException("jobId must be positive");
        }
        Objects.requireNonNull(job, "job");
    }
}
