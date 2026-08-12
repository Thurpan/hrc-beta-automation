package net.hrcautomation.jobobserver;

import java.time.Instant;
import java.util.Objects;
import java.util.UUID;
import java.util.function.LongSupplier;
import java.util.function.Supplier;

/** Injected runtime sources; the default Bundle activator never constructs these. */
final class ObserverLifecycleDependencies {
    private final UUID sessionId;
    private final LongSupplier monotonicClock;
    private final Supplier<Instant> wallClock;
    private final ObservationClock observationClock;
    private final BundleIdentityResolver bundleResolver;
    private final JobManagerAccess jobManager;
    private final ObserverEndpointPublisher endpointPublisher;
    private final ObserverTransportFactory transportFactory;
    private final Supplier<byte[]> tokenSource;

    ObserverLifecycleDependencies(
            UUID sessionId,
            LongSupplier monotonicClock,
            Supplier<Instant> wallClock,
            ObservationClock observationClock,
            BundleIdentityResolver bundleResolver,
            JobManagerAccess jobManager,
            ObserverEndpointPublisher endpointPublisher,
            ObserverTransportFactory transportFactory,
            Supplier<byte[]> tokenSource) {
        this.sessionId = Objects.requireNonNull(sessionId, "sessionId");
        this.monotonicClock = Objects.requireNonNull(
                monotonicClock, "monotonicClock");
        this.wallClock = Objects.requireNonNull(wallClock, "wallClock");
        this.observationClock = Objects.requireNonNull(
                observationClock, "observationClock");
        this.bundleResolver = Objects.requireNonNull(
                bundleResolver, "bundleResolver");
        this.jobManager = Objects.requireNonNull(jobManager, "jobManager");
        this.endpointPublisher = Objects.requireNonNull(
                endpointPublisher, "endpointPublisher");
        this.transportFactory = Objects.requireNonNull(
                transportFactory, "transportFactory");
        this.tokenSource = Objects.requireNonNull(tokenSource, "tokenSource");
    }

    UUID sessionId() {
        return sessionId;
    }

    LongSupplier monotonicClock() {
        return monotonicClock;
    }

    Supplier<Instant> wallClock() {
        return wallClock;
    }

    ObservationClock observationClock() {
        return observationClock;
    }

    BundleIdentityResolver bundleResolver() {
        return bundleResolver;
    }

    JobManagerAccess jobManager() {
        return jobManager;
    }

    ObserverEndpointPublisher endpointPublisher() {
        return endpointPublisher;
    }

    ObserverTransportFactory transportFactory() {
        return transportFactory;
    }

    byte[] createToken() {
        return tokenSource.get();
    }
}
