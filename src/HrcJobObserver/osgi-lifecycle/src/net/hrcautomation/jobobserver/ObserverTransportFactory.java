package net.hrcautomation.jobobserver;

import java.time.Duration;

interface ObserverTransportFactory {
    ObserverTransportLifecycle create(
            ObserverTransportControl control,
            byte[] bearerToken,
            Duration socketTimeout) throws Exception;
}
