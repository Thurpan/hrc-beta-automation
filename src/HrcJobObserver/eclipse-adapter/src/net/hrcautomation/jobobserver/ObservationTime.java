package net.hrcautomation.jobobserver;

import java.time.Instant;
import java.util.Objects;

record ObservationTime(Instant utc, long monotonicNanos) {
    ObservationTime {
        Objects.requireNonNull(utc, "utc");
    }
}
