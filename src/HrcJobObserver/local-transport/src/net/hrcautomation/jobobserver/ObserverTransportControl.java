package net.hrcautomation.jobobserver;

import java.util.UUID;

/**
 * Atomic control boundary supplied by the future runtime assembly. Implementors
 * must fence callback delivery before returning a checkpoint or accepting an arm.
 */
interface ObserverTransportControl {
    UUID sessionId();

    ObserverCheckpoint checkpoint(long lastSeenSequence);

    ArmOutcome armIfHealthy(
            UUID requestId,
            OperationKind operation,
            String expectedJobName,
            long timeoutNanos);
}
