package net.hrcautomation.jobobserver;

import java.util.Objects;
import java.util.UUID;

record JobTerminalEvent(
        EventMetadata metadata,
        UUID requestId,
        OperationKind operation,
        long jobId,
        JobDescriptor job,
        TerminalResult result,
        int statusSeverity,
        boolean statusOk,
        int statusCode,
        String statusPlugin,
        boolean statusPluginOmitted,
        boolean runningSeen) implements ObserverEvent {

    JobTerminalEvent {
        Objects.requireNonNull(metadata, "metadata");
        Objects.requireNonNull(requestId, "requestId");
        Objects.requireNonNull(operation, "operation");
        if (jobId <= 0) {
            throw new IllegalArgumentException("jobId must be positive");
        }
        Objects.requireNonNull(job, "job");
        Objects.requireNonNull(result, "result");
        Objects.requireNonNull(statusPlugin, "statusPlugin");
        if (statusPluginOmitted) {
            throw new IllegalArgumentException("trusted terminal event requires status plugin");
        }
        TerminalResult derived = terminalResult(statusSeverity, statusOk);
        if (derived != result) {
            throw new IllegalArgumentException("result contradicts status severity or OK flag");
        }
        if (result == TerminalResult.UNKNOWN) {
            throw new IllegalArgumentException("unknown results use JobTerminalRejectedEvent");
        }
        if (!runningSeen && result != TerminalResult.CANCEL) {
            throw new IllegalArgumentException(
                    "only queued cancellation may finish without RUNNING");
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
