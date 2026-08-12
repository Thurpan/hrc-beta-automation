package net.hrcautomation.jobobserver;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.Base64;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;

public final class LocalTransportTest {
    private static final UUID SESSION = new UUID(0, 99);
    private static final byte[] TOKEN = token();
    private static final Duration TIMEOUT = Duration.ofSeconds(3);

    private LocalTransportTest() {
    }

    public static void main(String[] args) {
        List<TestCase> tests = List.of(
                test("validatesEndpointAndCheckpoint", LocalTransportTest::validatesEndpointAndCheckpoint),
                test("authenticatesAndPings", LocalTransportTest::authenticatesAndPings),
                test("rejectsWrongAuthentication", LocalTransportTest::rejectsWrongAuthentication),
                test("acceptsLfAndCrLfOnly", LocalTransportTest::acceptsLfAndCrLfOnly),
                test("rejectsOversizedAndMalformedFrames", LocalTransportTest::rejectsOversizedAndMalformedFrames),
                test("rejectsIncoherentCheckpoints", LocalTransportTest::rejectsIncoherentCheckpoints),
                test("rejectsControlFailuresAndMismatches", LocalTransportTest::rejectsControlFailuresAndMismatches),
                test("returnsHealthyCheckpoint", LocalTransportTest::returnsHealthyCheckpoint),
                test("returnsNonActionableCheckpointStates", LocalTransportTest::returnsNonActionableCheckpointStates),
                test("rejectsSessionMismatch", LocalTransportTest::rejectsSessionMismatch),
                test("armsAllOperationsAndPreservesIdempotency", LocalTransportTest::armsAllOperationsAndPreservesIdempotency),
                test("rejectsInvalidArmInputs", LocalTransportTest::rejectsInvalidArmInputs),
                test("reconnectsWithSameSessionAndReplayCursor", LocalTransportTest::reconnectsWithSameSessionAndReplayCursor),
                test("serialisesEveryEventWithoutSensitiveFields", LocalTransportTest::serialisesEveryEventWithoutSensitiveFields),
                test("escapesJsonFields", LocalTransportTest::escapesJsonFields),
                test("boundsCheckpointResponse", LocalTransportTest::boundsCheckpointResponse),
                test("rejectsOversizedCheckpointEncoding", LocalTransportTest::rejectsOversizedCheckpointEncoding),
                test("handlesClientDisconnect", LocalTransportTest::handlesClientDisconnect),
                test("closeBeforeStartIsCleanAndFinal", LocalTransportTest::closeBeforeStartIsCleanAndFinal),
                test("cleanCloseStopsAccept", LocalTransportTest::cleanCloseStopsAccept),
                test("closeWaitsForAdmittedArm", LocalTransportTest::closeWaitsForAdmittedArm),
                test("serverAllowsOnlyOneConcurrentClient", LocalTransportTest::serverAllowsOnlyOneConcurrentClient),
                test("tokenIsDefensivelyCopied", LocalTransportTest::tokenIsDefensivelyCopied));
        int passed = 0;
        for (TestCase test : tests) {
            try {
                test.body().run();
                passed++;
                System.out.println("PASS " + test.name());
            } catch (Throwable failure) {
                System.err.println("FAIL " + test.name() + ": " + failure);
                failure.printStackTrace(System.err);
                System.exit(1);
            }
        }
        System.out.println("PASS " + passed + "/" + tests.size());
    }

