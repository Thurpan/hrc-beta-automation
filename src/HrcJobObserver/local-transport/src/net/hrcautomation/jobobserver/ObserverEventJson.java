package net.hrcautomation.jobobserver;

import java.util.Objects;

/** Manual, allow-list-only JSON projection of observer events. */
final class ObserverEventJson {
    private ObserverEventJson() {
    }

    static String encode(ObserverEvent event) {
        Objects.requireNonNull(event, "event");
        StringBuilder output = new StringBuilder(512);
        output.append('{');
        firstField(output, "type", type(event));
        common(output, event.metadata());
        if (event instanceof ArmAcceptedEvent arm) {
            field(output, "requestId", arm.requestId().toString());
            field(output, "operation", arm.operation().name());
            field(output, "expectedJobName", arm.expectedJobName());
            number(output, "deadlineNanos", arm.deadlineNanos());
        } else if (event instanceof ArmConfirmedEvent arm) {
            field(output, "requestId", arm.requestId().toString());
            field(output, "operation", arm.operation().name());
            field(output, "expectedJobName", arm.expectedJobName());
            number(output, "deadlineNanos", arm.deadlineNanos());
        } else if (event instanceof JobScheduledEvent scheduled) {
            jobEvent(output, scheduled.requestId().toString(),
                    scheduled.operation(), scheduled.jobId(), scheduled.job());
        } else if (event instanceof JobRunningEvent running) {
            jobEvent(output, running.requestId().toString(),
                    running.operation(), running.jobId(), running.job());
        } else if (event instanceof JobRunningRejectedEvent rejected) {
            jobEvent(output, rejected.requestId().toString(),
                    rejected.operation(), rejected.jobId(), rejected.job());
            field(output, "rejectionReason", rejected.rejectionReason().name());
        } else if (event instanceof JobTerminalEvent terminal) {
            jobEvent(output, terminal.requestId().toString(),
                    terminal.operation(), terminal.jobId(), terminal.job());
            terminal(output, terminal.result(), terminal.statusSeverity(),
                    terminal.statusOk(), terminal.statusCode(),
                    terminal.statusPlugin(), terminal.statusPluginOmitted(),
                    terminal.runningSeen());
        } else if (event instanceof JobTerminalRejectedEvent terminal) {
            jobEvent(output, terminal.requestId().toString(),
                    terminal.operation(), terminal.jobId(), terminal.job());
            terminal(output, terminal.observedResult(), terminal.statusSeverity(),
                    terminal.statusOk(), terminal.statusCode(),
                    terminal.statusPlugin(), terminal.statusPluginOmitted(),
                    terminal.runningSeen());
            field(output, "rejectionReason", terminal.rejectionReason().name());
        } else if (event instanceof ObserverFaultEvent fault) {
            field(output, "reason", fault.reason().name());
        } else {
            throw new IllegalArgumentException("unsupported observer event");
        }
        output.append('}');
        return output.toString();
    }

    private static void common(StringBuilder output, EventMetadata metadata) {
        number(output, "sequence", metadata.sequence());
        field(output, "eventUtc", metadata.eventUtc().toString());
        number(output, "monotonicNanos", metadata.monotonicNanos());
        field(output, "sessionId", metadata.sessionId().toString());
    }

    private static void jobEvent(
            StringBuilder output,
            String requestId,
            OperationKind operation,
            long jobId,
            JobDescriptor job) {
        field(output, "requestId", requestId);
        field(output, "operation", operation.name());
        number(output, "jobId", jobId);
        output.append(",\"job\":{");
        firstField(output, "bundle", job.bundleSymbolicName());
        field(output, "version", job.bundleVersion());
        field(output, "class", job.className());
        field(output, "name", job.name());
        bool(output, "user", job.user());
        bool(output, "system", job.system());
        output.append('}');
    }

    private static void terminal(
            StringBuilder output,
            TerminalResult result,
            int severity,
            boolean ok,
            int code,
            String plugin,
            boolean pluginOmitted,
            boolean runningSeen) {
        field(output, "result", result.name());
        number(output, "statusSeverity", severity);
        bool(output, "statusOk", ok);
        number(output, "statusCode", code);
        field(output, "statusPlugin", plugin);
        bool(output, "statusPluginOmitted", pluginOmitted);
        bool(output, "runningSeen", runningSeen);
    }

    private static String type(ObserverEvent event) {
        if (event instanceof ArmAcceptedEvent) {
            return "ARM_ACCEPTED";
        }
        if (event instanceof ArmConfirmedEvent) {
            return "ARM_CONFIRMED";
        }
        if (event instanceof JobScheduledEvent) {
            return "JOB_SCHEDULED";
        }
        if (event instanceof JobRunningEvent) {
            return "JOB_RUNNING";
        }
        if (event instanceof JobRunningRejectedEvent) {
            return "JOB_RUNNING_REJECTED";
        }
        if (event instanceof JobTerminalEvent) {
            return "JOB_TERMINAL";
        }
        if (event instanceof JobTerminalRejectedEvent) {
            return "JOB_TERMINAL_REJECTED";
        }
        if (event instanceof ObserverFaultEvent) {
            return "OBSERVER_FAULT";
        }
        throw new IllegalArgumentException("unsupported observer event");
    }

    private static void firstField(StringBuilder output, String name, String value) {
        quoted(output, name);
        output.append(':');
        quoted(output, value);
    }

    private static void field(StringBuilder output, String name, String value) {
        output.append(',');
        firstField(output, name, value);
    }

    private static void number(StringBuilder output, String name, long value) {
        output.append(',');
        quoted(output, name);
        output.append(':').append(value);
    }

    private static void bool(StringBuilder output, String name, boolean value) {
        output.append(',');
        quoted(output, name);
        output.append(':').append(value);
    }

    private static void quoted(StringBuilder output, String value) {
        output.append('"');
        for (int index = 0; index < value.length(); index++) {
            char character = value.charAt(index);
            switch (character) {
                case '"' -> output.append("\\\"");
                case '\\' -> output.append("\\\\");
                case '\b' -> output.append("\\b");
                case '\f' -> output.append("\\f");
                case '\n' -> output.append("\\n");
                case '\r' -> output.append("\\r");
                case '\t' -> output.append("\\t");
                default -> {
                    if (character < 0x20 || character > 0x7e) {
                        output.append(String.format("\\u%04x", (int) character));
                    } else {
                        output.append(character);
                    }
                }
            }
        }
        output.append('"');
    }

}
