package net.hrcautomation.jobobserver;

/** Safe, non-sensitive failure raised by the offline Bundle lifecycle owner. */
final class ObserverLifecycleException extends Exception {
    private static final long serialVersionUID = 1L;

    enum Reason {
        BOOTSTRAP_DISABLED,
        ACTIVATOR_STATE_INVALID,
        RUNTIME_ASSEMBLY_FAILED,
        MAILBOX_START_FAILED,
        LISTENER_REGISTRATION_FAILED,
        BASELINE_QUERY_FAILED,
        BASELINE_TOO_LARGE,
        RELEVANT_JOB_PRESENT,
        SOURCE_MISMATCH,
        STARTUP_CALLBACK_FAILED,
        STARTUP_CALLBACK_TIMEOUT,
        STARTUP_HEALTH_FAILED,
        TRANSPORT_START_FAILED,
        TRANSPORT_HEALTH_FAILED,
        ENDPOINT_PUBLICATION_FAILED,
        ENDPOINT_REVOCATION_FAILED,
        TRANSPORT_SHUTDOWN_UNCLEAN,
        LISTENER_REMOVAL_FAILED,
        LISTENER_DRAIN_TIMEOUT,
        MAILBOX_SHUTDOWN_UNCLEAN,
        CALLBACKS_REMAIN,
        TOKEN_INVALID
    }

    private final Reason reason;

    ObserverLifecycleException(Reason reason) {
        super(java.util.Objects.requireNonNull(reason, "reason").name());
        this.reason = reason;
    }

    Reason reason() {
        return reason;
    }
}