    private static void validatesEndpointAndCheckpoint() {
        assertThrows(IllegalArgumentException.class,
                () -> new TransportEndpoint(2, "127.0.0.1", 1, SESSION));
        assertThrows(IllegalArgumentException.class,
                () -> new TransportEndpoint(1, "0.0.0.0", 1, SESSION));
        assertThrows(IllegalArgumentException.class,
                () -> new LocalObserverServer(
                        new FakeControl(), new byte[LocalObserverServer.TOKEN_BYTES], TIMEOUT));
        assertThrows(IllegalArgumentException.class,
                () -> new ObserverCheckpoint(
                        SESSION, 0, 0, emptyReplay(), null,
                        CallbackHealth.HEALTHY, null));
        assertThrows(IllegalArgumentException.class,
                () -> new ObserverCheckpoint(
                        SESSION, 1, 0, emptyReplay(), null, CallbackHealth.HEALTHY,
                        InfrastructureFailure.CALLBACK_CAPTURE_FAILED));
        assertThrows(IllegalArgumentException.class,
                () -> new ObserverCheckpoint(
                        SESSION, 1, 0, emptyReplay(), null,
                        CallbackHealth.FAULTED, null));
        assertThrows(IllegalArgumentException.class,
                () -> new ObserverCheckpoint(
                        SESSION, 1, 0,
                        new ReplayQuery(ReplayQuery.Disposition.OK,
                                List.of(scheduledEvent(2)), 1, 2),
                        null, CallbackHealth.HEALTHY, null));
        assertThrows(IllegalArgumentException.class,
                () -> new ObserverCheckpoint(
                        SESSION, 1, 0,
                        new ReplayQuery(ReplayQuery.Disposition.OK,
                                List.of(new ObserverFaultEvent(
                                        metadata(1), FaultReason.JOB_MISMATCH)), 1, 1),
                        null, CallbackHealth.HEALTHY, null));
        JobDescriptor rejectedJob = job();
        assertThrows(IllegalArgumentException.class,
                () -> new ObserverCheckpoint(
                        SESSION, 1, 0,
                        new ReplayQuery(ReplayQuery.Disposition.OK,
                                List.of(new JobRunningRejectedEvent(
                                        metadata(1), new UUID(0, 1),
                                        OperationKind.NASH, 1, rejectedJob,
                                        FaultReason.TERMINAL_EVENT_REJECTED)), 1, 1),
                        null, CallbackHealth.HEALTHY, null));
    }

    private static void authenticatesAndPings() throws Exception {
        FakeControl control = new FakeControl();
        try (ServerHarness server = server(control)) {
            try (Client client = server.connect()) {
                assertEquals("READY\t1\t" + SESSION, client.authenticate(TOKEN));
                assertEquals("PONG\t" + SESSION, client.exchange("PING\t" + SESSION));
                assertEquals("BYE", client.exchange("BYE"));
            }
            assertTrue(server.shutdown().clean());
        }
    }

    private static void rejectsWrongAuthentication() throws Exception {
        FakeControl control = new FakeControl();
        try (ServerHarness server = server(control);
                Client client = server.connect()) {
            byte[] wrong = token();
            wrong[0] ^= 1;
            client.write("HELLO\t1\t" + Base64.getUrlEncoder().withoutPadding()
                    .encodeToString(wrong));
            assertEquals(null, client.read());
            awaitFailure(server.server, TransportFailure.AUTHENTICATION_FAILED);
        }
    }

    private static void acceptsLfAndCrLfOnly() throws Exception {
        try (ServerHarness server = server(new FakeControl());
                Client client = server.connect()) {
            assertReady(client);
            assertEquals("PONG\t" + SESSION,
                    client.exchangeCrLf("PING\t" + SESSION));
        }
        try (ServerHarness server = server(new FakeControl());
                Client client = server.connect()) {
            assertReady(client);
            client.writeRaw("PING\t" + SESSION + "\rX\n");
            assertEquals(null, client.read());
            awaitFailure(server.server, TransportFailure.PROTOCOL_VIOLATION);
        }
    }

    private static void rejectsOversizedAndMalformedFrames() throws Exception {
        try (ServerHarness server = server(new FakeControl());
                Client client = server.connect()) {
            assertReady(client);
            client.write("X".repeat(LocalObserverServer.MAX_FRAME_BYTES + 1));
            assertEquals(null, client.read());
            awaitFailure(server.server, TransportFailure.FRAME_TOO_LARGE);
        }
    }

