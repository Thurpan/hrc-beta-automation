package net.hrcautomation.jobobserver;

import java.time.Duration;
import java.util.Objects;
import java.util.UUID;

/**
 * Joins transport control to the core only through ordered mailbox actions.
 * It is an offline assembly seam, not a runtime entry point.
 */
final class OrderedObserverTransportControl implements ObserverTransportControl {
    private final ObserverCoordinator coordinator;
    private final EclipseCallbackMailbox mailbox;
    private final Duration controlTimeout;

    OrderedObserverTransportControl(
            ObserverCoordinator coordinator,
            EclipseCallbackMailbox mailbox,
            Duration controlTimeout) {
        this.coordinator = Objects.requireNonNull(coordinator, "coordinator");
        this.mailbox = Objects.requireNonNull(mailbox, "mailbox");
        this.controlTimeout = validateTimeout(controlTimeout);
        if (!mailbox.dispatchesTo(coordinator)) {
            throw new IllegalArgumentException(
                    "mailbox must dispatch to the same coordinator");
        }
        if (coordinator.replayCapacity() > ObserverCheckpoint.MAX_REPLAY_EVENTS) {
            throw new IllegalArgumentException(
                    "core replay capacity exceeds checkpoint capacity");
        }
    }

    @Override
    public UUID sessionId() {
        return coordinator.sessionId();
    }

    @Override
    public ObserverCheckpoint checkpoint(long lastSeenSequence) {
        if (lastSeenSequence < 0) {
            throw new IllegalArgumentException(
                    "last seen sequence must not be negative");
        }
        MailboxControlHandle<ObserverCoreSnapshot> handle = mailbox.submitControl(
                before -> coordinator.checkpoint(lastSeenSequence));
        MailboxControlResult<ObserverCoreSnapshot> result =
                handle.await(controlTimeout);
        ObserverCoreSnapshot core = result.value();
        return new ObserverCheckpoint(
                sessionId(),
                result.barrierId(),
                lastSeenSequence,
                core.replay(),
                core.faultReason(),
                callbackHealth(result.after()),
                callbackFailure(result.after()));
    }

    @Override
    public ArmOutcome armIfHealthy(
            UUID requestId,
            OperationKind operation,
            String expectedJobName,
            long timeoutNanos) {
        MailboxControlHandle<ArmOutcome> armHandle = mailbox.submitControl(before -> {
            if (!before.healthy()) {
                return ArmOutcome.FAULTED;
            }
            return coordinator.arm(
                    requestId, operation, expectedJobName, timeoutNanos);
        });
        MailboxControlResult<ArmOutcome> armResult =
                armHandle.await(controlTimeout);
        if (!armResult.before().healthy() || !armResult.after().healthy()) {
            return ArmOutcome.FAULTED;
        }
        if (armResult.value() != ArmOutcome.ACCEPTED
                && armResult.value() != ArmOutcome.IDEMPOTENT) {
            return armResult.value();
        }

        /*
         * A callback can acquire a higher ticket while the first marker waits
         * behind older work. The second marker drains every callback admitted
         * before the arm completed. The request-bound core confirmation then
         * requires that the same arm remains pending and starts its external
         * action lease from this final ordered point.
         */
        MailboxControlHandle<Boolean> confirmationHandle =
                mailbox.submitControl(before ->
                        before.healthy()
                                && coordinator.confirmArmHealthy(requestId));
        MailboxControlResult<Boolean> confirmation =
                confirmationHandle.await(controlTimeout);
        if (!confirmation.before().healthy()
                || !confirmation.after().healthy()
                || !confirmation.value()) {
            return ArmOutcome.FAULTED;
        }
        return armResult.value();
    }

    private static CallbackHealth callbackHealth(MailboxHealthSnapshot health) {
        if (health.failurePending()) {
            return health.firstFailure() == null
                    ? CallbackHealth.UNAVAILABLE
                    : CallbackHealth.FAULTED;
        }
        if (health.stopping()) {
            return CallbackHealth.STOPPING;
        }
        return health.firstFailure() == null
                ? CallbackHealth.HEALTHY
                : CallbackHealth.FAULTED;
    }

    private static InfrastructureFailure callbackFailure(
            MailboxHealthSnapshot health) {
        return health.firstFailure() == null
                ? null : health.firstFailure().failure();
    }

    private static Duration validateTimeout(Duration timeout) {
        Objects.requireNonNull(timeout, "controlTimeout");
        long nanos;
        try {
            nanos = timeout.toNanos();
        } catch (ArithmeticException failure) {
            throw new IllegalArgumentException(
                    "control timeout is too large", failure);
        }
        if (nanos <= 0) {
            throw new IllegalArgumentException(
                    "control timeout must be positive");
        }
        return timeout;
    }
}
