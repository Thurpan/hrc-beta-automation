package net.hrcautomation.jobobserver;

import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicLong;
import java.util.concurrent.atomic.AtomicReference;
import org.eclipse.core.runtime.IProgressMonitor;
import org.eclipse.core.runtime.IStatus;
import org.eclipse.core.runtime.jobs.IJobChangeEvent;
import org.eclipse.core.runtime.jobs.Job;

public final class EclipseJobsAdapterTest {
    private static final Instant UTC = Instant.parse("2026-08-12T15:00:00Z");
    private static final String BUNDLE = "net.holdemresources.calculator";
    private static final String VERSION = "4.1.1.202607211244";
    private static final Duration CLOSE_TIMEOUT = Duration.ofSeconds(3);
    private static final long WAIT_MILLIS = 3_000;
    private static final long NEGATIVE_WAIT_MILLIS = 100;

    private EclipseJobsAdapterTest() {
    }

    public static void main(String[] args) {
        List<TestCase> tests = List.of(
                test("mapsAllThreeOperationClasses",
                        EclipseJobsAdapterTest::mapsAllThreeOperationClasses),
                test("capturesOnlyAllowedStatusFieldsAndMissingStatus",
                        EclipseJobsAdapterTest::capturesOnlyAllowedStatusFieldsAndMissingStatus),
                test("ignoresUnknownClassBeforeBundleAndResult",
                        EclipseJobsAdapterTest::ignoresUnknownClassBeforeBundleAndResult),
                test("reportsWrongBundleAsMinimalSourceMismatch",
                        EclipseJobsAdapterTest::reportsWrongBundleAsMinimalSourceMismatch),
                test("latchesNullClockCaptureFailure",
                        EclipseJobsAdapterTest::latchesNullClockCaptureFailure),
                test("latchesObservedCaptureFailure",
                        EclipseJobsAdapterTest::latchesObservedCaptureFailure),
                test("preservesFailureWhenReporterThrows",
                        EclipseJobsAdapterTest::preservesFailureWhenReporterThrows),
                test("listenerReturnsWhileIngressIsBlocked",
                        EclipseJobsAdapterTest::listenerReturnsWhileIngressIsBlocked),
                test("faultsOnBoundedMailboxOverflow",
                        EclipseJobsAdapterTest::faultsOnBoundedMailboxOverflow),
                test("dispatchesCallbacksInTicketOrder",
                        EclipseJobsAdapterTest::dispatchesCallbacksInTicketOrder),
                test("preArmCallbackTimestampIsRejected",
                        EclipseJobsAdapterTest::preArmCallbackTimestampIsRejected),
                test("sustainedCallbacksCoalesceWorkerWakeups",
                        EclipseJobsAdapterTest::sustainedCallbacksCoalesceWorkerWakeups),
                test("duplicateCompletionAndFailureKeepCountersBalanced",
                        EclipseJobsAdapterTest::duplicateCompletionAndFailureKeepCountersBalanced),
                test("closeBeforeStartIsCleanAndFinal",
                        EclipseJobsAdapterTest::closeBeforeStartIsCleanAndFinal),
                test("closeLinearizesBeforeCallbackLease",
                        EclipseJobsAdapterTest::closeLinearizesBeforeCallbackLease),
                test("lateCompletionAfterTimedOutCloseReleasesReservation",
                        EclipseJobsAdapterTest::lateCompletionAfterTimedOutCloseReleasesReservation),
                test("closeWaitsForCallbackBeforeTimestampAdmission",
                        EclipseJobsAdapterTest::closeWaitsForCallbackBeforeTimestampAdmission),
                test("closeDuringEnteredCallbackFaultsInsteadOfDropping",
                        EclipseJobsAdapterTest::closeDuringEnteredCallbackFaultsInsteadOfDropping),
                test("lowerDispatchFinishesBeforeLaterFailureIsReported",
                        EclipseJobsAdapterTest::lowerDispatchFinishesBeforeLaterFailureIsReported),
                test("cleanCloseDrainsAndIsSingleUse",
                        EclipseJobsAdapterTest::cleanCloseDrainsAndIsSingleUse),
                test("latchesDispatchFailure",
                        EclipseJobsAdapterTest::latchesDispatchFailure),
                test("rethrowsFatalCaptureFailure",
                        EclipseJobsAdapterTest::rethrowsFatalCaptureFailure),
                test("leavesOtherCallbacksAsNoOps",
                        EclipseJobsAdapterTest::leavesOtherCallbacksAsNoOps),
                test("rejectsFrameworkResolutionOutsideOsgi",
                        EclipseJobsAdapterTest::rejectsFrameworkResolutionOutsideOsgi),
                test("integratesExactOkWithCore",
                        EclipseJobsAdapterTest::integratesExactOkWithCore),
                test("faultsCoreOnMissingDoneStatus",
                        EclipseJobsAdapterTest::faultsCoreOnMissingDoneStatus),
                test("faultsCoreOnWrongBundle",
                        EclipseJobsAdapterTest::faultsCoreOnWrongBundle));

        int passed = 0;
        for (TestCase test : tests) {
            try {
                test.body.run();
                passed++;
                System.out.println("PASS " + test.name);
            } catch (Throwable failure) {
                System.err.println("FAIL " + test.name + ": " + failure);
                failure.printStackTrace(System.err);
                System.exit(1);
            }
        }
        System.out.println("PASS " + passed + "/" + tests.size());
    }

    private static void mapsAllThreeOperationClasses() {
        record Case(Job job, OperationKind operation, long observedNanos) {
        }
        List<Case> cases = List.of(
                new Case(new FakeNashJob("HU-2: Monte Carlo Sampling"),
                        OperationKind.NASH, 101),
                new Case(new FakeViewerJob("Saving hand to: stage-a.hrcv"),
                        OperationKind.VIEWER_SAVE, 102),
                new Case(new FakeExportJob("Exporting ranges to stage-a.zip"),
                        OperationKind.EXPORT, 103));
        RecordingIngress ingress = new RecordingIngress(3, 0, 0);
        CountingResolver resolver = new CountingResolver(exactResolver());
        try (ListenerHarness harness = listener(
                ingress,
                resolver,
                new SequenceClock(
                        observed(101), observed(102), observed(103)),
                8)) {
            for (Case current : cases) {
                harness.listener.scheduled(new TestEvent(current.job(), null));
            }
            MailboxCloseResult close = harness.finish();

            assertTrue(close.clean());
            assertEquals(3, resolver.calls.get());
            List<LifecycleInput> inputs = ingress.inputs();
            assertEquals(3, inputs.size());
            for (int index = 0; index < cases.size(); index++) {
                Case expected = cases.get(index);
                LifecycleInput actual = inputs.get(index);
                JobDescriptor descriptor = actual.job();
                assertEquals(LifecycleInput.Kind.SCHEDULED, actual.kind());
                assertSame(expected.job(), actual.identity());
                assertEquals(expected.job().getClass().getName(), descriptor.className());
                assertEquals(expected.job().getName(), descriptor.name());
                assertEquals(BUNDLE, descriptor.bundleSymbolicName());
                assertEquals(VERSION, descriptor.bundleVersion());
                assertEquals(expected.operation(),
                        profiles().forClassName(descriptor.className()).operation());
                assertTrue(descriptor.user());
                assertFalse(descriptor.system());
                assertEquals(expected.observedNanos(), actual.observedNanos());
            }
            assertEquals(List.of(), ingress.failures());
            assertEquals(List.of(), ingress.sourceMismatches());
        }
    }

