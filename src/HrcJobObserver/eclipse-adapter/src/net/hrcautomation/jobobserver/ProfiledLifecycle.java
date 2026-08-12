package net.hrcautomation.jobobserver;

import java.time.Instant;
import java.util.Objects;

record ProfiledLifecycle(LifecycleInput input) implements CapturedLifecycle {
    ProfiledLifecycle {
        Objects.requireNonNull(input, "input");
    }

    @Override
    public Instant observedUtc() {
        return input.observedUtc();
    }

    @Override
    public long observedNanos() {
        return input.observedNanos();
    }
}
