package net.hrcautomation.jobobserver;

import java.time.Instant;

/** Captures process-local monotonic time before wall-clock time at callback entry. */
final class SystemObservationClock implements ObservationClock {
    @Override
    public ObservationTime capture() {
        long nanos = System.nanoTime();
        return new ObservationTime(Instant.now(), nanos);
    }
}