    private static void capturesOnlyAllowedStatusFieldsAndMissingStatus() {
        RecordingIngress ingress = new RecordingIngress(4, 0, 0);
        TestStatus status = TestStatus.allowed(
                IStatus.CANCEL, false, 11, "plugin.id");
        TestEvent scheduled = new TestEvent(
                new FakeNashJob("scheduled"), status);
        TestEvent running = new TestEvent(
                new FakeNashJob("running"), status);
        TestEvent done = new TestEvent(
                new FakeNashJob("done"), status);
        TestEvent missing = new TestEvent(
                new FakeNashJob("missing"), null);
        try (ListenerHarness harness = listener(
                ingress,
                exactResolver(),
                new SequenceClock(
                        observed(101), observed(102), observed(103), observed(104)),
                8)) {
            harness.listener.scheduled(scheduled);
            harness.listener.running(running);
            harness.listener.done(done);
            harness.listener.done(missing);
            MailboxCloseResult close = harness.finish();

            assertTrue(close.clean());
            assertEquals(0, scheduled.resultReads.get());
            assertEquals(0, running.resultReads.get());
            assertEquals(1, done.resultReads.get());
            assertEquals(1, missing.resultReads.get());
            assertEquals(1, status.severityReads.get());
            assertEquals(1, status.okReads.get());
            assertEquals(1, status.codeReads.get());
            assertEquals(1, status.pluginReads.get());
            assertEquals(0, status.forbiddenReads.get());

            List<LifecycleInput> inputs = ingress.inputs();
            assertEquals(4, inputs.size());
            assertEquals(LifecycleInput.Kind.SCHEDULED, inputs.get(0).kind());
            assertEquals(LifecycleInput.Kind.RUNNING, inputs.get(1).kind());
            assertEquals(LifecycleInput.Kind.DONE, inputs.get(2).kind());
            assertEquals(TerminalResult.CANCEL,
                    inputs.get(2).status().terminalResult());
            assertEquals(null, inputs.get(3).status());
        }
    }

    private static void ignoresUnknownClassBeforeBundleAndResult() {
        RecordingIngress ingress = new RecordingIngress(0, 0, 0);
        CountingResolver resolver = new CountingResolver(exactResolver());
        TestStatus status = TestStatus.allowed(IStatus.OK, true, 0, "plugin.id");
        TestEvent event = new TestEvent(new UnrelatedJob("unrelated"), status);
        try (ListenerHarness harness = listener(
                ingress, resolver, new FixedClock(observed(101)), 2)) {
            harness.listener.done(event);
            MailboxCloseResult close = harness.finish();

            assertTrue(close.clean());
            assertEquals(0, resolver.calls.get());
            assertEquals(0, event.resultReads.get());
            assertEquals(0, status.totalReads());
            assertEquals(List.of(), ingress.inputs());
            assertEquals(List.of(), ingress.sourceMismatches());
            assertEquals(List.of(), ingress.failures());
        }
    }

    private static void reportsWrongBundleAsMinimalSourceMismatch() {
        RecordingIngress ingress = new RecordingIngress(0, 1, 0);
        CountingResolver resolver = new CountingResolver(
                ignored -> new BundleIdentity("wrong.bundle", VERSION));
        TestStatus status = TestStatus.allowed(IStatus.OK, true, 0, "plugin.id");
        TestEvent event = new TestEvent(
                new FakeNashJob("must-not-be-projected"), status);
        try (ListenerHarness harness = listener(
                ingress, resolver, new FixedClock(observed(177)), 2)) {
            harness.listener.done(event);
            MailboxCloseResult close = harness.finish();

            assertTrue(close.clean());
            assertEquals(1, resolver.calls.get());
            assertEquals(0, event.resultReads.get());
            assertEquals(0, status.totalReads());
            assertEquals(List.of(), ingress.inputs());
            assertEquals(List.of(new ObservationPoint(UTC, 177)),
                    ingress.sourceMismatches());
            assertEquals(List.of(), ingress.failures());
        }
    }

    private static void latchesNullClockCaptureFailure() {
        RecordingIngress ingress = new RecordingIngress(0, 0, 1);
        CountingClock clock = new CountingClock(null);
        try (ListenerHarness harness = listener(
                ingress, exactResolver(), clock, 2)) {
            harness.listener.scheduled(new TestEvent(
                    new FakeNashJob("HU-2: Monte Carlo Sampling"), null));
            MailboxCloseResult close = harness.finish();

            assertEquals(1, clock.reads.get());
            assertIncident(close,
                    InfrastructureFailure.CALLBACK_CAPTURE_FAILED,
                    null);
            assertTrue(close.failureNotificationAttempted());
            assertTrue(close.failureNotificationSucceeded());
            assertTrue(close.workerTerminated());
            assertEquals(0, harness.mailbox.retainedCallbackCount());
            assertEquals(0, harness.mailbox.reservedCallbackCount());
            assertEquals(0, harness.mailbox.callbacksInFlightCount());
            assertEquals(List.of(new FailureRecord(
                    InfrastructureFailure.CALLBACK_CAPTURE_FAILED, null)),
                    ingress.failures());
            assertEquals(List.of(), ingress.inputs());
        }
    }

    private static void latchesObservedCaptureFailure() {
        RecordingIngress ingress = new RecordingIngress(0, 0, 1);
        try (ListenerHarness harness = listener(
                ingress, exactResolver(), new FixedClock(observed(277)), 2)) {
            harness.listener.scheduled(TestEvent.throwingJob());
            MailboxCloseResult close = harness.finish();

            ObservationPoint expected = new ObservationPoint(UTC, 277);
            assertIncident(close,
                    InfrastructureFailure.CALLBACK_CAPTURE_FAILED,
                    expected);
            assertTrue(close.failureNotificationSucceeded());
            assertTrue(close.workerTerminated());
            assertEquals(0, harness.mailbox.retainedCallbackCount());
            assertEquals(0, harness.mailbox.reservedCallbackCount());
            assertEquals(0, harness.mailbox.callbacksInFlightCount());
            assertEquals(List.of(new FailureRecord(
                    InfrastructureFailure.CALLBACK_CAPTURE_FAILED, expected)),
                    ingress.failures());
        }
    }

    private static void preservesFailureWhenReporterThrows() {
        ThrowingFailureIngress ingress = new ThrowingFailureIngress();
        SequenceClock clock = new SequenceClock((ObservationTime) null);
        try (ListenerHarness harness = listener(
                ingress, exactResolver(), clock, 2)) {
            harness.listener.scheduled(new TestEvent(
                    new FakeNashJob("first"), null));
            harness.listener.scheduled(new TestEvent(
                    new FakeNashJob("must-be-ignored"), null));
            MailboxCloseResult firstClose = harness.finish();
            MailboxCloseResult secondClose = harness.finish();

            assertEquals(1, clock.reads.get());
            assertEquals(1, ingress.failureAttempts.get());
            assertIncident(firstClose,
                    InfrastructureFailure.CALLBACK_CAPTURE_FAILED,
                    null);
            assertTrue(firstClose.failureNotificationAttempted());
            assertFalse(firstClose.failureNotificationSucceeded());
            assertTrue(firstClose.workerTerminated());
            assertIncident(secondClose,
                    InfrastructureFailure.CALLBACK_CAPTURE_FAILED,
                    null);
            assertTrue(secondClose.failureNotificationAttempted());
            assertFalse(secondClose.failureNotificationSucceeded());
            assertTrue(secondClose.workerTerminated());
            assertEquals(InfrastructureFailure.CALLBACK_CAPTURE_FAILED,
                    harness.mailbox.firstFailure().orElseThrow().failure());
        }
    }

