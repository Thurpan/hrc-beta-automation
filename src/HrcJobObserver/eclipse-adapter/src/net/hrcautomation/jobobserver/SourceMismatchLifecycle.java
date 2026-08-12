package net.hrcautomation.jobobserver;

import java.time.Instant;
import java.util.Objects;

/** Timestamp-only evidence that a recognised Job class came from the wrong Bundle. */
record SourceMismatchLifecycle(Instant observedUtc, long observedNanos)
        implements CapturedLifecycle {
    SourceMismatchLifecycle {
        Objects.requireNonNull(observedUtc, "observedUtc");
    }
}
