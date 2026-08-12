package net.hrcautomation.jobobserver;

import java.time.Duration;
import java.util.Objects;

/** Explicit bounds for one offline-assembled observer lifecycle. */
final class ObserverLifecycleConfiguration {
    private final OperationProfileSet profiles;
    private final int requestCapacity;
    private final int jobCapacity;
    private final int replayCapacity;
    private final int mailboxCapacity;
    private final int baselineJobCapacity;
    private final Duration controlTimeout;
    private final Duration socketTimeout;
    private final Duration lifecycleTimeout;

    ObserverLifecycleConfiguration(
            OperationProfileSet profiles,
            int requestCapacity,
            int jobCapacity,
            int replayCapacity,
            int mailboxCapacity,
            int baselineJobCapacity,
            Duration controlTimeout,
            Duration socketTimeout,
            Duration lifecycleTimeout) {
        this.profiles = Objects.requireNonNull(profiles, "profiles");
        this.requestCapacity = requirePositive(requestCapacity, "requestCapacity");
        this.jobCapacity = requirePositive(jobCapacity, "jobCapacity");
        this.replayCapacity = requirePositive(replayCapacity, "replayCapacity");
        this.mailboxCapacity = requirePositive(mailboxCapacity, "mailboxCapacity");
        this.baselineJobCapacity = requirePositive(
                baselineJobCapacity, "baselineJobCapacity");
        if (replayCapacity > ObserverCheckpoint.MAX_REPLAY_EVENTS) {
            throw new IllegalArgumentException(
                    "replayCapacity exceeds checkpoint capacity");
        }
        this.controlTimeout = requirePositive(controlTimeout, "controlTimeout");
        this.socketTimeout = requirePositive(socketTimeout, "socketTimeout");
        this.lifecycleTimeout = requirePositive(
                lifecycleTimeout, "lifecycleTimeout");
    }

    OperationProfileSet profiles() {
        return profiles;
    }

    int requestCapacity() {
        return requestCapacity;
    }

    int jobCapacity() {
        return jobCapacity;
    }

    int replayCapacity() {
        return replayCapacity;
    }

    int mailboxCapacity() {
        return mailboxCapacity;
    }

    int baselineJobCapacity() {
        return baselineJobCapacity;
    }

    Duration controlTimeout() {
        return controlTimeout;
    }

    Duration socketTimeout() {
        return socketTimeout;
    }

    Duration lifecycleTimeout() {
        return lifecycleTimeout;
    }

    private static int requirePositive(int value, String field) {
        if (value < 1) {
            throw new IllegalArgumentException(field + " must be positive");
        }
        return value;
    }

    private static Duration requirePositive(Duration value, String field) {
        Objects.requireNonNull(value, field);
        try {
            if (value.toNanos() <= 0) {
                throw new IllegalArgumentException(field + " must be positive");
            }
        } catch (ArithmeticException failure) {
            throw new IllegalArgumentException(field + " is too large", failure);
        }
        return value;
    }
}