    private static void rejectsIncoherentCheckpoints() throws Exception {
        FakeControl control = new FakeControl();
        control.checkpoint = new ObserverCheckpoint(
                SESSION, 1, 7, emptyReplayAfter(7), null,
                CallbackHealth.HEALTHY, null);
        try (ServerHarness server = server(control);
                Client client = server.connect()) {
            assertReady(client);
            client.write("CHECKPOINT\t" + SESSION + "\t0");
            assertEquals(null, client.read());
            awaitFailure(server.server, TransportFailure.CHECKPOINT_MISMATCH);
        }
    }

    private static void rejectsControlFailuresAndMismatches() throws Exception {
        FakeControl failing = new FakeControl();
        failing.throwOnCheckpoint = true;
        try (ServerHarness server = server(failing);
                Client client = server.connect()) {
            assertReady(client);
            client.write("CHECKPOINT\t" + SESSION + "\t0");
            assertEquals(null, client.read());
            awaitFailure(server.server, TransportFailure.CONTROL_FAILURE);
        }

        FakeControl mismatch = new FakeControl();
        mismatch.checkpoint = new ObserverCheckpoint(
                new UUID(0, 100), 1, 0,
                new ReplayQuery(ReplayQuery.Disposition.OK, List.of(), 1, 0),
                null, CallbackHealth.HEALTHY, null);
        try (ServerHarness server = server(mismatch);
                Client client = server.connect()) {
            assertReady(client);
            client.write("CHECKPOINT\t" + SESSION + "\t0");
            assertEquals(null, client.read());
            awaitFailure(server.server, TransportFailure.SESSION_MISMATCH);
        }
    }

    private static void returnsHealthyCheckpoint() throws Exception {
        FakeControl control = new FakeControl();
        control.checkpoint = healthyCheckpoint(List.of(scheduledEvent(1)));
        try (ServerHarness server = server(control);
                Client client = server.connect()) {
            assertReady(client);
            String response = client.exchange("CHECKPOINT\t" + SESSION + "\t0");
            assertContains(response, "CHECKPOINT\t{\"v\":1");
            assertContains(response, "\"actionable\":true");
            assertContains(response, "\"type\":\"JOB_SCHEDULED\"");
            assertEquals(0L, control.lastCursor);
        }
    }

    private static void returnsNonActionableCheckpointStates() throws Exception {
        for (ObserverCheckpoint checkpoint : List.of(
                new ObserverCheckpoint(SESSION, 1, 0,
                        new ReplayQuery(ReplayQuery.Disposition.GAP, List.of(), 2, 2),
                        null, CallbackHealth.HEALTHY, null),
                new ObserverCheckpoint(SESSION, 2, 0, emptyReplay(),
                        FaultReason.JOB_MISMATCH, CallbackHealth.HEALTHY, null),
                new ObserverCheckpoint(SESSION, 3, 0, emptyReplay(), null,
                        CallbackHealth.FAULTED,
                        InfrastructureFailure.CALLBACK_QUEUE_OVERFLOW))) {
            FakeControl control = new FakeControl();
            control.checkpoint = checkpoint;
            try (ServerHarness server = server(control);
                    Client client = server.connect()) {
                assertReady(client);
                assertContains(client.exchange("CHECKPOINT\t" + SESSION + "\t0"),
                        "\"actionable\":false");
            }
        }
    }

    private static void rejectsSessionMismatch() throws Exception {
        try (ServerHarness server = server(new FakeControl());
                Client client = server.connect()) {
            assertReady(client);
            client.write("PING\t" + new UUID(0, 100));
            assertEquals(null, client.read());
            awaitFailure(server.server, TransportFailure.SESSION_MISMATCH);
        }
    }

