package net.hrcautomation.jobobserver;

/** Failures in the callback hand-off, separate from HRC Job state. */
enum InfrastructureFailure {
    CALLBACK_CAPTURE_FAILED,
    CALLBACK_QUEUE_OVERFLOW,
    CALLBACK_DISPATCH_FAILED
}