    private static void listenerReturnsWhileIngressIsBlocked() throws Exception {
        BlockingIngress ingress = new BlockingIngress();
        ListenerHarness harness = listener(
                ingress, exactResolver(), new FixedClock(observed(101)), 2);
        AsyncCall callback = startAsync("blocked-ingress-callback", () ->
                harness.listener.scheduled(new TestEvent(
                        new FakeNashJob("HU-2: Monte Carlo Sampling"), null)));
        try {
            await(ingress.acceptEntered, "mailbox worker did not enter ingress");
            await(callback.finished,
                    "listener callback waited for the blocked ingress");
        } finally {
            ingress.releaseAccept.countDown();
        }
        callback.rethrowFailure();
        MailboxCloseResult close = harness.finish();
        harness.close();

        assertTrue(close.clean());
        assertEquals(1, ingress.accepted.get());
    }

    private static void faultsOnBoundedMailboxOverflow() throws Exception {
        RecordingIngress ingress = new RecordingIngress(0, 0, 1);
        SequenceClock clock = new SequenceClock(observed(101), observed(102));
        BlockingFirstResolver resolver = new BlockingFirstResolver(exactResolver());
        ListenerHarness harness = listener(ingress, resolver, clock, 1);
        AsyncCall first = startAsync("overflow-first-callback", () ->
                harness.listener.scheduled(new TestEvent(
                        new FakeNashJob("first"), null)));
        await(resolver.firstEntered,
                "first callback did not reserve the mailbox slot");
        try {
            harness.listener.scheduled(new TestEvent(
                    new FakeNashJob("overflow"), null));
        } finally {
            resolver.releaseFirst.countDown();
        }
        await(first.finished, "first callback did not finish after release");
        first.rethrowFailure();
        MailboxCloseResult close = harness.finish();
        harness.close();

        assertEquals(2, clock.reads.get());
        assertIncident(close,
                InfrastructureFailure.CALLBACK_QUEUE_OVERFLOW,
                new ObservationPoint(UTC, 102));
        assertTrue(close.failureNotificationSucceeded());
        assertTrue(close.workerTerminated());
        assertEquals(0, harness.mailbox.retainedCallbackCount());
        assertEquals(0, harness.mailbox.reservedCallbackCount());
        assertEquals(0, harness.mailbox.callbacksInFlightCount());
        assertEquals(List.of(), ingress.inputs());
        assertEquals(List.of(new FailureRecord(
                InfrastructureFailure.CALLBACK_QUEUE_OVERFLOW,
                new ObservationPoint(UTC, 102))),
                ingress.failures());
    }

    private static void dispatchesCallbacksInTicketOrder() throws Exception {
        RecordingIngress ingress = new RecordingIngress(2, 0, 0);
        SequenceClock clock = new SequenceClock(observed(101), observed(102));
        BlockingFirstResolver resolver = new BlockingFirstResolver(exactResolver());
        ListenerHarness harness = listener(ingress, resolver, clock, 4);
        AsyncCall first = startAsync("fifo-first-callback", () ->
                harness.listener.scheduled(new TestEvent(
                        new FakeNashJob("first"), null)));
        await(resolver.firstEntered,
                "first callback did not obtain the first mailbox ticket");
        harness.listener.scheduled(new TestEvent(
                new FakeNashJob("second"), null));
        assertNotSignalled(ingress.anyInput,
                "second callback was dispatched before the first completed");
        resolver.releaseFirst.countDown();
        await(first.finished, "first callback did not finish after release");
        first.rethrowFailure();
        await(ingress.allInputs, "mailbox did not dispatch both callbacks");
        MailboxCloseResult close = harness.finish();
        harness.close();

        assertTrue(close.clean());
        List<LifecycleInput> inputs = ingress.inputs();
        assertEquals(List.of("first", "second"), inputs.stream()
                .map(input -> input.job().name())
                .toList());
        assertEquals(List.of(101L, 102L), inputs.stream()
                .map(LifecycleInput::observedNanos)
                .toList());
    }

    private static void preArmCallbackTimestampIsRejected() throws Exception {
        AtomicLong coordinatorNow = new AtomicLong(200);
        ObserverCoordinator coordinator = new ObserverCoordinator(
                new UUID(0, 100),
                profiles(),
                16,
                16,
                64,
                coordinatorNow::get,
                () -> UTC);
        BlockingCapturedClock clock = new BlockingCapturedClock(observed(101));
        ListenerHarness harness = listener(
                coordinator, exactResolver(), clock, 2);
        AsyncCall callback = startAsync("pre-arm-callback", () ->
                harness.listener.scheduled(new TestEvent(
                        new FakeNashJob("HU-2: Monte Carlo Sampling"), null)));
        await(clock.timestampCaptured,
                "callback timestamp was not captured before the arm");

        assertEquals(ArmOutcome.ACCEPTED, coordinator.arm(
                new UUID(0, 4),
                OperationKind.NASH,
                "HU-2: Monte Carlo Sampling",
                1_000));
        clock.releaseCapture.countDown();
        await(callback.finished,
                "pre-arm callback did not finish after release");
        callback.rethrowFailure();
        MailboxCloseResult close = harness.finish();
        harness.close();

        assertTrue(close.clean());
        assertEquals(FaultReason.EVENT_BEFORE_ARM, coordinator.faultReason());
        assertEquals(0, eventsOf(
                coordinator.replayAfter(0).events(), JobScheduledEvent.class).size());
        assertEquals(0, harness.mailbox.retainedCallbackCount());
        assertEquals(0, harness.mailbox.reservedCallbackCount());
        assertEquals(0, harness.mailbox.callbacksInFlightCount());
    }

    private static void sustainedCallbacksCoalesceWorkerWakeups()
            throws Exception {
        int queuedCallbacks = 500;
        BlockingIngress ingress = new BlockingIngress();
        ListenerHarness harness = listener(
                ingress,
                exactResolver(),
                new FixedClock(observed(101)),
                queuedCallbacks + 1);
        harness.listener.scheduled(new TestEvent(
                new FakeNashJob("first"), null));
        await(ingress.acceptEntered,
                "first callback did not block the mailbox worker");

        try {
            for (int index = 0; index < queuedCallbacks; index++) {
                harness.listener.scheduled(new TestEvent(
                        new FakeNashJob("queued-" + index), null));
            }
            assertTrue(harness.mailbox.pendingWakeupCount() <= 1);
            assertEquals(queuedCallbacks,
                    harness.mailbox.retainedCallbackCount());
            assertEquals(queuedCallbacks,
                    harness.mailbox.reservedCallbackCount());
            assertEquals(0, harness.mailbox.callbacksInFlightCount());
        } finally {
            ingress.releaseAccept.countDown();
        }

        MailboxCloseResult close = harness.finish();
        harness.close();

        assertTrue(close.clean());
        assertEquals(queuedCallbacks + 1, ingress.accepted.get());
        assertTrue(harness.mailbox.pendingWakeupCount() <= 1);
        assertEquals(0, harness.mailbox.retainedCallbackCount());
        assertEquals(0, harness.mailbox.reservedCallbackCount());
        assertEquals(0, harness.mailbox.callbacksInFlightCount());
    }

