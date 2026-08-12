package net.hrcautomation.jobobserver;

import java.util.Objects;
import java.util.UUID;

record ArmAcceptedEvent(
        EventMetadata metadata,
        UUID requestId,
        OperationKind operation,
        String expectedJobName,
        long deadlineNanos) implements ObserverEvent {

    ArmAcceptedEvent {
        Objects.requireNonNull(metadata, "metadata");
        Objects.requireNonNull(requestId, "requestId");
        Objects.requireNonNull(operation, "operation");
        Objects.requireNonNull(expectedJobName, "expectedJobName");
    }
}
