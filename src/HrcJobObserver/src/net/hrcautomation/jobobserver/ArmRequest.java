package net.hrcautomation.jobobserver;

import java.util.Objects;
import java.util.UUID;

record ArmRequest(
        UUID requestId,
        OperationKind operation,
        String expectedJobName,
        long armedNanos,
        long deadlineNanos,
        long timeoutNanos,
        boolean confirmed) {

    ArmRequest {
        Objects.requireNonNull(requestId, "requestId");
        Objects.requireNonNull(operation, "operation");
        Objects.requireNonNull(expectedJobName, "expectedJobName");
        if (!operation.acceptsExpectedName(expectedJobName)) {
            throw new IllegalArgumentException("expectedJobName is invalid");
        }
        if (timeoutNanos <= 0) {
            throw new IllegalArgumentException("timeoutNanos must be positive");
        }
    }

    boolean sameIntent(
            UUID otherRequestId,
            OperationKind otherOperation,
            String otherName,
            long otherTimeoutNanos) {
        return requestId.equals(otherRequestId)
                && operation == otherOperation
                && expectedJobName.equals(otherName)
                && timeoutNanos == otherTimeoutNanos;
    }

    ArmRequest confirmedAt(long observedNanos) {
        return new ArmRequest(
                requestId,
                operation,
                expectedJobName,
                armedNanos,
                observedNanos + timeoutNanos,
                timeoutNanos,
                true);
    }

    boolean expiredAt(long observedNanos) {
        return observedNanos - deadlineNanos >= 0;
    }

    boolean observedBeforeArm(long observedNanos) {
        return observedNanos - armedNanos < 0;
    }
}