    private static void duplicateCompletionAndFailureKeepCountersBalanced() {
        RecordingIngress completeIngress = new RecordingIngress(0, 0, 1);
        EclipseCallbackMailbox completeMailbox =
                new EclipseCallbackMailbox(2, completeIngress);
        completeMailbox.start();
        CallbackEntry completedEntry = completeMailbox.beginCallback();
        assertTrue(completedEntry != null);
        assertTrue(completeMailbox.admitCallback(completedEntry, observed(101)));
        completeMailbox.completeCallback(completedEntry, null);
        completeMailbox.completeCallback(completedEntry, null);
        MailboxCloseResult completeClose =
                completeMailbox.closeAndAwait(CLOSE_TIMEOUT);

        assertIncident(completeClose,
                InfrastructureFailure.CALLBACK_DISPATCH_FAILED,
                null);
        assertTrue(completeClose.workerTerminated());
        assertEquals(0, completeMailbox.retainedCallbackCount());
        assertEquals(0, completeMailbox.reservedCallbackCount());
        assertEquals(0, completeMailbox.callbacksInFlightCount());
        assertTrue(completeMailbox.pendingWakeupCount() <= 1);

        RecordingIngress failIngress = new RecordingIngress(0, 0, 1);
        EclipseCallbackMailbox failMailbox =
                new EclipseCallbackMailbox(2, failIngress);
        failMailbox.start();
        CallbackEntry failedEntry = failMailbox.beginCallback();
        assertTrue(failedEntry != null);
        assertTrue(failMailbox.admitCallback(failedEntry, observed(201)));
        InfrastructureIncident captureFailure = InfrastructureIncident.observed(
                InfrastructureFailure.CALLBACK_CAPTURE_FAILED, observed(202));
        failMailbox.failCallback(failedEntry, captureFailure);
        failMailbox.failCallback(failedEntry, captureFailure);
        MailboxCloseResult failClose = failMailbox.closeAndAwait(CLOSE_TIMEOUT);

        ObservationPoint expectedFailureTime = new ObservationPoint(UTC, 202);
        assertIncident(failClose,
                InfrastructureFailure.CALLBACK_CAPTURE_FAILED,
                expectedFailureTime);
        assertTrue(failClose.workerTerminated());
        assertEquals(0, failMailbox.retainedCallbackCount());
        assertEquals(0, failMailbox.reservedCallbackCount());
        assertEquals(0, failMailbox.callbacksInFlightCount());
        assertTrue(failMailbox.pendingWakeupCount() <= 1);
    }

    private static void closeBeforeStartIsCleanAndFinal() {
        RecordingIngress ingress = new RecordingIngress(0, 0, 0);
        EclipseCallbackMailbox mailbox = new EclipseCallbackMailbox(2, ingress);

        MailboxCloseResult firstClose = mailbox.closeAndAwait(CLOSE_TIMEOUT);
        MailboxCloseResult secondClose = mailbox.closeAndAwait(CLOSE_TIMEOUT);

        assertTrue(firstClose.clean());
        assertTrue(firstClose.workerTerminated());
        assertTrue(secondClose.clean());
        assertTrue(secondClose.workerTerminated());
        assertEquals(0, mailbox.retainedCallbackCount());
        assertEquals(0, mailbox.reservedCallbackCount());
        assertEquals(List.of(), ingress.inputs());
        assertEquals(List.of(), ingress.failures());
        assertThrows(IllegalStateException.class, mailbox::start);
    }

    private static void closeLinearizesBeforeCallbackLease() throws Exception {
        RecordingIngress ingress = new RecordingIngress(0, 0, 0);
        CountDownLatch gateRead = new CountDownLatch(1);
        CountDownLatch releaseAdmission = new CountDownLatch(1);
        EclipseCallbackMailbox mailbox = new EclipseCallbackMailbox(
                2,
                ingress,
                () -> {
                    gateRead.countDown();
                    awaitUnchecked(releaseAdmission,
                            "pre-lease callback was not released");
                });
        AtomicReference<CallbackEntry> entry = new AtomicReference<>();
        mailbox.start();
        AsyncCall callback = startAsync("pre-lease-callback", () ->
                entry.set(mailbox.beginCallback()));
        await(gateRead, "callback did not reach the pre-lease admission point");

        MailboxCloseResult close;
        try {
            close = mailbox.closeAndAwait(CLOSE_TIMEOUT);
            assertTrue(close.clean());
            assertTrue(close.workerTerminated());
            assertEquals(0L, mailbox.leasedCallbackCount());
        } finally {
            releaseAdmission.countDown();
        }

        await(callback.finished, "pre-lease callback did not return after close");
        callback.rethrowFailure();
        assertEquals(null, entry.get());
        assertEquals(0L, mailbox.leasedCallbackCount());
        assertEquals(0, mailbox.reservedCallbackCount());
        assertEquals(0, mailbox.retainedCallbackCount());
        assertEquals(List.of(), ingress.inputs());
        assertEquals(List.of(), ingress.failures());
    }

    private static void lateCompletionAfterTimedOutCloseReleasesReservation()
            throws Exception {
        RecordingIngress ingress = new RecordingIngress(0, 0, 1);
        BlockingFirstResolver resolver = new BlockingFirstResolver(exactResolver());
        ListenerHarness harness = listener(
                ingress, resolver, new FixedClock(observed(101)), 2);
        AsyncCall callback = startAsync("late-completion-callback", () ->
                harness.listener.scheduled(new TestEvent(
                        new FakeNashJob("late"), null)));
        await(resolver.firstEntered,
                "callback capture did not block after reserving its ticket");

        MailboxCloseResult timedOut = harness.mailbox.closeAndAwait(
                Duration.ofMillis(50));
        MailboxCloseResult closed = harness.mailbox.closeAndAwait(CLOSE_TIMEOUT);
        try {
            assertFalse(timedOut.workerTerminated());
            assertIncident(timedOut,
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED,
                    null);
            assertTrue(closed.workerTerminated());
            assertIncident(closed,
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED,
                    null);
            assertTrue(closed.failureNotificationSucceeded());
            assertEquals(1, harness.mailbox.retainedCallbackCount());
            assertEquals(1, harness.mailbox.reservedCallbackCount());
            assertEquals(1, harness.mailbox.callbacksInFlightCount());
        } finally {
            resolver.releaseFirst.countDown();
        }

        await(callback.finished,
                "late callback did not finish after capture was released");
        callback.rethrowFailure();
        MailboxCloseResult finalClose = harness.finish();
        harness.close();

        assertTrue(finalClose.workerTerminated());
        assertEquals(0, harness.mailbox.retainedCallbackCount());
        assertEquals(0, harness.mailbox.reservedCallbackCount());
        assertEquals(0, harness.mailbox.callbacksInFlightCount());
        assertEquals(List.of(), ingress.inputs());
        assertEquals(List.of(new FailureRecord(
                InfrastructureFailure.CALLBACK_DISPATCH_FAILED, null)),
                ingress.failures());
    }