    private static void armsAllOperationsAndPreservesIdempotency() throws Exception {
        FakeControl control = new FakeControl();
        try (ServerHarness server = server(control);
                Client client = server.connect()) {
            assertReady(client);
            record Arm(OperationKind operation, String name) {
            }
            for (Arm arm : List.of(
                    new Arm(OperationKind.NASH, "HU-2: Monte Carlo Sampling"),
                    new Arm(OperationKind.VIEWER_SAVE, "Saving hand to: stage-a.hrcv"),
                    new Arm(OperationKind.EXPORT, "Exporting ranges to stage-a.zip"))) {
                UUID request = UUID.randomUUID();
                String frame = armFrame(request, arm.operation(), arm.name(), 1_000);
                assertEquals("ARM\t" + request + "\tACCEPTED", client.exchange(frame));
                assertEquals("ARM\t" + request + "\tIDEMPOTENT", client.exchange(frame));
            }
            assertEquals(6, control.armCalls.get());
        }
    }

    private static void rejectsInvalidArmInputs() throws Exception {
        try (ServerHarness server = server(new FakeControl());
                Client client = server.connect()) {
            assertReady(client);
            client.write("ARM\t" + SESSION + "\tbad\tNASH\tQQ\t1000");
            assertEquals(null, client.read());
            awaitFailure(server.server, TransportFailure.PROTOCOL_VIOLATION);
        }
    }

    private static void reconnectsWithSameSessionAndReplayCursor() throws Exception {
        FakeControl control = new FakeControl();
        try (ServerHarness server = server(control)) {
            try (Client first = server.connect()) {
                assertReady(first);
            }
            try (Client second = server.connect()) {
                assertReady(second);
                second.exchange("CHECKPOINT\t" + SESSION + "\t7");
                assertEquals(7L, control.lastCursor);
            }
        }
    }

    private static void serialisesEveryEventWithoutSensitiveFields() throws Exception {
        List<ObserverEvent> events = allEvents();
        for (ObserverEvent event : events) {
            String json = ObserverEventJson.encode(event);
            assertContains(json, "\"sessionId\":\"" + SESSION + "\"");
            assertFalse(json.contains("message"));
            assertFalse(json.contains("exception"));
            assertFalse(json.contains("strategy"));
            assertFalse(json.contains("licence"));
            assertFalse(json.contains("license"));
        }
    }

    private static void escapesJsonFields() {
        JobDescriptor job = new JobDescriptor("bundle", "1", "class", "A\"B\\C", true, false);
        String json = ObserverEventJson.encode(new JobScheduledEvent(
                metadata(1), UUID.randomUUID(), OperationKind.NASH, 1, job));
        assertContains(json, "A\\\"B\\\\C");
        assertFalse(json.contains("A\"B\\C"));
    }

    private static void boundsCheckpointResponse() {
        List<ObserverEvent> events = new ArrayList<>();
        for (int index = 1; index <= ObserverCheckpoint.MAX_REPLAY_EVENTS + 1; index++) {
            events.add(scheduledEvent(index));
        }
        assertThrows(IllegalArgumentException.class, () -> new ObserverCheckpoint(
                SESSION, 1, 0,
                new ReplayQuery(ReplayQuery.Disposition.OK, events, 1, events.size()),
                null, CallbackHealth.HEALTHY, null));
    }

    private static void rejectsOversizedCheckpointEncoding() {
        List<ObserverEvent> events = new ArrayList<>();
        JobDescriptor largeJob = new JobDescriptor(
                "net.holdemresources.calculator",
                "4.1.1.202607211244",
                "net.holdemresources.internal.bQ",
                "\u20ac".repeat(300),
                true,
                false);
        for (int index = 1; index <= ObserverCheckpoint.MAX_REPLAY_EVENTS; index++) {
            events.add(new JobScheduledEvent(
                    metadata(index), new UUID(0, index),
                    OperationKind.NASH, index, largeJob));
        }
        ObserverCheckpoint checkpoint = new ObserverCheckpoint(
                SESSION, 1, 0,
                new ReplayQuery(ReplayQuery.Disposition.OK, events, 1, events.size()),
                null, CallbackHealth.HEALTHY, null);
        assertThrows(ProtocolFailure.class, () -> CheckpointJson.encode(checkpoint));
    }

    private static void handlesClientDisconnect() throws Exception {
        try (ServerHarness server = server(new FakeControl())) {
            try (Client client = server.connect()) {
                assertReady(client);
            }
            assertEquals(null, server.server.firstFailure());
            try (Client client = server.connect()) {
                assertReady(client);
            }
        }
    }

