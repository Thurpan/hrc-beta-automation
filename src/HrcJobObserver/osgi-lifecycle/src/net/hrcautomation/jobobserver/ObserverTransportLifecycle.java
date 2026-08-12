package net.hrcautomation.jobobserver;

import java.time.Duration;

interface ObserverTransportLifecycle {
    TransportEndpoint start() throws Exception;

    void requireHealthy() throws Exception;

    void closeAndAwait(Duration timeout) throws Exception;
}
