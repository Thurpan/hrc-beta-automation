package net.hrcautomation.jobobserver;

/** Fail-closed local transport failures. */
enum TransportFailure {
    AUTHENTICATION_FAILED,
    PROTOCOL_VIOLATION,
    SESSION_MISMATCH,
    CHECKPOINT_MISMATCH,
    FRAME_TOO_LARGE,
    CONTROL_FAILURE,
    SERIALISATION_FAILED,
    INTERNAL_FAILURE,
    SHUTDOWN_TIMEOUT
}
