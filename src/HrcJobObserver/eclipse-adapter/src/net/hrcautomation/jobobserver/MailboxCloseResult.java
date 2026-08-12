package net.hrcautomation.jobobserver;

/** Health result for the single-use in-process callback mailbox. */
record MailboxCloseResult(
        InfrastructureIncident firstFailure,
        boolean failureNotificationAttempted,
        boolean failureNotificationSucceeded,
        boolean workerTerminated) {
    MailboxCloseResult {
        if (failureNotificationSucceeded && !failureNotificationAttempted) {
            throw new IllegalArgumentException(
                    "a successful failure notification must be attempted");
        }
        if (firstFailure == null && failureNotificationAttempted) {
            throw new IllegalArgumentException(
                    "a failure notification requires an incident");
        }
    }

    boolean clean() {
        return firstFailure == null && workerTerminated;
    }
}
