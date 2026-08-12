package net.hrcautomation.jobobserver;

/** Internal checked failure with an enumerated, non-sensitive wire cause. */
final class ProtocolFailure extends Exception {
    private static final long serialVersionUID = 1L;

    private final TransportFailure reason;

    ProtocolFailure(TransportFailure reason) {
        super(reason.name());
        this.reason = reason;
    }

    TransportFailure reason() {
        return reason;
    }
}
