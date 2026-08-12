package net.hrcautomation.jobobserver;

import java.time.Duration;

/** Package-private adapter around the already bounded loopback server. */
final class LocalObserverTransportFactory implements ObserverTransportFactory {
    @Override
    public ObserverTransportLifecycle create(
            ObserverTransportControl control,
            byte[] bearerToken,
            Duration socketTimeout) {
        LocalObserverServer server = new LocalObserverServer(
                control, bearerToken, socketTimeout);
        return new ObserverTransportLifecycle() {
            @Override
            public TransportEndpoint start() throws Exception {
                return server.start();
            }

            @Override
            public void requireHealthy() throws Exception {
                if (server.firstFailure() != null) {
                    throw new ObserverLifecycleException(
                            ObserverLifecycleException.Reason.TRANSPORT_HEALTH_FAILED);
                }
            }

            @Override
            public void closeAndAwait(Duration timeout) throws Exception {
                if (!server.closeAndAwait(timeout).clean()) {
                    throw new ObserverLifecycleException(
                            ObserverLifecycleException.Reason.TRANSPORT_SHUTDOWN_UNCLEAN);
                }
            }
        };
    }
}
