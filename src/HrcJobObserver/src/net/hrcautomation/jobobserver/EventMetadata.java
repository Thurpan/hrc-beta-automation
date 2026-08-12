package net.hrcautomation.jobobserver;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;

record EventMetadata(
        long sequence,
        Instant eventUtc,
        long monotonicNanos,
        UUID sessionId) {

    EventMetadata {
        if (sequence <= 0) {
            throw new IllegalArgumentException("sequence must be positive");
        }
        Objects.requireNonNull(eventUtc, "eventUtc");
        Objects.requireNonNull(sessionId, "sessionId");
    }
}
