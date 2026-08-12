package net.hrcautomation.jobobserver;

import java.lang.reflect.Constructor;
import java.lang.reflect.Modifier;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;
import org.eclipse.core.runtime.IProgressMonitor;
import org.eclipse.core.runtime.IStatus;
import org.eclipse.core.runtime.jobs.IJobChangeEvent;
import org.eclipse.core.runtime.jobs.IJobChangeListener;
import org.eclipse.core.runtime.jobs.Job;
import org.osgi.framework.BundleContext;

/** Dependency-free deterministic harness for the offline OSGi lifecycle layer. */
public final class ObserverOsgiLifecycleTest {
    private static final String BUNDLE = "net.holdemresources.calculator";
    private static final String VERSION = "4.1.1.202607211244";
    private static final UUID SESSION = new UUID(0, 512);
    private static final Instant UTC = Instant.parse("2026-08-12T12:00:00Z");
    private static final Duration TIMEOUT = Duration.ofSeconds(2);
    private static final long WAIT_MILLIS = 2_000;

    private ObserverOsgiLifecycleTest() {
    }

    public static void main(String[] args) throws Exception {
        List<TestCase> tests = List.of(
                test("activatorHasPublicDisabledDefault", () ->
                        activatorHasPublicDisabledDefault()),
                test("startsAndStopsInExactOrder", () ->
                        startsAndStopsInExactOrder()),
                test("rejectsRelevantBaseline", () ->
                        rejectsRelevantBaseline()),
                test("rejectsSourceMismatchBaseline", () ->
                        rejectsSourceMismatchBaseline()),
                test("ignoresUnknownBaselineBeforeBundleResolution", () ->
                        ignoresUnknownBaselineBeforeBundleResolution()),
                test("rejectsRelevantCallbackDuringBaseline", () ->
                        rejectsRelevantCallbackDuringBaseline()),
                test("rejectsCallbackAcrossStartupSeal", () ->
                        rejectsCallbackAcrossStartupSeal()),
                test("documentsDelayedDoneAfterEmptyBaselineGap", () ->
                        documentsDelayedDoneAfterEmptyBaselineGap()),
                test("rollsBackTransportStartFailure", () ->
                        rollsBackTransportStartFailure()),
                test("rollsBackPublicationFailure", () ->
                        rollsBackPublicationFailure()),
                test("removesListenerBeforeMailboxClose", () ->
                        removesListenerBeforeMailboxClose()),
                test("transportFailureStillAttemptsRemainingCleanup", () ->
                        transportFailureStillAttemptsRemainingCleanup()),
                test("staleCallbackAfterRemovalCannotReachMailbox", () ->
                        staleCallbackAfterRemovalCannotReachMailbox()),
                test("defaultActivatorCannotRetry", () ->
                        defaultActivatorCannotRetry()));

        int passed = 0;
        for (TestCase test : tests) {
            test.body().run();
            passed++;
            System.out.println("PASS " + test.name());
        }
        System.out.println("PASS " + passed + "/" + tests.size());
    }

    private static void activatorHasPublicDisabledDefault() throws Exception {
        assertTrue(Modifier.isPublic(HrcJobObserverActivator.class.getModifiers()));
        assertTrue(Modifier.isFinal(HrcJobObserverActivator.class.getModifiers()));
        Constructor<HrcJobObserverActivator> constructor =
                HrcJobObserverActivator.class.getConstructor();
        assertTrue(Modifier.isPublic(constructor.getModifiers()));

        HrcJobObserverActivator activator = constructor.newInstance();
        ObserverLifecycleException failure = assertThrowsResult(
                ObserverLifecycleException.class,
                () -> activator.start(new BundleContextStub()));
        assertEquals(
                ObserverLifecycleException.Reason.BOOTSTRAP_DISABLED,
                failure.reason());
        activator.stop(new BundleContextStub());
    }

    private static void startsAndStopsInExactOrder() throws Exception {
        Trace trace = new Trace();
        Fixture fixture = fixture(trace);
        fixture.lifecycle.start();
        assertEquals(ObserverBundleLifecycle.State.ACTIVE, fixture.lifecycle.state());
        fixture.lifecycle.stop();
        assertEquals(ObserverBundleLifecycle.State.STOPPED, fixture.lifecycle.state());
        assertEquals(List.of(
                "manager.add",
                "manager.find",
                "manager.find",
                "transport.start",
                "transport.health",
                "publisher.publish",
                "publication.close",
                "transport.close",
                "manager.remove"), trace.values());
        assertTrue(fixture.publisher.publishedTokenWiped());
        assertTrue(fixture.transport.factoryTokenWiped());
    }

