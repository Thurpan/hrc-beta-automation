package net.hrcautomation.jobobserver;

/** Transactional publication: a thrown publish call must retain no credential. */
interface ObserverEndpointPublisher {
    ObserverEndpointPublication publish(
            TransportEndpoint endpoint, byte[] bearerToken) throws Exception;
}
