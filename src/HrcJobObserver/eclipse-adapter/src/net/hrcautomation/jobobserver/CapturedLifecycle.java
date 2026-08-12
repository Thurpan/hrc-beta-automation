package net.hrcautomation.jobobserver;

import java.time.Instant;

/** Bounded callback projection accepted by the in-process mailbox. */
sealed interface CapturedLifecycle
        permits ProfiledLifecycle, SourceMismatchLifecycle {
    Instant observedUtc();

    long observedNanos();
}
