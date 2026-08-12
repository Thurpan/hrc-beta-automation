package net.hrcautomation.jobobserver;

import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.time.Instant;
import java.util.Base64;
import java.util.List;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicLong;
import java.util.concurrent.atomic.AtomicReference;

public final class ObserverRuntimeAssemblyTest {
    private static final UUID SESSION = new UUID(0, 700);
    private static final Instant UTC = Instant.parse("2026-08-12T17:00:00Z");
    private static final Duration CONTROL_TIMEOUT = Duration.ofSeconds(2);
    private static final String BUNDLE = "net.holdemresources.calculator";
    private static final String VERSION = "4.1.1.202607211244";
    private static final String NASH_CLASS = "net.holdemresources.internal.bQ";

    private ObserverRuntimeAssemblyTest() {
    }

    public static void main(String[] args) {
        List<TestCase> tests = List.of(
                test("rejectsInvalidConstruction",
                        ObserverRuntimeAssemblyTest::rejectsInvalidConstruction),
                test("buildsActionableCheckpointAfterOrderedCallbacks",
                        ObserverRuntimeAssemblyTest::buildsActionableCheckpointAfterOrderedCallbacks),
                test("armsAllOperationsThroughOrderedWorker",
                        ObserverRuntimeAssemblyTest::armsAllOperationsThroughOrderedWorker),
                test("rejectsArmThatExpiresBeforeConfirmation",
                        ObserverRuntimeAssemblyTest::rejectsArmThatExpiresBeforeConfirmation),
                test("rejectsArmConsumedBeforeConfirmation",
                        ObserverRuntimeAssemblyTest::rejectsArmConsumedBeforeConfirmation),
                test("startsArmLeaseAtConfirmation",
                        ObserverRuntimeAssemblyTest::startsArmLeaseAtConfirmation),
                test("renewsLeaseForIdempotentRetry",
                        ObserverRuntimeAssemblyTest::renewsLeaseForIdempotentRetry),
                test("servesOrderedControlOverLoopback",
                        ObserverRuntimeAssemblyTest::servesOrderedControlOverLoopback),
                test("projectsObserverFaultAsNonActionable",
                        ObserverRuntimeAssemblyTest::projectsObserverFaultAsNonActionable),
                test("rejectsInvalidCursorWithoutCoreMutation",
                        ObserverRuntimeAssemblyTest::rejectsInvalidCursorWithoutCoreMutation));

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

    private static void rejectsInvalidConstruction() {
        Fixture fixture = fixture(8, 100);
        try {
            assertThrows(IllegalArgumentException.class, () ->
                    new OrderedObserverTransportControl(
                            fixture.coordinator(),
                            fixture.mailbox(),
                            Duration.ZERO));
        } finally {
            assertTrue(fixture.close().clean());
        }

        ObserverCoordinator oversized = coordinator(257, new AtomicLong(100));
        EclipseCallbackMailbox mailbox = new EclipseCallbackMailbox(8, oversized);
        mailbox.start();
        try {
            assertThrows(IllegalArgumentException.class, () ->
                    new OrderedObserverTransportControl(
                            oversized, mailbox, CONTROL_TIMEOUT));
        } finally {
            assertTrue(mailbox.closeAndAwait(CONTROL_TIMEOUT).clean());
        }

        ObserverCoordinator first = coordinator(64, new AtomicLong(100));
        ObserverCoordinator second = coordinator(64, new AtomicLong(100));
        EclipseCallbackMailbox mismatched = new EclipseCallbackMailbox(8, second);
        mismatched.start();
        try {
            assertThrows(IllegalArgumentException.class, () ->
                    new OrderedObserverTransportControl(
                            first, mismatched, CONTROL_TIMEOUT));
        } finally {
            assertTrue(mismatched.closeAndAwait(CONTROL_TIMEOUT).clean());
        }
    }

    private static void buildsActionableCheckpointAfterOrderedCallbacks()
            throws Exception {
        AtomicLong clock = new AtomicLong(100);
        ObserverCoordinator coordinator = coordinator(64, clock);
        EclipseCallbackMailbox mailbox = new EclipseCallbackMailbox(8, coordinator);
        mailbox.start();
        Fixture fixture = new Fixture(
                coordinator,
                mailbox,
                new OrderedObserverTransportControl(
                        coordinator, mailbox, CONTROL_TIMEOUT),
                clock);
        Object identity = new Object();
        String name = "ORDERED: Monte Carlo Sampling";
        try {
            assertEquals(ArmOutcome.ACCEPTED, fixture.control().armIfHealthy(
                    new UUID(0, 1), OperationKind.NASH, name, 1_000));
            CallbackEntry entry = fixture.mailbox().beginCallback();
            assertTrue(entry != null);
            assertTrue(fixture.mailbox().admitCallback(
                    entry, new ObservationTime(UTC, 101)));
            AtomicReference<ObserverCheckpoint> checkpoint = new AtomicReference<>();
            AtomicReference<Throwable> failure = new AtomicReference<>();
            Thread waiter = new Thread(() -> {
                try {
                    checkpoint.set(fixture.control().checkpoint(0));
                } catch (Throwable thrown) {
                    failure.set(thrown);
                }
            }, "runtime-checkpoint-test");
            waiter.start();
            assertFalse(joined(waiter, 100));
            fixture.mailbox().completeCallback(entry, new ProfiledLifecycle(
                    LifecycleInput.scheduled(
                            identity, descriptor(name), UTC, 101)));
            waiter.join(2_000);
            assertFalse(waiter.isAlive());
            if (failure.get() != null) {
                throw new AssertionError("checkpoint failed", failure.get());
            }

            ObserverCheckpoint result = checkpoint.get();
            assertTrue(result.actionable());
            assertEquals(SESSION, result.sessionId());
            assertTrue(result.barrierId() > 0);
            assertEquals(0L, result.afterSequence());
            assertEquals(3, result.replay().events().size());
            assertTrue(result.replay().events().get(0) instanceof ArmAcceptedEvent);
            assertTrue(result.replay().events().get(1) instanceof ArmConfirmedEvent);
            assertTrue(result.replay().events().get(2) instanceof JobScheduledEvent);
        } finally {
            assertTrue(fixture.close().clean());
        }
    }

    private static void armsAllOperationsThroughOrderedWorker() {
        Fixture nash = fixture(8, 100);
        try {
            assertEquals(ArmOutcome.ACCEPTED, nash.control().armIfHealthy(
                    new UUID(0, 10),
                    OperationKind.NASH,
                    "AUTO-HU-2: Monte Carlo Sampling",
                    1_000));
            assertEquals(ArmOutcome.IDEMPOTENT, nash.control().armIfHealthy(
                    new UUID(0, 10),
                    OperationKind.NASH,
                    "AUTO-HU-2: Monte Carlo Sampling",
                    1_000));
            assertEquals(ArmOutcome.FAULTED, nash.control().armIfHealthy(
                    new UUID(0, 10),
                    OperationKind.NASH,
                    "AUTO-HU-2: Monte Carlo Sampling",
                    1_001));
        } finally {
            assertTrue(nash.close().clean());
        }

        Fixture viewer = fixture(8, 100);
        try {
            assertEquals(ArmOutcome.ACCEPTED, viewer.control().armIfHealthy(
                    new UUID(0, 11),
                    OperationKind.VIEWER_SAVE,
                    "Saving hand to: stage-1.hrcv",
                    1_000));
        } finally {
            assertTrue(viewer.close().clean());
        }

        Fixture export = fixture(8, 100);
        try {
            assertEquals(ArmOutcome.ACCEPTED, export.control().armIfHealthy(
                    new UUID(0, 12),
                    OperationKind.EXPORT,
                    "Exporting ranges to stage-1.zip",
                    1_000));
        } finally {
            assertTrue(export.close().clean());
        }
    }

    private static void rejectsArmThatExpiresBeforeConfirmation() {
        AtomicLong clock = new AtomicLong(100);
        ObserverCoordinator coordinator = new ObserverCoordinator(
                SESSION,
                List.of(new OperationProfile(
                        OperationKind.NASH, BUNDLE, VERSION, NASH_CLASS)),
                16,
                16,
                64,
                () -> clock.getAndSet(111),
                () -> UTC);
        EclipseCallbackMailbox mailbox = new EclipseCallbackMailbox(8, coordinator);
        mailbox.start();
        OrderedObserverTransportControl control =
                new OrderedObserverTransportControl(
                        coordinator, mailbox, CONTROL_TIMEOUT);
        try {
            assertEquals(ArmOutcome.FAULTED, control.armIfHealthy(
                    new UUID(0, 13),
                    OperationKind.NASH,
                    "EXPIRED: Monte Carlo Sampling",
                    10));
            assertEquals(FaultReason.ARM_DEADLINE_EXPIRED,
                    coordinator.faultReason());
            ReplayQuery replay = coordinator.replayAfter(0);
            assertEquals(2, replay.events().size());
            assertTrue(replay.events().get(0) instanceof ArmAcceptedEvent);
            assertEquals(FaultReason.ARM_DEADLINE_EXPIRED,
                    ((ObserverFaultEvent) replay.events().get(1)).reason());
        } finally {
            assertTrue(mailbox.closeAndAwait(CONTROL_TIMEOUT).clean());
        }
    }

    private static void rejectsArmConsumedBeforeConfirmation() throws Exception {
        AtomicLong clock = new AtomicLong(100);
        ObserverCoordinator coordinator = coordinator(64, clock);
        CountDownLatch controlReleased = new CountDownLatch(1);
        CountDownLatch allowControlCompletion = new CountDownLatch(1);
        java.util.concurrent.atomic.AtomicBoolean firstRelease =
                new java.util.concurrent.atomic.AtomicBoolean(true);
        EclipseCallbackMailbox mailbox = new EclipseCallbackMailbox(
                8,
                coordinator,
                () -> { },
                () -> {
                    if (!firstRelease.compareAndSet(true, false)) {
                        return;
                    }
                    controlReleased.countDown();
                    try {
                        if (!allowControlCompletion.await(2, TimeUnit.SECONDS)) {
                            throw new AssertionError(
                                    "arm control release was not resumed");
                        }
                    } catch (InterruptedException interrupted) {
                        Thread.currentThread().interrupt();
                        throw new AssertionError(
                                "arm control release was interrupted", interrupted);
                    }
                });
        mailbox.start();
        OrderedObserverTransportControl control =
                new OrderedObserverTransportControl(
                        coordinator, mailbox, CONTROL_TIMEOUT);
        UUID requestId = new UUID(0, 14);
        String name = "CONSUMED: Monte Carlo Sampling";
        AtomicReference<ArmOutcome> outcome = new AtomicReference<>();
        AtomicReference<Throwable> failure = new AtomicReference<>();
        Thread armThread = new Thread(() -> {
            try {
                outcome.set(control.armIfHealthy(
                        requestId, OperationKind.NASH, name, 1_000));
            } catch (Throwable thrown) {
                failure.set(thrown);
            }
        }, "runtime-arm-consumed-test");
        armThread.start();
        try {
            assertTrue(controlReleased.await(2, TimeUnit.SECONDS));
            CallbackEntry entry = mailbox.beginCallback();
            assertTrue(entry != null);
            assertTrue(mailbox.admitCallback(
                    entry, new ObservationTime(UTC, 101)));
            mailbox.completeCallback(entry, new ProfiledLifecycle(
                    LifecycleInput.scheduled(
                            new Object(), descriptor(name), UTC, 101)));
            allowControlCompletion.countDown();
            armThread.join(2_000);
            assertFalse(armThread.isAlive());
            if (failure.get() != null) {
                throw new AssertionError("arm confirmation failed", failure.get());
            }
            assertEquals(ArmOutcome.FAULTED, outcome.get());
            assertEquals(FaultReason.ARM_CONFIRMATION_LOST,
                    coordinator.faultReason());
            ReplayQuery replay = coordinator.replayAfter(0);
            assertEquals(3, replay.events().size());
            assertTrue(replay.events().get(0) instanceof ArmAcceptedEvent);
            assertTrue(replay.events().get(1) instanceof JobScheduledEvent);
            assertEquals(FaultReason.ARM_CONFIRMATION_LOST,
                    ((ObserverFaultEvent) replay.events().get(2)).reason());
        } finally {
            allowControlCompletion.countDown();
            armThread.join(2_000);
            assertTrue(mailbox.closeAndAwait(CONTROL_TIMEOUT).clean());
        }
    }

    private static void startsArmLeaseAtConfirmation() {
        AtomicLong clock = new AtomicLong(100);
        ObserverCoordinator coordinator = coordinator(64, clock);
        java.util.concurrent.atomic.AtomicBoolean firstRelease =
                new java.util.concurrent.atomic.AtomicBoolean(true);
        EclipseCallbackMailbox mailbox = new EclipseCallbackMailbox(
                8,
                coordinator,
                () -> { },
                () -> {
                    if (firstRelease.compareAndSet(true, false)) {
                        clock.set(105);
                    }
                });
        mailbox.start();
        OrderedObserverTransportControl control =
                new OrderedObserverTransportControl(
                        coordinator, mailbox, CONTROL_TIMEOUT);
        try {
            assertEquals(ArmOutcome.ACCEPTED, control.armIfHealthy(
                    new UUID(0, 15),
                    OperationKind.NASH,
                    "LEASE: Monte Carlo Sampling",
                    10));

            clock.set(114);
            assertTrue(control.checkpoint(0).actionable());
            clock.set(115);
            ObserverCheckpoint expired = control.checkpoint(0);
            assertFalse(expired.actionable());
            assertEquals(FaultReason.ARM_DEADLINE_EXPIRED,
                    expired.observerFault());
        } finally {
            assertTrue(mailbox.closeAndAwait(CONTROL_TIMEOUT).clean());
        }
    }

    private static void renewsLeaseForIdempotentRetry() {
        Fixture fixture = fixture(8, 100);
        UUID requestId = new UUID(0, 16);
        String name = "RETRY: Monte Carlo Sampling";
        try {
            assertEquals(ArmOutcome.ACCEPTED, fixture.control().armIfHealthy(
                    requestId, OperationKind.NASH, name, 10));
            fixture.clock().set(109);
            assertEquals(ArmOutcome.IDEMPOTENT, fixture.control().armIfHealthy(
                    requestId, OperationKind.NASH, name, 10));

            fixture.clock().set(118);
            assertTrue(fixture.control().checkpoint(0).actionable());
            fixture.clock().set(119);
            ObserverCheckpoint expired = fixture.control().checkpoint(0);
            assertFalse(expired.actionable());
            assertEquals(FaultReason.ARM_DEADLINE_EXPIRED,
                    expired.observerFault());
            long confirmations = expired.replay().events().stream()
                    .filter(ArmConfirmedEvent.class::isInstance)
                    .count();
            assertEquals(2L, confirmations);
        } finally {
            assertTrue(fixture.close().clean());
        }
    }

    private static void projectsObserverFaultAsNonActionable() {
        Fixture fixture = fixture(8, 100);
        try {
            assertEquals(ArmOutcome.ACCEPTED, fixture.control().armIfHealthy(
                    new UUID(0, 20),
                    OperationKind.NASH,
                    "FAULT: Monte Carlo Sampling",
                    10));
            fixture.clock().set(111);
            ObserverCheckpoint checkpoint = fixture.control().checkpoint(0);

            assertFalse(checkpoint.actionable());
            assertEquals(FaultReason.ARM_DEADLINE_EXPIRED,
                    checkpoint.observerFault());
            assertEquals(CallbackHealth.HEALTHY, checkpoint.callbackHealth());
            assertEquals(null, checkpoint.callbackFailure());
            assertEquals(3, checkpoint.replay().events().size());
        } finally {
            assertTrue(fixture.close().clean());
        }
    }

    private static void servesOrderedControlOverLoopback() throws Exception {
        Fixture fixture = fixture(8, 100);
        byte[] token = new byte[LocalObserverServer.TOKEN_BYTES];
        java.util.Arrays.fill(token, (byte) 7);
        LocalObserverServer server = new LocalObserverServer(
                fixture.control(), token, CONTROL_TIMEOUT);
        TransportEndpoint endpoint = server.start();
        try (Socket socket = new Socket(endpoint.address(), endpoint.port());
                BufferedReader reader = new BufferedReader(new InputStreamReader(
                        socket.getInputStream(), StandardCharsets.US_ASCII));
                BufferedWriter writer = new BufferedWriter(new OutputStreamWriter(
                        socket.getOutputStream(), StandardCharsets.US_ASCII))) {
            socket.setSoTimeout(2_000);
            writeLine(writer, "HELLO\t1\t" + Base64.getUrlEncoder()
                    .withoutPadding().encodeToString(token));
            assertEquals("READY\t1\t" + SESSION, reader.readLine());

            UUID request = new UUID(0, 40);
            String jobName = "LOOPBACK: Monte Carlo Sampling";
            writeLine(writer, "ARM\t" + SESSION + "\t" + request
                    + "\tNASH\t" + Base64.getUrlEncoder().withoutPadding()
                            .encodeToString(jobName.getBytes(StandardCharsets.UTF_8))
                    + "\t5000");
            assertEquals("ARM\t" + request + "\tACCEPTED", reader.readLine());

            writeLine(writer, "CHECKPOINT\t" + SESSION + "\t0");
            String checkpoint = reader.readLine();
            assertTrue(checkpoint.startsWith("CHECKPOINT\t{"));
            assertTrue(checkpoint.contains("\"actionable\":true"));
            assertTrue(checkpoint.contains("\"type\":\"ARM_ACCEPTED\""));
            assertTrue(checkpoint.contains("\"type\":\"ARM_CONFIRMED\""));
            writeLine(writer, "BYE");
            assertEquals("BYE", reader.readLine());
        } finally {
            assertTrue(server.closeAndAwait(CONTROL_TIMEOUT).clean());
            assertTrue(fixture.close().clean());
            java.util.Arrays.fill(token, (byte) 0);
        }
    }

    private static void rejectsInvalidCursorWithoutCoreMutation() {
        Fixture fixture = fixture(8, 100);
        try {
            assertEquals(ArmOutcome.ACCEPTED, fixture.control().armIfHealthy(
                    new UUID(0, 30),
                    OperationKind.NASH,
                    "CURSOR: Monte Carlo Sampling",
                    10));
            fixture.clock().set(111);
            assertThrows(IllegalArgumentException.class,
                    () -> fixture.control().checkpoint(-1));
            assertFalse(fixture.coordinator().isFaulted());
            assertEquals(2, fixture.coordinator().replayAfter(0).events().size());
        } finally {
            assertTrue(fixture.close().clean());
        }
    }

    private static Fixture fixture(int mailboxCapacity, long initialNanos) {
        AtomicLong clock = new AtomicLong(initialNanos);
        ObserverCoordinator coordinator = coordinator(64, clock);
        EclipseCallbackMailbox mailbox =
                new EclipseCallbackMailbox(mailboxCapacity, coordinator);
        mailbox.start();
        return new Fixture(
                coordinator,
                mailbox,
                new OrderedObserverTransportControl(
                        coordinator, mailbox, CONTROL_TIMEOUT),
                clock);
    }

    private static ObserverCoordinator coordinator(
            int replayCapacity, AtomicLong clock) {
        return new ObserverCoordinator(
                SESSION,
                List.of(
                        new OperationProfile(
                                OperationKind.NASH, BUNDLE, VERSION, NASH_CLASS),
                        new OperationProfile(
                                OperationKind.VIEWER_SAVE,
                                BUNDLE, VERSION, "viewer"),
                        new OperationProfile(
                                OperationKind.EXPORT,
                                BUNDLE, VERSION, "export")),
                16,
                16,
                replayCapacity,
                clock::get,
                () -> UTC);
    }

    private static JobDescriptor descriptor(String name) {
        return new JobDescriptor(
                BUNDLE, VERSION, NASH_CLASS, name, true, false);
    }

    private static boolean joined(Thread thread, long millis)
            throws InterruptedException {
        thread.join(millis);
        return !thread.isAlive();
    }

    private static void writeLine(BufferedWriter writer, String value)
            throws Exception {
        writer.write(value);
        writer.write('\n');
        writer.flush();
    }

    private static TestCase test(String name, ThrowingRunnable body) {
        return new TestCase(name, body);
    }

    private static void assertTrue(boolean condition) {
        if (!condition) {
            throw new AssertionError("expected true");
        }
    }

    private static void assertFalse(boolean condition) {
        if (condition) {
            throw new AssertionError("expected false");
        }
    }

    private static void assertEquals(Object expected, Object actual) {
        if (!Objects.equals(expected, actual)) {
            throw new AssertionError("expected " + expected + " but got " + actual);
        }
    }

    private static void assertThrows(
            Class<? extends Throwable> expected, ThrowingRunnable body) {
        try {
            body.run();
        } catch (Throwable failure) {
            if (expected.isInstance(failure)) {
                return;
            }
            throw new AssertionError("expected " + expected.getName()
                    + " but got " + failure, failure);
        }
        throw new AssertionError("expected " + expected.getName());
    }

    private record TestCase(String name, ThrowingRunnable body) {
    }

    @FunctionalInterface
    private interface ThrowingRunnable {
        void run() throws Exception;
    }

    private record Fixture(
            ObserverCoordinator coordinator,
            EclipseCallbackMailbox mailbox,
            OrderedObserverTransportControl control,
            AtomicLong clock) {
        private MailboxCloseResult close() {
            return mailbox.closeAndAwait(CONTROL_TIMEOUT);
        }
    }

}