    private static void closeBeforeStartIsCleanAndFinal() {
        LocalObserverServer server = new LocalObserverServer(
                new FakeControl(), TOKEN, TIMEOUT);
        assertTrue(server.closeAndAwait(TIMEOUT).clean());
        assertTrue(server.closeAndAwait(TIMEOUT).clean());
        assertThrows(IllegalStateException.class, server::start);
    }

    private static void cleanCloseStopsAccept() throws Exception {
        ServerHarness server = server(new FakeControl());
        TransportEndpoint endpoint = server.endpoint;
        assertTrue(server.shutdown().clean());
        try (Socket socket = new Socket()) {
            assertThrows(IOException.class, () -> socket.connect(
                    new InetSocketAddress(endpoint.address(), endpoint.port()), 200));
        }
    }

    private static void closeWaitsForAdmittedArm() throws Exception {
        BlockingControl control = new BlockingControl();
        ServerHarness server = server(control);
        Client client = server.connect();
        assertReady(client);
        UUID request = UUID.randomUUID();
        client.write(armFrame(
                request, OperationKind.NASH, "HU-2: Monte Carlo Sampling", 1_000));
        assertTrue(control.entered.await(1, TimeUnit.SECONDS));

        AtomicReference<TransportCloseResult> closed = new AtomicReference<>();
        Thread closeThread = new Thread(() -> closed.set(server.shutdown()));
        closeThread.start();
        assertFalse(joined(closeThread, Duration.ofMillis(100)));
        control.release.countDown();
        assertTrue(joined(closeThread, TIMEOUT));
        assertTrue(closed.get().clean());
        client.close();
    }

    private static void serverAllowsOnlyOneConcurrentClient() throws Exception {
        try (ServerHarness server = server(new FakeControl());
                Client first = server.connect();
                Client second = server.connect()) {
            assertReady(first);
            second.write(hello(TOKEN));
            second.socket.setSoTimeout(100);
            assertThrows(IOException.class, second::read);
            assertEquals("PONG\t" + SESSION, first.exchange("PING\t" + SESSION));
        }
    }

    private static void tokenIsDefensivelyCopied() throws Exception {
        byte[] supplied = token();
        FakeControl control = new FakeControl();
        LocalObserverServer server = new LocalObserverServer(control, supplied, TIMEOUT);
        supplied[0] ^= 1;
        try (ServerHarness harness = new ServerHarness(server, server.start());
                Client client = harness.connect()) {
            assertEquals("READY\t1\t" + SESSION, client.authenticate(TOKEN));
        }
    }

    private static ServerHarness server(FakeControl control) throws IOException {
        LocalObserverServer server = new LocalObserverServer(control, TOKEN, TIMEOUT);
        return new ServerHarness(server, server.start());
    }

    private static void assertReady(Client client) throws IOException {
        assertEquals("READY\t1\t" + SESSION, client.authenticate(TOKEN));
    }

    private static String armFrame(
            UUID request, OperationKind operation, String name, long timeoutMillis) {
        String encoded = Base64.getUrlEncoder().withoutPadding()
                .encodeToString(name.getBytes(StandardCharsets.UTF_8));
        return "ARM\t" + SESSION + "\t" + request + "\t"
                + operation + "\t" + encoded + "\t" + timeoutMillis;
    }

    private static String hello(byte[] token) {
        return "HELLO\t1\t" + Base64.getUrlEncoder().withoutPadding()
                .encodeToString(token);
    }

    private static byte[] token() {
        byte[] token = new byte[LocalObserverServer.TOKEN_BYTES];
        for (int index = 0; index < token.length; index++) {
            token[index] = (byte) (index + 1);
        }
        return token;
    }

    private static ObserverCheckpoint healthyCheckpoint(List<ObserverEvent> events) {
        return healthyCheckpoint(0, events);
    }