    private static void closeWaitsForCallbackBeforeTimestampAdmission()
            throws Exception {
        RecordingIngress ingress = new RecordingIngress(0, 0, 1);
        BlockingCapturedClock clock = new BlockingCapturedClock(observed(101));
        ListenerHarness harness = listener(ingress, exactResolver(), clock, 2);
        AsyncCall callback = startAsync("pre-admission-close-callback", () ->
                harness.listener.scheduled(new TestEvent(
                        new FakeNashJob("pre-admission"), null)));
        await(clock.timestampCaptured,
                "callback did not enter before timestamp admission");

        MailboxCloseResult timedOut = harness.mailbox.closeAndAwait(
                Duration.ofMillis(50));
        assertFalse(timedOut.clean());
        assertFalse(timedOut.workerTerminated());
        assertIncident(timedOut,
                InfrastructureFailure.CALLBACK_DISPATCH_FAILED,
                null);
        assertEquals(1, harness.mailbox.callbacksInFlightCount());

        clock.releaseCapture.countDown();
        await(callback.finished,
                "pre-admission callback did not finish after release");
        callback.rethrowFailure();
        MailboxCloseResult closed = harness.finish();
        harness.close();

        assertTrue(closed.workerTerminated());
        assertIncident(closed,
                InfrastructureFailure.CALLBACK_DISPATCH_FAILED,
                null);
        assertTrue(closed.failureNotificationSucceeded());
        assertEquals(0, harness.mailbox.retainedCallbackCount());
        assertEquals(0, harness.mailbox.reservedCallbackCount());
        assertEquals(0, harness.mailbox.callbacksInFlightCount());
        assertEquals(List.of(), ingress.inputs());
        assertEquals(List.of(new FailureRecord(
                InfrastructureFailure.CALLBACK_DISPATCH_FAILED, null)),
                ingress.failures());
    }

    private static void closeDuringEnteredCallbackFaultsInsteadOfDropping()
            throws Exception {
        RecordingIngress ingress = new RecordingIngress(0, 0, 1);
        BlockingCapturedClock clock = new BlockingCapturedClock(observed(151));
        ListenerHarness harness = listener(ingress, exactResolver(), clock, 2);
        AsyncCall callback = startAsync("entered-callback", () ->
                harness.listener.scheduled(new TestEvent(
                        new FakeNashJob("entered-before-close"), null)));
        await(clock.timestampCaptured,
                "callback did not capture its timestamp before close");
        AsyncCall closeCall = startAsync("entered-callback-close", () -> {
            MailboxCloseResult close = harness.mailbox.closeAndAwait(CLOSE_TIMEOUT);
            ObservationPoint expected = new ObservationPoint(UTC, 151);
            assertFalse(close.clean());
            assertTrue(close.workerTerminated());
            assertIncident(close,
                    InfrastructureFailure.CALLBACK_DISPATCH_FAILED,
                    expected);
            assertTrue(close.failureNotificationSucceeded());
        });
        try {
            assertNotSignalled(closeCall.finished,
                    "close completed before the entered callback was released");
        } finally {
            clock.releaseCapture.countDown();
        }
        await(callback.finished, "entered callback did not finish");
        await(closeCall.finished, "close did not finish after callback release");
        callback.rethrowFailure();
        closeCall.rethrowFailure();
        harness.close();

        assertEquals(0, harness.mailbox.retainedCallbackCount());
        assertEquals(0, harness.mailbox.reservedCallbackCount());
        assertEquals(0, harness.mailbox.callbacksInFlightCount());
        assertEquals(List.of(), ingress.inputs());
        assertEquals(List.of(new FailureRecord(
                InfrastructureFailure.CALLBACK_DISPATCH_FAILED,
                new ObservationPoint(UTC, 151))), ingress.failures());
    }

    private static void lowerDispatchFinishesBeforeLaterFailureIsReported()
            throws Exception {
        OrderedBlockingIngress ingress = new OrderedBlockingIngress();
        SequenceClock clock = new SequenceClock(
                observed(101), observed(102), observed(103));
        ListenerHarness harness = listener(ingress, exactResolver(), clock, 4);
        harness.listener.scheduled(new TestEvent(
                new FakeNashJob("first"), null));
        await(ingress.acceptEntered,
                "lower-ticket dispatch did not enter ingress");

        try {
            harness.listener.scheduled(TestEvent.throwingJob());
            InfrastructureIncident incident =
                    harness.mailbox.firstFailure().orElseThrow();
            assertEquals(InfrastructureFailure.CALLBACK_CAPTURE_FAILED,
                    incident.failure());
            assertTrue(incident.hasObservation());
            assertEquals(102L, incident.observedNanos());

            harness.listener.scheduled(new TestEvent(
                    new FakeNashJob("after-fault"), null));
            assertEquals(2, clock.reads.get());
            assertNotSignalled(ingress.failureReported,
                    "failure was reported before the authorised dispatch finished");
        } finally {
            ingress.releaseAccept.countDown();
        }

        await(ingress.acceptFinished,
                "authorised lower-ticket dispatch did not finish");
        await(ingress.failureReported,
                "later capture failure was not reported");
        MailboxCloseResult close = harness.finish();
        harness.close();

        ObservationPoint expectedFailureTime = new ObservationPoint(UTC, 102);
        assertIncident(close,
                InfrastructureFailure.CALLBACK_CAPTURE_FAILED,
                expectedFailureTime);
        assertTrue(close.failureNotificationSucceeded());
        assertEquals(List.of(
                "accept:first",
                "failure:" + InfrastructureFailure.CALLBACK_CAPTURE_FAILED),
                ingress.order());
        assertEquals(expectedFailureTime, ingress.failureObservation.get());
        assertEquals(0, harness.mailbox.retainedCallbackCount());
        assertEquals(0, harness.mailbox.reservedCallbackCount());
    }

    private static void cleanCloseDrainsAndIsSingleUse() {
        RecordingIngress ingress = new RecordingIngress(1, 0, 0);
        CountingClock clock = new CountingClock(observed(101));
        ListenerHarness harness = listener(ingress, exactResolver(), clock, 2);
        harness.listener.scheduled(new TestEvent(
                new FakeNashJob("HU-2: Monte Carlo Sampling"), null));

        MailboxCloseResult firstClose = harness.finish();
        harness.listener.scheduled(new TestEvent(
                new FakeNashJob("after-close"), null));
        MailboxCloseResult secondClose = harness.finish();

        assertTrue(firstClose.clean());
        assertTrue(secondClose.clean());
        assertFalse(firstClose.failureNotificationAttempted());
        assertEquals(1, ingress.inputs().size());
        assertEquals(1, clock.reads.get());
        assertThrows(IllegalStateException.class, harness.mailbox::start);
        harness.close();
    }

    private static void latchesDispatchFailure() throws Exception {
        DispatchFailureIngress ingress = new DispatchFailureIngress();
        ListenerHarness harness = listener(
                ingress, exactResolver(), new FixedClock(observed(333)), 2);
        harness.listener.scheduled(new TestEvent(
                new FakeNashJob("HU-2: Monte Carlo Sampling"), null));
        await(ingress.failureReported,
                "dispatch failure was not reported through the failure inlet");
        harness.listener.scheduled(new TestEvent(
                new FakeNashJob("must-be-ignored"), null));
        MailboxCloseResult close = harness.finish();
        harness.close();

        ObservationPoint expected = new ObservationPoint(UTC, 333);
        assertEquals(1, ingress.acceptAttempts.get());
        assertEquals(1, ingress.failureAttempts.get());
        assertIncident(close,
                InfrastructureFailure.CALLBACK_DISPATCH_FAILED,
                expected);
        assertTrue(close.failureNotificationSucceeded());
        assertEquals(expected, ingress.failureObservation.get());
    }

    private static void rethrowsFatalCaptureFailure() {
        RecordingIngress ingress = new RecordingIngress(0, 0, 1);
        ListenerHarness harness = listener(ingress, exactResolver(), () -> {
            throw new TestVirtualMachineError();
        }, 2);
        assertThrows(TestVirtualMachineError.class, () ->
                harness.listener.scheduled(new TestEvent(
                        new FakeNashJob("HU-2: Monte Carlo Sampling"), null)));
        MailboxCloseResult close = harness.finish();
        harness.close();

        assertIncident(close,
                InfrastructureFailure.CALLBACK_CAPTURE_FAILED,
                null);
        assertTrue(close.failureNotificationSucceeded());
    }

