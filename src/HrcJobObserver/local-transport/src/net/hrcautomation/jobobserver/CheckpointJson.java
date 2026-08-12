package net.hrcautomation.jobobserver;

import java.util.Objects;

/** Allow-list-only checkpoint serialisation. */
final class CheckpointJson {
    static final int MAX_RESPONSE_BYTES = 256 * 1024;

    private CheckpointJson() {
    }

    static String encode(ObserverCheckpoint checkpoint) throws ProtocolFailure {
        Objects.requireNonNull(checkpoint, "checkpoint");
        StringBuilder output = new StringBuilder(1024);
        output.append("{\"v\":").append(LocalObserverServer.PROTOCOL_VERSION);
        string(output, "sessionId", checkpoint.sessionId().toString());
        number(output, "barrierId", checkpoint.barrierId());
        number(output, "afterSequence", checkpoint.afterSequence());
        string(output, "disposition", checkpoint.replay().disposition().name());
        number(output, "oldestAvailable", checkpoint.replay().oldestAvailable());
        number(output, "latestAvailable", checkpoint.replay().latestAvailable());
        bool(output, "actionable", checkpoint.actionable());
        nullableString(output, "observerFault", checkpoint.observerFault());
        string(output, "callbackHealth", checkpoint.callbackHealth().name());
        nullableString(output, "callbackFailure", checkpoint.callbackFailure());
        output.append(",\"events\":[");
        boolean first = true;
        for (ObserverEvent event : checkpoint.replay().events()) {
            if (!first) {
                output.append(',');
            }
            output.append(ObserverEventJson.encode(event));
            first = false;
        }
        output.append("]}");
        String encoded = output.toString();
        if (encoded.length() > MAX_RESPONSE_BYTES) {
            throw new ProtocolFailure(TransportFailure.SERIALISATION_FAILED);
        }
        return encoded;
    }

    private static void string(StringBuilder output, String name, String value) {
        output.append(",\"").append(name).append("\":\"")
                .append(value).append('"');
    }

    private static void nullableString(StringBuilder output, String name, Enum<?> value) {
        output.append(",\"").append(name).append("\":");
        if (value == null) {
            output.append("null");
        } else {
            output.append('"').append(value.name()).append('"');
        }
    }

    private static void number(StringBuilder output, String name, long value) {
        output.append(",\"").append(name).append("\":").append(value);
    }

    private static void bool(StringBuilder output, String name, boolean value) {
        output.append(",\"").append(name).append("\":").append(value);
    }
}