    private static ObserverCheckpoint healthyCheckpoint(
            long afterSequence, List<ObserverEvent> events) {
        long latest = events.isEmpty() ? 0 : events.get(events.size() - 1).sequence();
        if (events.isEmpty()) {
            latest = afterSequence;
        }
        return new ObserverCheckpoint(
                SESSION,
                1,
                afterSequence,
                new ReplayQuery(ReplayQuery.Disposition.OK, events, 1, latest),
                null,
                CallbackHealth.HEALTHY,
                null);
    }

    private static ReplayQuery emptyReplay() {
        return new ReplayQuery(ReplayQuery.Disposition.OK, List.of(), 1, 0);
    }

    private static ReplayQuery emptyReplayAfter(long cursor) {
        return new ReplayQuery(
                ReplayQuery.Disposition.OK, List.of(), cursor + 1, cursor);
    }

    private static EventMetadata metadata(long sequence) {
        return new EventMetadata(
                sequence, Instant.parse("2026-08-12T16:00:00Z"), sequence, SESSION);
    }

    private static JobDescriptor job() {
        return new JobDescriptor(
                "net.holdemresources.calculator",
                "4.1.1.202607211244",
                "net.holdemresources.internal.bQ",
                "HU-2: Monte Carlo Sampling",
                true,
                false);
    }

    private static JobScheduledEvent scheduledEvent(long sequence) {
        return new JobScheduledEvent(
                metadata(sequence), new UUID(0, sequence), OperationKind.NASH, sequence, job());
    }

    private static List<ObserverEvent> allEvents() {
        UUID request = new UUID(0, 1);
        JobDescriptor job = job();
        return List.of(
                new ArmAcceptedEvent(metadata(1), request, OperationKind.NASH,
                        "HU-2: Monte Carlo Sampling", 100),
                new JobScheduledEvent(metadata(2), request, OperationKind.NASH, 1, job),
                new JobRunningEvent(metadata(3), request, OperationKind.NASH, 1, job),
                new JobRunningRejectedEvent(metadata(4), request, OperationKind.NASH, 1,
                        job, FaultReason.TERMINAL_EVENT_REJECTED),
                new JobTerminalEvent(metadata(5), request, OperationKind.NASH, 1, job,
                        TerminalResult.OK, 0, true, 0, "org.eclipse.core.runtime",
                        false, true),
                new JobTerminalRejectedEvent(metadata(6), request, OperationKind.NASH, 1,
                        job, TerminalResult.ERROR, 4, false, 1,
                        "org.eclipse.core.runtime", false, true,
                        FaultReason.TERMINAL_EVENT_REJECTED),
                new ObserverFaultEvent(metadata(7), FaultReason.JOB_MISMATCH));
    }

    private static void awaitFailure(
            LocalObserverServer server, TransportFailure expected) throws Exception {
        long deadline = System.nanoTime() + TIMEOUT.toNanos();
        while (server.firstFailure() == null && System.nanoTime() - deadline < 0) {
            Thread.onSpinWait();
        }
        assertEquals(expected, server.firstFailure());
    }

