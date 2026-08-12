package net.hrcautomation.jobobserver;

import java.util.Objects;
import java.util.UUID;

record ArmRequest(
        UUID requestId,
        OperationKind operation,
        String expectedJobName,
        long armedNanos,
        long deadlineNanos) {

    ArmRequest {
        Objects.requireNonNull(requestId, "requestId");
        Objects.requireNonNull(operation, "operation");
        Objects.requireNonNull(expectedJobName, "expectedJobName");
        if (!operation.acceptsExpectedName(expectedJobName)) {
            throw new IllegalArgumentException("expectedJobName is invalid");
        }
    }

    boolean sameIntent(UUID otherRequestId, OperationKind otherOperation, String otherName) {
        return requestId.equals(otherRequestId)
                && operation == otherOperation
                && expectedJobName.equals(otherName);
    }

    boolean expiredAt(long observedNanos) {
        return observedNanos - deadlineNanos >= 0;
    }

    boolean observedBeforeArm(long observedNanos) {
        return observedNanos - armedNanos < 0;
    }
}
