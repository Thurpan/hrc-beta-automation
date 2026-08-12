package net.hrcautomation.jobobserver;

import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.Objects;

/** Reads one bounded printable-ASCII protocol frame without unbounded buffering. */
final class BoundedAsciiLineReader {
    private final InputStream input;
    private final int maximumBytes;

    BoundedAsciiLineReader(InputStream input, int maximumBytes) {
        this.input = Objects.requireNonNull(input, "input");
        if (maximumBytes < 1) {
            throw new IllegalArgumentException("maximumBytes must be positive");
        }
        this.maximumBytes = maximumBytes;
    }

    String readLine() throws IOException, ProtocolFailure {
        byte[] bytes = new byte[maximumBytes];
        int length = 0;
        while (true) {
            int value = input.read();
            if (value < 0) {
                if (length == 0) {
                    return null;
                }
                throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
            }
            if (value == '\n') {
                if (length == 0) {
                    throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
                }
                return new String(bytes, 0, length, StandardCharsets.US_ASCII);
            }
            if (value == '\r') {
                int following = input.read();
                if (following != '\n' || length == 0) {
                    throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
                }
                return new String(bytes, 0, length, StandardCharsets.US_ASCII);
            }
            if (length == maximumBytes) {
                throw new ProtocolFailure(TransportFailure.FRAME_TOO_LARGE);
            }
            if (value != '\t' && (value < 0x20 || value > 0x7e)) {
                throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
            }
            bytes[length++] = (byte) value;
        }
    }
}
