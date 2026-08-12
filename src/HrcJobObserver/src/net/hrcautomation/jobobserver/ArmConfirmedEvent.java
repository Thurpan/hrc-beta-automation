package net.hrcautomation.jobobserver;

import java.util.Objects;
import java.util.UUID;

/** Final ordered arm confirmation and its observer-process-local deadline. */
record ArmConfirmedEvent(
        EventMetadata metadata,
        UUID requestId,
        OperationKind operation,
        String expectedJobName,
        long deadlineNanos) implements ObserverEvent {

    ArmConfirmedEvent {
        Objects.requireNonNull(metadata, "metadata");
        Objects.requireNonNull(requestId, "requestId");
        Objects.requireNonNull(operation, "operation");
        Objects.requireNonNull(expectedJobName, "expectedJobName");
    }
}
