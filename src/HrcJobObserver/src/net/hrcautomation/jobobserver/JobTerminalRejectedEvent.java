package net.hrcautomation.jobobserver;

import java.util.Objects;
import java.util.UUID;

/** Exact terminal projection that is explicitly unsafe for workflow progress. */
record JobTerminalRejectedEvent(
        EventMetadata metadata,
        UUID requestId,
        OperationKind operation,
        long jobId,
        JobDescriptor job,
        TerminalResult observedResult,
        int statusSeverity,
        boolean statusOk,
        int statusCode,
        String statusPlugin,
        boolean statusPluginOmitted,
        boolean runningSeen,
        FaultReason rejectionReason) implements ObserverEvent {

    JobTerminalRejectedEvent {
        Objects.requireNonNull(metadata, "metadata");
        Objects.requireNonNull(requestId, "requestId");
        Objects.requireNonNull(operation, "operation");
        if (jobId <= 0) {
            throw new IllegalArgumentException("jobId must be positive");
        }
        Objects.requireNonNull(job, "job");
        Objects.requireNonNull(observedResult, "observedResult");
        Objects.requireNonNull(statusPlugin, "statusPlugin");
        Objects.requireNonNull(rejectionReason, "rejectionReason");
        if (rejectionReason != FaultReason.UNKNOWN_TERMINAL_STATUS
                && rejectionReason != FaultReason.DONE_BEFORE_RUNNING
                && rejectionReason != FaultReason.TERMINAL_EVENT_REJECTED
                && rejectionReason != FaultReason.STATUS_PLUGIN_OMITTED) {
            throw new IllegalArgumentException("unsupported terminal rejection reason");
        }
        TerminalResult derived = terminalResult(statusSeverity, statusOk);
        if (derived != observedResult) {
            throw new IllegalArgumentException(
                    "observedResult contradicts status severity or OK flag");
        }
        if (rejectionReason == FaultReason.UNKNOWN_TERMINAL_STATUS
                && observedResult != TerminalResult.UNKNOWN) {
            throw new IllegalArgumentException("unknown status rejection requires UNKNOWN result");
        }
        if (rejectionReason == FaultReason.DONE_BEFORE_RUNNING
                && (runningSeen
                || observedResult == TerminalResult.UNKNOWN
                || observedResult == TerminalResult.CANCEL
                || statusPluginOmitted)) {
            throw new IllegalArgumentException("invalid missing-RUNNING rejection");
        }
        if (rejectionReason == FaultReason.STATUS_PLUGIN_OMITTED
                && (!statusPluginOmitted || observedResult == TerminalResult.UNKNOWN)) {
            throw new IllegalArgumentException("plugin rejection requires an omitted plugin");
        }
        if (rejectionReason == FaultReason.TERMINAL_EVENT_REJECTED
                && observedResult == TerminalResult.UNKNOWN) {
            throw new IllegalArgumentException(
                    "prior-fault rejection cannot hide an unknown terminal status");
        }
    }

    private static TerminalResult terminalResult(int severity, boolean ok) {
        if (severity == StatusSnapshot.OK && ok) {
            return TerminalResult.OK;
        }
        if (severity == StatusSnapshot.CANCEL && !ok) {
            return TerminalResult.CANCEL;
        }
        if (severity == StatusSnapshot.ERROR && !ok) {
            return TerminalResult.ERROR;
        }
        return TerminalResult.UNKNOWN;
    }
}
