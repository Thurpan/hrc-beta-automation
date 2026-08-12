package net.hrcautomation.jobobserver;

/** Revokes endpoint discovery and wipes any credential copy owned by a publisher. */
interface ObserverEndpointPublication {
    void close() throws Exception;
}