    private static void rejectsRelevantBaseline() throws Exception {
        Trace trace = new Trace();
        Fixture fixture = fixture(trace);
        fixture.manager.snapshots.add(new Job[]{new FakeNashJob("existing")});
        ObserverLifecycleException failure = assertThrowsResult(
                ObserverLifecycleException.class, fixture.lifecycle::start);
        assertEquals(
                ObserverLifecycleException.Reason.RELEVANT_JOB_PRESENT,
                failure.reason());
        assertEquals(ObserverBundleLifecycle.State.STOPPED, fixture.lifecycle.state());
        assertFalse(trace.values().contains("transport.start"));
        assertEquals(1, fixture.manager.removeCalls.get());
    }

    private static void rejectsSourceMismatchBaseline() throws Exception {
        Trace trace = new Trace();
        Fixture fixture = fixture(
                trace,
                ignored -> new BundleIdentity("wrong.bundle", VERSION));
        fixture.manager.snapshots.add(new Job[]{new FakeNashJob("existing")});
        ObserverLifecycleException failure = assertThrowsResult(
                ObserverLifecycleException.class, fixture.lifecycle::start);
        assertEquals(
                ObserverLifecycleException.Reason.SOURCE_MISMATCH,
                failure.reason());
        assertFalse(trace.values().contains("transport.start"));
    }

    private static void ignoresUnknownBaselineBeforeBundleResolution()
            throws Exception {
        Trace trace = new Trace();
        AtomicInteger bundleReads = new AtomicInteger();
        Fixture fixture = fixture(trace, ignored -> {
            bundleReads.incrementAndGet();
            throw new AssertionError("unknown Job Bundle must not be resolved");
        });
        UnrelatedJob unknown = new UnrelatedJob();
        fixture.manager.snapshots.add(new Job[]{unknown});
        fixture.lifecycle.start();
        fixture.lifecycle.stop();
        assertEquals(0, bundleReads.get());
    }

    private static void rejectsRelevantCallbackDuringBaseline() throws Exception {
        Trace trace = new Trace();
        Fixture fixture = fixture(trace);
        fixture.manager.onFirstFind = () -> fixture.manager.fireScheduled(
                new FakeNashJob("during-baseline"));
        ObserverLifecycleException failure = assertThrowsResult(
                ObserverLifecycleException.class, fixture.lifecycle::start);
        assertEquals(
                ObserverLifecycleException.Reason.RELEVANT_JOB_PRESENT,
                failure.reason());
        assertFalse(trace.values().contains("transport.start"));
    }

    private static void rejectsCallbackAcrossStartupSeal() throws Exception {
        Trace trace = new Trace();
        CountDownLatch resolverEntered = new CountDownLatch(1);
        CountDownLatch releaseResolver = new CountDownLatch(1);
        AtomicInteger calls = new AtomicInteger();
        BundleIdentityResolver resolver = ignored -> {
            if (calls.getAndIncrement() == 0) {
                resolverEntered.countDown();
                awaitUnchecked(releaseResolver);
            }
            return new BundleIdentity(BUNDLE, VERSION);
        };
        Fixture fixture = fixture(trace, resolver);
        AtomicReference<Throwable> callbackFailure = new AtomicReference<>();
        fixture.manager.onFirstFind = () -> {
            Thread callback = new Thread(() -> {
                try {
                    fixture.manager.fireScheduled(new FakeNashJob("blocked"));
                } catch (Throwable failure) {
                    callbackFailure.set(failure);
                }
            }, "lifecycle-blocked-startup-callback");
            callback.setDaemon(true);
            callback.start();
            awaitUnchecked(resolverEntered);
        };
        AtomicReference<Throwable> startFailure = new AtomicReference<>();
        Thread starter = new Thread(() -> {
            try {
                fixture.lifecycle.start();
            } catch (Throwable failure) {
                startFailure.set(failure);
            }
        }, "lifecycle-start-across-seal");
        starter.setDaemon(true);
        starter.start();
        awaitUnchecked(resolverEntered);
        awaitPhase(fixture.lifecycle.listener(), ListenerRegistrationGate.Phase.SEALING);
        releaseResolver.countDown();
        starter.join(WAIT_MILLIS);
        assertFalse(starter.isAlive());
        assertEquals(null, callbackFailure.get());
        assertTrue(startFailure.get() instanceof ObserverLifecycleException);
        ObserverLifecycleException failure =
                (ObserverLifecycleException) startFailure.get();
        assertEquals(
                ObserverLifecycleException.Reason.RELEVANT_JOB_PRESENT,
                failure.reason());
        assertEquals(0L, fixture.lifecycle.listener().callbacksInFlight());
    }

