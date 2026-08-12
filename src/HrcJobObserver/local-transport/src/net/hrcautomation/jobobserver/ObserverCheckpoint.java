package net.hrcautomation.jobobserver;

import java.util.Objects;
import java.util.UUID;

/**
 * One linearised replay and health view. A future runtime assembly must create
 * it only after all callbacks before its barrier have reached the core.
 */
record ObserverCheckpoint(
        UUID sessionId,
        long barrierId,
        long afterSequence,
        ReplayQuery replay,
        FaultReason observerFault,
        CallbackHealth callbackHealth,
        InfrastructureFailure callbackFailure) {

    static final int MAX_REPLAY_EVENTS = 256;

    ObserverCheckpoint {
        Objects.requireNonNull(sessionId, "sessionId");
        if (barrierId <= 0) {
            throw new IllegalArgumentException("barrierId must be positive");
        }
        if (afterSequence < 0) {
            throw new IllegalArgumentException("afterSequence must not be negative");
        }
        Objects.requireNonNull(replay, "replay");
        Objects.requireNonNull(replay.disposition(), "replay disposition");
        Objects.requireNonNull(callbackHealth, "callbackHealth");
        if (callbackHealth == CallbackHealth.HEALTHY && callbackFailure != null) {
            throw new IllegalArgumentException("healthy callback state cannot have a failure");
        }
        if (callbackHealth == CallbackHealth.FAULTED && callbackFailure == null) {
            throw new IllegalArgumentException("faulted callback state requires a failure");
        }
        if (replay.events().size() > MAX_REPLAY_EVENTS) {
            throw new IllegalArgumentException("checkpoint replay is too large");
        }
        validateReplay(afterSequence, replay, sessionId, observerFault);
    }

    private static void validateReplay(
            long afterSequence,
            ReplayQuery replay,
            UUID sessionId,
            FaultReason observerFault) {
        long oldest = replay.oldestAvailable();
        long latest = replay.latestAvailable();
        if (oldest < 1 || latest < 0 || latest == Long.MAX_VALUE
                || oldest > latest + 1) {
            throw new IllegalArgumentException("replay bounds are invalid");
        }

        if (replay.disposition() != ReplayQuery.Disposition.OK
                && !replay.events().isEmpty()) {
            throw new IllegalArgumentException("non-OK replay cannot contain events");
        }
        switch (replay.disposition()) {
            case OK -> {
                if (afterSequence > latest) {
                    throw new IllegalArgumentException("OK replay cursor is ahead");
                }
                if (replay.events().isEmpty() && afterSequence != latest) {
                    throw new IllegalArgumentException("OK empty replay is incomplete");
                }
            }
            case GAP -> {
                if (oldest <= 1 || afterSequence >= oldest - 1) {
                    throw new IllegalArgumentException("GAP replay bounds are inconsistent");
                }
            }
            case CURSOR_AHEAD -> {
                if (afterSequence <= latest) {
                    throw new IllegalArgumentException(
                            "CURSOR_AHEAD replay bounds are inconsistent");
                }
            }
        }

        long expectedSequence = afterSequence;
        for (ObserverEvent event : replay.events()) {
            if (!sessionId.equals(event.metadata().sessionId())) {
                throw new IllegalArgumentException("event belongs to another session");
            }
            if (expectedSequence == Long.MAX_VALUE
                    || event.sequence() != expectedSequence + 1) {
                throw new IllegalArgumentException("replay events are not contiguous");
            }
            if (event.sequence() < oldest || event.sequence() > latest) {
                throw new IllegalArgumentException("event falls outside replay bounds");
            }
            if (event instanceof ObserverFaultEvent fault
                    && fault.reason() != observerFault) {
                throw new IllegalArgumentException("fault event and fault state differ");
            }
            if (isRejected(event) && observerFault == null) {
                throw new IllegalArgumentException(
                        "rejected event requires a faulted observer state");
            }
            expectedSequence = event.sequence();
        }
        if (!replay.events().isEmpty() && expectedSequence != latest) {
            throw new IllegalArgumentException("OK replay does not reach the latest event");
        }
    }

    private static boolean isRejected(ObserverEvent event) {
        return event instanceof JobRunningRejectedEvent
                || event instanceof JobTerminalRejectedEvent;
    }

    boolean actionable() {
        return replay.disposition() == ReplayQuery.Disposition.OK
                && observerFault == null
                && callbackHealth == CallbackHealth.HEALTHY
                && callbackFailure == null;
    }
}