    private static void assertContains(String value, String expected) {
        if (!value.contains(expected)) {
            throw new AssertionError("missing " + expected + " in " + value);
        }
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

    private static boolean joined(Thread thread, Duration timeout)
            throws InterruptedException {
        thread.join(timeout.toMillis());
        return !thread.isAlive();
    }

    private static void assertEquals(Object expected, Object actual) {
        if (!Objects.equals(expected, actual)) {
            throw new AssertionError("expected " + expected + " but got " + actual);
        }
    }

    private static void assertThrows(Class<? extends Throwable> type, ThrowingRunnable body) {
        try {
            body.run();
        } catch (Throwable failure) {
            if (type.isInstance(failure)) {
                return;
            }
            throw new AssertionError("expected " + type.getName() + " but got " + failure,
                    failure);
        }
        throw new AssertionError("expected " + type.getName());
    }

    private static TestCase test(String name, ThrowingRunnable body) {
        return new TestCase(name, body);
    }

    @FunctionalInterface
    private interface ThrowingRunnable {
        void run() throws Exception;
    }

    private record TestCase(String name, ThrowingRunnable body) {
    }

    private static class FakeControl implements ObserverTransportControl {
        private final AtomicInteger armCalls = new AtomicInteger();
        private final Map<UUID, String> arms = new HashMap<>();
        private volatile long lastCursor = -1;
        private volatile ObserverCheckpoint checkpoint;
        private volatile boolean throwOnCheckpoint;

        @Override
        public UUID sessionId() {
            return SESSION;
        }

        @Override
        public ObserverCheckpoint checkpoint(long lastSeenSequence) {
            lastCursor = lastSeenSequence;
            if (throwOnCheckpoint) {
                throw new IllegalStateException("synthetic control failure");
            }
            return checkpoint == null
                    ? healthyCheckpoint(lastSeenSequence, List.of()) : checkpoint;
        }

        @Override
        public ArmOutcome armIfHealthy(
                UUID requestId,
                OperationKind operation,
                String expectedJobName,
                long timeoutNanos) {
            armCalls.incrementAndGet();
            if (!operation.acceptsExpectedName(expectedJobName)) {
                return ArmOutcome.REJECTED;
            }
            String intent = operation + "\t" + expectedJobName;
            String prior = arms.putIfAbsent(requestId, intent);
            if (prior == null) {
                return ArmOutcome.ACCEPTED;
            }
            return prior.equals(intent) ? ArmOutcome.IDEMPOTENT : ArmOutcome.FAULTED;
        }
    }

    private static final class BlockingControl extends FakeControl {
        private final CountDownLatch entered = new CountDownLatch(1);
        private final CountDownLatch release = new CountDownLatch(1);

        @Override
        public ArmOutcome armIfHealthy(
                UUID requestId,
                OperationKind operation,
                String expectedJobName,
                long timeoutNanos) {
            entered.countDown();
            try {
                if (!release.await(1, TimeUnit.SECONDS)) {
                    throw new IllegalStateException("synthetic arm timeout");
                }
            } catch (InterruptedException interrupted) {
                Thread.currentThread().interrupt();
                throw new IllegalStateException("synthetic arm interrupted", interrupted);
            }
            return super.armIfHealthy(
                    requestId, operation, expectedJobName, timeoutNanos);
        }
    }

    private static final class ServerHarness implements AutoCloseable {
        private final LocalObserverServer server;
        private final TransportEndpoint endpoint;

        private ServerHarness(LocalObserverServer server, TransportEndpoint endpoint) {
            this.server = server;
            this.endpoint = endpoint;
        }

        private Client connect() throws IOException {
            return new Client(endpoint);
        }

        private TransportCloseResult shutdown() {
            return server.closeAndAwait(TIMEOUT);
        }

        @Override
        public void close() {
            shutdown();
        }
    }

    private static final class Client implements AutoCloseable {
        private final Socket socket = new Socket();
        private final BufferedReader input;
        private final OutputStream output;

        private Client(TransportEndpoint endpoint) throws IOException {
            socket.connect(new InetSocketAddress(endpoint.address(), endpoint.port()), 500);
            socket.setSoTimeout(1_000);
            input = new BufferedReader(new InputStreamReader(
                    socket.getInputStream(), StandardCharsets.UTF_8));
            output = socket.getOutputStream();
        }

        private String authenticate(byte[] token) throws IOException {
            return exchange(hello(token));
        }

        private String exchange(String frame) throws IOException {
            write(frame);
            return read();
        }

        private String exchangeCrLf(String frame) throws IOException {
            writeRaw(frame + "\r\n");
            return read();
        }

        private void write(String frame) throws IOException {
            writeRaw(frame + "\n");
        }

        private void writeRaw(String frame) throws IOException {
            output.write(frame.getBytes(StandardCharsets.US_ASCII));
            output.flush();
        }

        private String read() throws IOException {
            return input.readLine();
        }

        @Override
        public void close() throws IOException {
            socket.close();
        }
    }
}