    private static void documentsDelayedDoneAfterEmptyBaselineGap()
            throws Exception {
        Trace trace = new Trace();
        Fixture fixture = fixture(trace);
        fixture.lifecycle.start();
        fixture.manager.fireDone(new FakeNashJob("pre-registration-done"));
        assertEquals(ObserverBundleLifecycle.State.ACTIVE, fixture.lifecycle.state());
        ObserverCheckpoint checkpoint = fixture.lifecycle.checkpoint(0);
        assertFalse(checkpoint.actionable());
        fixture.lifecycle.stop();
        assertEquals(ObserverBundleLifecycle.State.STOPPED, fixture.lifecycle.state());
    }

    private static void rollsBackTransportStartFailure() throws Exception {
        Trace trace = new Trace();
        Fixture fixture = fixture(trace);
        fixture.transport.failStart = true;
        ObserverLifecycleException failure = assertThrowsResult(
                ObserverLifecycleException.class, fixture.lifecycle::start);
        assertEquals(
                ObserverLifecycleException.Reason.TRANSPORT_START_FAILED,
                failure.reason());
        assertOrder(trace, "transport.start", "transport.close", "manager.remove");
        assertEquals(ObserverBundleLifecycle.State.STOPPED, fixture.lifecycle.state());
    }

    private static void rollsBackPublicationFailure() throws Exception {
        Trace trace = new Trace();
        Fixture fixture = fixture(trace);
        fixture.publisher.failPublish = true;
        ObserverLifecycleException failure = assertThrowsResult(
                ObserverLifecycleException.class, fixture.lifecycle::start);
        assertEquals(
                ObserverLifecycleException.Reason.ENDPOINT_PUBLICATION_FAILED,
                failure.reason());
        assertOrder(trace, "publisher.publish", "transport.close", "manager.remove");
        assertEquals(0, fixture.publisher.closeCalls.get());
        assertTrue(fixture.publisher.publishedTokenWiped());
    }

    private static void removesListenerBeforeMailboxClose() throws Exception {
        Trace trace = new Trace();
        Fixture fixture = fixture(trace);
        fixture.lifecycle.start();
        fixture.lifecycle.stop();
        assertOrder(trace, "publication.close", "transport.close", "manager.remove");
        assertEquals(0L, fixture.lifecycle.listener().callbacksInFlight());
    }

    private static void transportFailureStillAttemptsRemainingCleanup()
            throws Exception {
        Trace trace = new Trace();
        Fixture fixture = fixture(trace);
        fixture.lifecycle.start();
        fixture.transport.failClose = true;
        ObserverLifecycleException failure = assertThrowsResult(
                ObserverLifecycleException.class, fixture.lifecycle::stop);
        assertEquals(
                ObserverLifecycleException.Reason.TRANSPORT_SHUTDOWN_UNCLEAN,
                failure.reason());
        assertTrue(trace.values().contains("manager.remove"));
        assertEquals(ObserverBundleLifecycle.State.UNSAFE, fixture.lifecycle.state());
    }

    private static void staleCallbackAfterRemovalCannotReachMailbox()
            throws Exception {
        Trace trace = new Trace();
        Fixture fixture = fixture(trace);
        fixture.lifecycle.start();
        IJobChangeListener stale = fixture.manager.listener;
        fixture.lifecycle.stop();
        stale.scheduled(new TestEvent(new FakeNashJob("stale"), null));
        assertEquals(0L, fixture.lifecycle.listener().callbacksInFlight());
        assertEquals(ListenerRegistrationGate.Phase.CLOSED,
                fixture.lifecycle.listener().phase());
    }

