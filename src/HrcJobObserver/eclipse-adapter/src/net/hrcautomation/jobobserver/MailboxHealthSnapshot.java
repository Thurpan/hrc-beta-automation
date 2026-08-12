package net.hrcautomation.jobobserver;

/** Authoritative adapter health at one ordered mailbox worker point. */
record MailboxHealthSnapshot(
        InfrastructureIncident firstFailure,
        boolean failurePending,
        boolean stopping) {

    MailboxHealthSnapshot {
        if (firstFailure != null && !failurePending) {
            throw new IllegalArgumentException(
                    "published failure requires failure-pending state");
        }
    }

    boolean healthy() {
        return firstFailure == null && !failurePending && !stopping;
    }
}
