package net.hrcautomation.jobobserver;

/** Authoritative callback-adapter state at a checkpoint barrier. */
enum CallbackHealth {
    HEALTHY,
    FAULTED,
    STOPPING,
    UNAVAILABLE
}