    private static void defaultActivatorCannotRetry() throws Exception {
        HrcJobObserverActivator activator = new HrcJobObserverActivator();
        BundleContext context = new BundleContextStub();
        assertThrows(ObserverLifecycleException.class, () -> activator.start(context));
        ObserverLifecycleException second = assertThrowsResult(
                ObserverLifecycleException.class, () -> activator.start(context));
        assertEquals(
                ObserverLifecycleException.Reason.ACTIVATOR_STATE_INVALID,
                second.reason());
    }

    private static Fixture fixture(Trace trace) throws Exception {
        return fixture(trace, ignored -> new BundleIdentity(BUNDLE, VERSION));
    }

    private static Fixture fixture(
            Trace trace, BundleIdentityResolver resolver) throws Exception {
        FakeJobManager manager = new FakeJobManager(trace);
        FakeTransport transport = new FakeTransport(trace);
        FakePublisher publisher = new FakePublisher(trace);
        byte[] sourceToken = token();
        ObserverLifecycleDependencies dependencies = new ObserverLifecycleDependencies(
                SESSION,
                () -> 1_000,
                () -> UTC,
                () -> new ObservationTime(UTC, 1_000),
                resolver,
                manager,
                publisher,
                (control, token, ignored) -> {
                    assertEquals(SESSION, control.sessionId());
                    transport.factoryToken = token;
                    return transport;
                },
                () -> sourceToken);
        ObserverBundleLifecycle lifecycle = ObserverBundleLifecycle.assemble(
                configuration(), dependencies);
        return new Fixture(lifecycle, manager, transport, publisher, sourceToken);
    }

    private static ObserverLifecycleConfiguration configuration() {
        return new ObserverLifecycleConfiguration(
                profiles(),
                16,
                16,
                64,
                16,
                128,
                TIMEOUT,
                Duration.ofSeconds(1),
                TIMEOUT);
    }

    private static OperationProfileSet profiles() {
        return new OperationProfileSet(List.of(
                new OperationProfile(
                        OperationKind.NASH,
                        BUNDLE,
                        VERSION,
                        FakeNashJob.class.getName()),
                new OperationProfile(
                        OperationKind.VIEWER_SAVE,
                        BUNDLE,
                        VERSION,
                        FakeViewerJob.class.getName()),
                new OperationProfile(
                        OperationKind.EXPORT,
                        BUNDLE,
                        VERSION,
                        FakeExportJob.class.getName())));
    }

    private static byte[] token() {
        byte[] value = new byte[LocalObserverServer.TOKEN_BYTES];
        Arrays.fill(value, (byte) 0x5a);
        return value;
    }

    private static void assertOrder(Trace trace, String... values) {
        int position = -1;
        for (String value : values) {
            int next = trace.values().indexOf(value);
            if (next <= position) {
                throw new AssertionError("bad trace order: " + trace.values());
            }
            position = next;
        }
    }

    private static void awaitUnchecked(CountDownLatch latch) {
        try {
            if (!latch.await(WAIT_MILLIS, TimeUnit.MILLISECONDS)) {
                throw new AssertionError("latch timed out");
            }
        } catch (InterruptedException interrupted) {
            Thread.currentThread().interrupt();
            throw new AssertionError("interrupted while awaiting latch", interrupted);
        }
    }

    private static void awaitPhase(
            ListenerRegistrationGate gate,
            ListenerRegistrationGate.Phase expected) {
        long deadline = System.nanoTime() + TimeUnit.MILLISECONDS.toNanos(WAIT_MILLIS);
        while (gate.phase() != expected) {
            if (System.nanoTime() - deadline >= 0) {
                throw new AssertionError(
                        "listener did not reach phase " + expected);
            }
            Thread.onSpinWait();
        }
    }

    private static TestCase test(String name, ThrowingRunnable body) {
        return new TestCase(name, body);
    }

    private static void assertTrue(boolean value) {
        if (!value) {
            throw new AssertionError("expected true");
        }
    }

    private static void assertFalse(boolean value) {
        if (value) {
            throw new AssertionError("expected false");
        }
    }

    private static void assertEquals(Object expected, Object actual) {
        if (!Objects.equals(expected, actual)) {
            throw new AssertionError("expected " + expected + " but got " + actual);
        }
    }

    private static void assertThrows(
            Class<? extends Throwable> type, ThrowingRunnable body) {
        assertThrowsResult(type, body);
    }

