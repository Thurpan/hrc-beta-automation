package net.hrcautomation.jobobserver;

import java.util.Objects;
import java.util.UUID;

/** Non-secret endpoint metadata. The bearer token is deliberately absent. */
record TransportEndpoint(int protocolVersion, String address, int port, UUID sessionId) {
    TransportEndpoint {
        if (protocolVersion != LocalObserverServer.PROTOCOL_VERSION) {
            throw new IllegalArgumentException("unsupported protocol version");
        }
        if (!"127.0.0.1".equals(address)) {
            throw new IllegalArgumentException("transport must use IPv4 loopback");
        }
        if (port < 1 || port > 65_535) {
            throw new IllegalArgumentException("port is invalid");
        }
        Objects.requireNonNull(sessionId, "sessionId");
    }
}
