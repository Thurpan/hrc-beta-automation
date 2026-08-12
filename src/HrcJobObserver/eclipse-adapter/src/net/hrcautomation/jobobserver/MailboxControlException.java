package net.hrcautomation.jobobserver;

/** Bounded, message-free failure returned to the local runtime assembly. */
final class MailboxControlException extends RuntimeException {
    private static final long serialVersionUID = 1L;

    private final MailboxControlFailure failure;

    MailboxControlException(MailboxControlFailure failure) {
        super(failure.name(), null, false, false);
        this.failure = failure;
    }

    MailboxControlFailure failure() {
        return failure;
    }
}