    private static <T extends Throwable> T assertThrowsResult(
            Class<T> type, ThrowingRunnable body) {
        try {
            body.run();
        } catch (Throwable failure) {
            if (type.isInstance(failure)) {
                return type.cast(failure);
            }
            throw new AssertionError(
                    "expected " + type.getName() + " but got " + failure,
                    failure);
        }
        throw new AssertionError("expected " + type.getName());
    }

    private record TestCase(String name, ThrowingRunnable body) {
    }

    @FunctionalInterface
    private interface ThrowingRunnable {
        void run() throws Exception;
    }

    private record Fixture(
            ObserverBundleLifecycle lifecycle,
            FakeJobManager manager,
            FakeTransport transport,
            FakePublisher publisher,
            byte[] sourceToken) {
    }

    private static final class Trace {
        private final List<String> values = new ArrayList<>();

        private synchronized void add(String value) {
            values.add(value);
        }

        private synchronized List<String> values() {
            return List.copyOf(values);
        }
    }

    private static final class FakeJobManager implements JobManagerAccess {
        private final Trace trace;
        private final List<Job[]> snapshots = new ArrayList<>();
        private final AtomicInteger findCalls = new AtomicInteger();
        private final AtomicInteger removeCalls = new AtomicInteger();
        private IJobChangeListener listener;
        private Runnable onFirstFind;

        private FakeJobManager(Trace trace) {
            this.trace = trace;
        }

        @Override
        public void add(IJobChangeListener value) {
            trace.add("manager.add");
            listener = value;
        }

        @Override
        public Job[] findAll() {
            trace.add("manager.find");
            int call = findCalls.getAndIncrement();
            if (call == 0 && onFirstFind != null) {
                onFirstFind.run();
            }
            if (call < snapshots.size()) {
                return snapshots.get(call).clone();
            }
            return new Job[0];
        }

        @Override
        public void remove(IJobChangeListener value) {
            trace.add("manager.remove");
            removeCalls.incrementAndGet();
            assertTrue(listener == value);
            listener = null;
        }

        private void fireScheduled(Job job) {
            IJobChangeListener current = Objects.requireNonNull(listener, "listener");
            current.scheduled(new TestEvent(job, null));
        }

        private void fireDone(Job job) {
            IJobChangeListener current = Objects.requireNonNull(listener, "listener");
            current.done(new TestEvent(job, null));
        }
    }

    private static final class FakeTransport implements ObserverTransportLifecycle {
        private final Trace trace;
        private byte[] factoryToken;
        private boolean failStart;
        private boolean failClose;

        private FakeTransport(Trace trace) {
            this.trace = trace;
        }

        @Override
        public TransportEndpoint start() throws Exception {
            trace.add("transport.start");
            if (failStart) {
                throw new Exception("synthetic transport start failure");
            }
            return new TransportEndpoint(
                    LocalObserverServer.PROTOCOL_VERSION,
                    "127.0.0.1",
                    42_000,
                    SESSION);
        }

        @Override
        public void requireHealthy() {
            trace.add("transport.health");
        }

        @Override
        public void closeAndAwait(Duration ignored) throws Exception {
            trace.add("transport.close");
            if (failClose) {
                throw new Exception("synthetic transport close failure");
            }
        }

        private boolean factoryTokenWiped() {
            return allZero(factoryToken);
        }
    }

    private static final class FakePublisher implements ObserverEndpointPublisher {
        private final Trace trace;
        private final AtomicInteger closeCalls = new AtomicInteger();
        private byte[] receivedToken;
        private boolean failPublish;

        private FakePublisher(Trace trace) {
            this.trace = trace;
        }

        @Override
        public ObserverEndpointPublication publish(
                TransportEndpoint ignored, byte[] bearerToken) throws Exception {
            trace.add("publisher.publish");
            receivedToken = bearerToken;
            if (failPublish) {
                throw new Exception("synthetic publish failure");
            }
            return () -> {
                trace.add("publication.close");
                closeCalls.incrementAndGet();
            };
        }

        private boolean publishedTokenWiped() {
            return allZero(receivedToken);
        }
    }

    private static boolean allZero(byte[] value) {
        if (value == null) {
            return false;
        }
        for (byte element : value) {
            if (element != 0) {
                return false;
            }
        }
        return true;
    }

    private static final class TestEvent implements IJobChangeEvent {
        private final Job job;
        private final IStatus result;

        private TestEvent(Job job, IStatus result) {
            this.job = job;
            this.result = result;
        }

        @Override
        public long getDelay() {
            throw new AssertionError("getDelay must not be called");
        }