    private static void leavesOtherCallbacksAsNoOps() {
        RecordingIngress ingress = new RecordingIngress(0, 0, 0);
        CountingClock clock = new CountingClock(observed(101));
        try (ListenerHarness harness = listener(
                ingress, exactResolver(), clock, 1)) {
            TestEvent event = new TestEvent(
                    new FakeNashJob("HU-2: Monte Carlo Sampling"), null);
            harness.listener.aboutToRun(event);
            harness.listener.awake(event);
            harness.listener.sleeping(event);
            MailboxCloseResult close = harness.finish();

            assertTrue(close.clean());
            assertEquals(0, clock.reads.get());
            assertEquals(0, event.jobReads.get());
            assertEquals(List.of(), ingress.inputs());
            assertEquals(List.of(), ingress.failures());
        }
    }

    private static void rejectsFrameworkResolutionOutsideOsgi() {
        FrameworkBundleIdentityResolver resolver =
                new FrameworkBundleIdentityResolver();
        assertThrows(IllegalStateException.class,
                () -> resolver.resolve(FakeNashJob.class));
        assertThrows(NullPointerException.class,
                () -> resolver.resolve(null));
    }

    private static void integratesExactOkWithCore() {
        ObserverCoordinator coordinator = coordinator(profiles(), 100);
        UUID request = new UUID(0, 1);
        assertEquals(ArmOutcome.ACCEPTED, coordinator.arm(
                request,
                OperationKind.NASH,
                "HU-2: Monte Carlo Sampling",
                100));
        try (ListenerHarness harness = listener(
                coordinator,
                exactResolver(),
                new SequenceClock(observed(101), observed(102), observed(103)),
                4)) {
            FakeNashJob job = new FakeNashJob("HU-2: Monte Carlo Sampling");
            harness.listener.scheduled(new TestEvent(job, null));
            harness.listener.running(new TestEvent(job, null));
            harness.listener.done(new TestEvent(job,
                    TestStatus.allowed(
                            IStatus.OK, true, 0, "org.eclipse.core.runtime")));
            assertTrue(harness.finish().clean());
        }

        JobTerminalEvent terminal = lastEvent(
                coordinator.replayAfter(0).events(), JobTerminalEvent.class);
        assertEquals(TerminalResult.OK, terminal.result());
        assertTrue(terminal.runningSeen());
        assertFalse(coordinator.isFaulted());
    }

    private static void faultsCoreOnMissingDoneStatus() {
        ObserverCoordinator coordinator = coordinator(profiles(), 100);
        coordinator.arm(new UUID(0, 2), OperationKind.NASH,
                "HU-2: Monte Carlo Sampling", 100);
        try (ListenerHarness harness = listener(
                coordinator,
                exactResolver(),
                new SequenceClock(observed(101), observed(102)),
                4)) {
            FakeNashJob job = new FakeNashJob("HU-2: Monte Carlo Sampling");
            harness.listener.scheduled(new TestEvent(job, null));
            harness.listener.done(new TestEvent(job, null));
            assertTrue(harness.finish().clean());
        }

        assertEquals(FaultReason.MISSING_TERMINAL_STATUS,
                coordinator.faultReason());
        assertEquals(0, eventsOf(
                coordinator.replayAfter(0).events(), JobTerminalEvent.class).size());
    }

    private static void faultsCoreOnWrongBundle() {
        ObserverCoordinator coordinator = coordinator(profiles(), 100);
        coordinator.arm(new UUID(0, 3), OperationKind.NASH,
                "HU-2: Monte Carlo Sampling", 100);
        try (ListenerHarness harness = listener(
                coordinator,
                ignored -> new BundleIdentity("wrong.bundle", VERSION),
                new FixedClock(observed(101)),
                2)) {
            harness.listener.scheduled(new TestEvent(
                    new FakeNashJob("HU-2: Monte Carlo Sampling"), null));
            assertTrue(harness.finish().clean());
        }

        assertEquals(FaultReason.JOB_MISMATCH, coordinator.faultReason());
        assertEquals(0, eventsOf(
                coordinator.replayAfter(0).events(), JobScheduledEvent.class).size());
    }

    private static ListenerHarness listener(
            ObserverIngress ingress,
            BundleIdentityResolver resolver,
            ObservationClock clock,
            int capacity) {
        EclipseCallbackMailbox mailbox = new EclipseCallbackMailbox(capacity, ingress);
        EclipseJobChangeListener listener = new EclipseJobChangeListener(
                new EclipseLifecycleCapture(profiles(), resolver), mailbox, clock);
        mailbox.start();
        return new ListenerHarness(mailbox, listener);
    }

    private static BundleIdentityResolver exactResolver() {
        return ignored -> new BundleIdentity(BUNDLE, VERSION);
    }

    private static OperationProfileSet profiles() {
        return new OperationProfileSet(List.of(
                new OperationProfile(OperationKind.NASH, BUNDLE, VERSION,
                        FakeNashJob.class.getName()),
                new OperationProfile(OperationKind.VIEWER_SAVE, BUNDLE, VERSION,
                        FakeViewerJob.class.getName()),
                new OperationProfile(OperationKind.EXPORT, BUNDLE, VERSION,
                        FakeExportJob.class.getName())));
    }

    private static ObserverCoordinator coordinator(
            OperationProfileSet profiles, long initialNanos) {
        return new ObserverCoordinator(
                new UUID(0, 99),
                profiles,
                16,
                16,
                64,
                () -> initialNanos,
                () -> UTC);
    }

    private static ObservationTime observed(long nanos) {
        return new ObservationTime(UTC, nanos);
    }

    private static void assertIncident(
            MailboxCloseResult result,
            InfrastructureFailure expectedFailure,
            ObservationPoint expectedObservation) {
        InfrastructureIncident incident = result.firstFailure();
        if (incident == null) {
            throw new AssertionError("expected infrastructure incident");
        }
        assertEquals(expectedFailure, incident.failure());
        assertEquals(expectedObservation != null, incident.hasObservation());
        if (expectedObservation != null) {
            assertEquals(expectedObservation.utc(), incident.observedUtc());
            assertEquals(expectedObservation.nanos(), incident.observedNanos());
        }
    }

    private static void await(CountDownLatch latch, String failureMessage)
            throws InterruptedException {
        if (!latch.await(WAIT_MILLIS, TimeUnit.MILLISECONDS)) {
            throw new AssertionError(failureMessage);
        }
    }

    private static void awaitUnchecked(CountDownLatch latch, String failureMessage) {
        try {
            await(latch, failureMessage);
        } catch (InterruptedException interrupted) {
            Thread.currentThread().interrupt();
            throw new AssertionError("interrupted while waiting", interrupted);
        }
    }

    private static void assertNotSignalled(
            CountDownLatch latch, String failureMessage) throws InterruptedException {
        if (latch.await(NEGATIVE_WAIT_MILLIS, TimeUnit.MILLISECONDS)) {
            throw new AssertionError(failureMessage);
        }
    }

    private static AsyncCall startAsync(String name, ThrowingRunnable body) {
        AtomicReference<Throwable> failure = new AtomicReference<>();
        CountDownLatch finished = new CountDownLatch(1);
        Thread thread = new Thread(() -> {
            try {
                body.run();
            } catch (Throwable caught) {
                failure.set(caught);
            } finally {
                finished.countDown();
            }
        }, name);
        thread.setDaemon(true);
        thread.start();
        return new AsyncCall(finished, failure);
    }