        @Override
        public Job getJob() {
            return job;
        }

        @Override
        public IStatus getResult() {
            return result;
        }

        @Override
        public IStatus getJobGroupResult() {
            throw new AssertionError("getJobGroupResult must not be called");
        }
    }

    private abstract static class TestJob extends Job {
        private TestJob(String name) {
            super(name);
        }

        @Override
        protected IStatus run(IProgressMonitor monitor) {
            throw new AssertionError("test Job must never run");
        }
    }

    private static final class FakeNashJob extends TestJob {
        private FakeNashJob(String name) {
            super(name);
        }
    }

    private static final class FakeViewerJob extends TestJob {
        private FakeViewerJob(String name) {
            super(name);
        }
    }

    private static final class FakeExportJob extends TestJob {
        private FakeExportJob(String name) {
            super(name);
        }
    }

    private static final class UnrelatedJob extends TestJob {
        private UnrelatedJob() {
            super("unknown");
        }
    }

    private static final class BundleContextStub implements BundleContext {
        @Override
        public String getProperty(String key) { return null; }
        @Override
        public org.osgi.framework.Bundle getBundle() { return null; }
        @Override
        public org.osgi.framework.Bundle installBundle(
                String location, java.io.InputStream input) { return null; }
        @Override
        public org.osgi.framework.Bundle installBundle(String location) { return null; }
        @Override
        public org.osgi.framework.Bundle getBundle(long id) { return null; }
        @Override
        public org.osgi.framework.Bundle[] getBundles() { return new org.osgi.framework.Bundle[0]; }
        @Override
        public void addServiceListener(
                org.osgi.framework.ServiceListener listener, String filter) { }
        @Override
        public void addServiceListener(org.osgi.framework.ServiceListener listener) { }
        @Override
        public void removeServiceListener(org.osgi.framework.ServiceListener listener) { }
        @Override
        public void addBundleListener(org.osgi.framework.BundleListener listener) { }
        @Override
        public void removeBundleListener(org.osgi.framework.BundleListener listener) { }
        @Override
        public void addFrameworkListener(org.osgi.framework.FrameworkListener listener) { }
        @Override
        public void removeFrameworkListener(org.osgi.framework.FrameworkListener listener) { }
        @Override
        public org.osgi.framework.ServiceRegistration<?> registerService(
                String[] classes, Object service, java.util.Dictionary<String, ?> properties) {
            return null;
        }
        @Override
        public org.osgi.framework.ServiceRegistration<?> registerService(
                String clazz, Object service, java.util.Dictionary<String, ?> properties) {
            return null;
        }
        @Override
        public <S> org.osgi.framework.ServiceRegistration<S> registerService(
                Class<S> clazz, S service, java.util.Dictionary<String, ?> properties) {
            return null;
        }
        @Override
        public <S> org.osgi.framework.ServiceRegistration<S> registerService(
                Class<S> clazz,
                org.osgi.framework.ServiceFactory<S> factory,
                java.util.Dictionary<String, ?> properties) {
            return null;
        }
        @Override
        public org.osgi.framework.ServiceReference<?>[] getServiceReferences(
                String clazz, String filter) { return null; }
        @Override
        public org.osgi.framework.ServiceReference<?>[] getAllServiceReferences(
                String clazz, String filter) { return null; }
        @Override
        public org.osgi.framework.ServiceReference<?> getServiceReference(String clazz) {
            return null;
        }
        @Override
        public <S> org.osgi.framework.ServiceReference<S> getServiceReference(Class<S> clazz) {
            return null;
        }
        @Override
        public <S> java.util.Collection<org.osgi.framework.ServiceReference<S>>
                getServiceReferences(Class<S> clazz, String filter) { return List.of(); }
        @Override
        public <S> S getService(org.osgi.framework.ServiceReference<S> reference) {
            return null;
        }
        @Override
        public boolean ungetService(org.osgi.framework.ServiceReference<?> reference) {
            return false;
        }
        @Override
        public java.io.File getDataFile(String filename) { return null; }
        @Override
        public org.osgi.framework.Filter createFilter(String filter) { return null; }
        @Override
        public org.osgi.framework.Bundle getBundle(String location) { return null; }
        @Override
        public <S> org.osgi.framework.ServiceObjects<S> getServiceObjects(
                org.osgi.framework.ServiceReference<S> reference) { return null; }
    }
}