    private static <T> List<T> eventsOf(List<ObserverEvent> events, Class<T> type) {
        List<T> values = new ArrayList<>();
        for (ObserverEvent event : events) {
            if (type.isInstance(event)) {
                values.add(type.cast(event));
            }
        }
        return values;
    }

    private static <T> T lastEvent(List<ObserverEvent> events, Class<T> type) {
        List<T> matches = eventsOf(events, type);
        if (matches.isEmpty()) {
            throw new AssertionError("missing event " + type.getSimpleName());
        }
        return matches.get(matches.size() - 1);
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

    private static void assertSame(Object expected, Object actual) {
        if (expected != actual) {
            throw new AssertionError("expected same reference");
        }
    }

    private static void assertEquals(Object expected, Object actual) {
        if (!Objects.equals(expected, actual)) {
            throw new AssertionError("expected " + expected + " but got " + actual);
        }
    }

    private static void assertThrows(
            Class<? extends Throwable> type, ThrowingRunnable body) {
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

    private record TestCase(String name, ThrowingRunnable body) {
    }

    @FunctionalInterface
    private interface ThrowingRunnable {
        void run() throws Exception;
    }

    private static final class ListenerHarness implements AutoCloseable {
        private final EclipseCallbackMailbox mailbox;
        private final EclipseJobChangeListener listener;

        private ListenerHarness(
                EclipseCallbackMailbox mailbox,
                EclipseJobChangeListener listener) {
            this.mailbox = mailbox;
            this.listener = listener;
        }

        private MailboxCloseResult finish() {
            return mailbox.closeAndAwait(CLOSE_TIMEOUT);
        }

        @Override
        public void close() {
            mailbox.closeAndAwait(CLOSE_TIMEOUT);
        }
    }

    private record AsyncCall(
            CountDownLatch finished,
            AtomicReference<Throwable> failure) {
        private void rethrowFailure() {
            Throwable caught = failure.get();
            if (caught == null) {
                return;
            }
            if (caught instanceof RuntimeException runtime) {
                throw runtime;
            }
            if (caught instanceof Error error) {
                throw error;
            }
            throw new AssertionError("asynchronous call failed", caught);
        }
    }

    private record ObservationPoint(Instant utc, long nanos) {
        private ObservationPoint {
            Objects.requireNonNull(utc, "utc");
        }
    }

    private record FailureRecord(
            InfrastructureFailure failure,
            ObservationPoint observation) {
        private FailureRecord {
            Objects.requireNonNull(failure, "failure");
        }
    }

    private static class RecordingIngress implements ObserverIngress {
        private final List<LifecycleInput> inputs = new ArrayList<>();
        private final List<ObservationPoint> sourceMismatches = new ArrayList<>();
        private final List<FailureRecord> failures = new ArrayList<>();
        private final CountDownLatch anyInput = new CountDownLatch(1);
        private final CountDownLatch allInputs;
        private final CountDownLatch allSourceMismatches;
        private final CountDownLatch allFailures;

        private RecordingIngress(
                int expectedInputs,
                int expectedSourceMismatches,
                int expectedFailures) {
            allInputs = new CountDownLatch(expectedInputs);
            allSourceMismatches = new CountDownLatch(expectedSourceMismatches);
            allFailures = new CountDownLatch(expectedFailures);
        }

        @Override
        public synchronized void accept(LifecycleInput input) {
            inputs.add(input);
            anyInput.countDown();
            allInputs.countDown();
        }

        @Override
        public synchronized void rejectSourceMismatch(
                Instant observedUtc, long observedNanos) {
            sourceMismatches.add(new ObservationPoint(observedUtc, observedNanos));
            allSourceMismatches.countDown();
        }

        @Override
        public synchronized void failInfrastructure(InfrastructureFailure failure) {
            failures.add(new FailureRecord(failure, null));
            allFailures.countDown();
        }

        @Override
        public synchronized void failInfrastructure(
                InfrastructureFailure failure,
                Instant observedUtc,
                long observedNanos) {
            failures.add(new FailureRecord(
                    failure, new ObservationPoint(observedUtc, observedNanos)));
            allFailures.countDown();
        }

        private synchronized List<LifecycleInput> inputs() {
            return List.copyOf(inputs);
        }

        private synchronized List<ObservationPoint> sourceMismatches() {
            return List.copyOf(sourceMismatches);
        }

        private synchronized List<FailureRecord> failures() {
            return List.copyOf(failures);
        }
    }

    private static final class BlockingIngress implements ObserverIngress {
        private final CountDownLatch acceptEntered = new CountDownLatch(1);
        private final CountDownLatch releaseAccept = new CountDownLatch(1);
        private final AtomicInteger accepted = new AtomicInteger();

        @Override
        public void accept(LifecycleInput input) {
            acceptEntered.countDown();
            awaitUnchecked(releaseAccept, "blocked ingress was not released");
            accepted.incrementAndGet();
        }

        @Override
        public void rejectSourceMismatch(Instant observedUtc, long observedNanos) {
            throw new AssertionError("unexpected source mismatch");
        }

        @Override
        public void failInfrastructure(InfrastructureFailure failure) {
            throw new AssertionError("unexpected infrastructure failure: " + failure);
        }

        @Override
        public void failInfrastructure(
                InfrastructureFailure failure,
                Instant observedUtc,
                long observedNanos) {
            throw new AssertionError("unexpected infrastructure failure: " + failure);
        }
    }

    private static final class OrderedBlockingIngress implements ObserverIngress {
        private final List<String> order = new ArrayList<>();
        private final CountDownLatch acceptEntered = new CountDownLatch(1);
        private final CountDownLatch releaseAccept = new CountDownLatch(1);
        private final CountDownLatch acceptFinished = new CountDownLatch(1);
        private final CountDownLatch failureReported = new CountDownLatch(1);
        private final AtomicReference<ObservationPoint> failureObservation =
                new AtomicReference<>();

        @Override
        public void accept(LifecycleInput input) {
            acceptEntered.countDown();
            awaitUnchecked(releaseAccept,
                    "authorised ingress dispatch was not released");
            synchronized (this) {
                order.add("accept:" + input.job().name());
            }
            acceptFinished.countDown();
        }

        @Override
        public void rejectSourceMismatch(Instant observedUtc, long observedNanos) {
            throw new AssertionError("unexpected source mismatch");
        }

        @Override
        public void failInfrastructure(InfrastructureFailure failure) {
            synchronized (this) {
                order.add("failure:" + failure);
            }
            failureReported.countDown();
        }

        @Override
        public void failInfrastructure(
                InfrastructureFailure failure,
                Instant observedUtc,
                long observedNanos) {
            failureObservation.set(new ObservationPoint(observedUtc, observedNanos));
            synchronized (this) {
                order.add("failure:" + failure);
            }
            failureReported.countDown();
        }

        private synchronized List<String> order() {
            return List.copyOf(order);
        }
    }

    private static final class ThrowingFailureIngress implements ObserverIngress {
        private final AtomicInteger failureAttempts = new AtomicInteger();

        @Override
        public void accept(LifecycleInput input) {
            throw new AssertionError("failed mailbox must not dispatch lifecycle input");
        }

        @Override
        public void rejectSourceMismatch(Instant observedUtc, long observedNanos) {
            throw new AssertionError("failed mailbox must not dispatch source mismatch");
        }

        @Override
        public void failInfrastructure(InfrastructureFailure failure) {
            failureAttempts.incrementAndGet();
            throw new IllegalStateException("failure reporter failed");
        }

        @Override
        public void failInfrastructure(
                InfrastructureFailure failure,
                Instant observedUtc,
                long observedNanos) {
            failureAttempts.incrementAndGet();
            throw new IllegalStateException("failure reporter failed");
        }
    }

    private static final class DispatchFailureIngress implements ObserverIngress {
        private final AtomicInteger acceptAttempts = new AtomicInteger();
        private final AtomicInteger failureAttempts = new AtomicInteger();
        private final AtomicReference<ObservationPoint> failureObservation =
                new AtomicReference<>();
        private final CountDownLatch failureReported = new CountDownLatch(1);

        @Override
        public void accept(LifecycleInput input) {
            acceptAttempts.incrementAndGet();
            throw new IllegalStateException("dispatch failed");
        }

        @Override
        public void rejectSourceMismatch(Instant observedUtc, long observedNanos) {
            throw new AssertionError("unexpected source mismatch");
        }

        @Override
        public void failInfrastructure(InfrastructureFailure failure) {
            failureAttempts.incrementAndGet();
            failureReported.countDown();
        }

        @Override
        public void failInfrastructure(
                InfrastructureFailure failure,
                Instant observedUtc,
                long observedNanos) {
            failureAttempts.incrementAndGet();
            failureObservation.set(new ObservationPoint(observedUtc, observedNanos));
            failureReported.countDown();
        }
    }

    private static final class FixedClock implements ObservationClock {
        private final ObservationTime value;

        private FixedClock(ObservationTime value) {
            this.value = value;
        }

        @Override
        public ObservationTime capture() {
            return value;
        }
    }

    private static final class CountingClock implements ObservationClock {
        private final ObservationTime value;
        private final AtomicInteger reads = new AtomicInteger();

        private CountingClock(ObservationTime value) {
            this.value = value;
        }

        @Override
        public ObservationTime capture() {
            reads.incrementAndGet();
            return value;
        }
    }

    private static final class SequenceClock implements ObservationClock {
        private final List<ObservationTime> values;
        private final AtomicInteger reads = new AtomicInteger();

        private SequenceClock(ObservationTime... values) {
            Objects.requireNonNull(values, "values");
            this.values = new ArrayList<>(values.length);
            for (ObservationTime value : values) {
                this.values.add(value);
            }
        }

        @Override
        public ObservationTime capture() {
            int index = reads.getAndIncrement();
            if (index >= values.size()) {
                throw new AssertionError("clock exhausted");
            }
            return values.get(index);
        }
    }

    private static final class BlockingCapturedClock implements ObservationClock {
        private final ObservationTime captured;
        private final CountDownLatch timestampCaptured = new CountDownLatch(1);
        private final CountDownLatch releaseCapture = new CountDownLatch(1);

        private BlockingCapturedClock(ObservationTime captured) {
            this.captured = Objects.requireNonNull(captured, "captured");
        }

        @Override
        public ObservationTime capture() {
            timestampCaptured.countDown();
            awaitUnchecked(releaseCapture,
                    "captured callback timestamp was not released");
            return captured;
        }
    }

    private static final class CountingResolver implements BundleIdentityResolver {
        private final BundleIdentityResolver delegate;
        private final AtomicInteger calls = new AtomicInteger();

        private CountingResolver(BundleIdentityResolver delegate) {
            this.delegate = delegate;
        }

        @Override
        public BundleIdentity resolve(Class<?> jobClass) {
            calls.incrementAndGet();
            return delegate.resolve(jobClass);
        }
    }

    private static final class BlockingFirstResolver
            implements BundleIdentityResolver {
        private final BundleIdentityResolver delegate;
        private final AtomicInteger calls = new AtomicInteger();
        private final CountDownLatch firstEntered = new CountDownLatch(1);
        private final CountDownLatch releaseFirst = new CountDownLatch(1);

        private BlockingFirstResolver(BundleIdentityResolver delegate) {
            this.delegate = Objects.requireNonNull(delegate, "delegate");
        }

        @Override
        public BundleIdentity resolve(Class<?> jobClass) {
            if (calls.getAndIncrement() == 0) {
                firstEntered.countDown();
                awaitUnchecked(releaseFirst,
                        "first bundle resolution was not released");
            }
            return delegate.resolve(jobClass);
        }
    }

    private static final class TestEvent implements IJobChangeEvent {
        private final Job job;
        private final IStatus result;
        private final RuntimeException jobFailure;
        private final AtomicInteger jobReads = new AtomicInteger();
        private final AtomicInteger resultReads = new AtomicInteger();

        private TestEvent(Job job, IStatus result) {
            this.job = job;
            this.result = result;
            jobFailure = null;
        }

        private TestEvent(RuntimeException jobFailure) {
            job = null;
            result = null;
            this.jobFailure = jobFailure;
        }

        private static TestEvent throwingJob() {
            return new TestEvent(new IllegalStateException("event failed"));
        }

        @Override
        public long getDelay() {
            throw new AssertionError("getDelay must not be called");
        }

        @Override
        public Job getJob() {
            jobReads.incrementAndGet();
            if (jobFailure != null) {
                throw jobFailure;
            }
            return job;
        }

        @Override
        public IStatus getResult() {
            resultReads.incrementAndGet();
            return result;
        }

        @Override
        public IStatus getJobGroupResult() {
            throw new AssertionError("getJobGroupResult must not be called");
        }
    }

    private static final class TestStatus implements IStatus {
        private final int severity;
        private final boolean ok;
        private final int code;
        private final String plugin;
        private final AtomicInteger severityReads = new AtomicInteger();
        private final AtomicInteger okReads = new AtomicInteger();
        private final AtomicInteger codeReads = new AtomicInteger();
        private final AtomicInteger pluginReads = new AtomicInteger();
        private final AtomicInteger forbiddenReads = new AtomicInteger();

        private TestStatus(int severity, boolean ok, int code, String plugin) {
            this.severity = severity;
            this.ok = ok;
            this.code = code;
            this.plugin = plugin;
        }

        private static TestStatus allowed(
                int severity, boolean ok, int code, String plugin) {
            return new TestStatus(severity, ok, code, plugin);
        }

        private int totalReads() {
            return severityReads.get()
                    + okReads.get()
                    + codeReads.get()
                    + pluginReads.get()
                    + forbiddenReads.get();
        }

        @Override
        public IStatus[] getChildren() {
            forbiddenReads.incrementAndGet();
            throw new AssertionError("getChildren must not be called");
        }

        @Override
        public int getCode() {
            codeReads.incrementAndGet();
            return code;
        }

        @Override
        public Throwable getException() {
            forbiddenReads.incrementAndGet();
            throw new AssertionError("getException must not be called");
        }

        @Override
        public String getMessage() {
            forbiddenReads.incrementAndGet();
            throw new AssertionError("getMessage must not be called");
        }

        @Override
        public String getPlugin() {
            pluginReads.incrementAndGet();
            return plugin;
        }

        @Override
        public int getSeverity() {
            severityReads.incrementAndGet();
            return severity;
        }

        @Override
        public boolean isMultiStatus() {
            forbiddenReads.incrementAndGet();
            throw new AssertionError("isMultiStatus must not be called");
        }

        @Override
        public boolean isOK() {
            okReads.incrementAndGet();
            return ok;
        }

        @Override
        public boolean matches(int severityMask) {
            forbiddenReads.incrementAndGet();
            throw new AssertionError("matches must not be called");
        }
    }

    private abstract static class TestJob extends Job {
        private TestJob(String name) {
            super(name);
            setUser(true);
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
        private UnrelatedJob(String name) {
            super(name);
        }
    }

    private static final class TestVirtualMachineError extends VirtualMachineError {
        private static final long serialVersionUID = 1L;
    }
}
