package net.hrcautomation.jobobserver;

import java.lang.reflect.Method;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicLong;
import java.util.concurrent.atomic.AtomicReference;

public final class ObserverCoreTest {
    private static final UUID SESSION = UUID.fromString("00000000-0000-0000-0000-000000000001");
    private static final String BUNDLE = "net.holdemresources.calculator";
    private static final String VERSION = "4.1.1.202607211244";
    private static final String NASH_CLASS = "net.holdemresources.internal.bQ";
    private static final String VIEWER_CLASS = "net.holdemresources.internal.bT";
    private static final String EXPORT_CLASS = "net.holdemresources.internal.af";
    private static final Instant UTC = Instant.parse("2026-08-12T12:00:00Z");

    private ObserverCoreTest() {
    }

    public static void main(String[] args) throws Exception {
        List<TestCase> tests = List.of(
                test("validatesOperationNames", ObserverCoreTest::validatesOperationNames),
                test("validatesLifecycleInput", ObserverCoreTest::validatesLifecycleInput),
                test("faultsOnMissingStatusAndInfrastructureFailure", ObserverCoreTest::faultsOnMissingStatusAndInfrastructureFailure),
                test("rejectsSourceMismatchWithOrderingPrecedence", ObserverCoreTest::rejectsSourceMismatchWithOrderingPrecedence),
                test("acceptsAndRetriesArmWithoutChangingDeadline", ObserverCoreTest::acceptsAndRetriesArmWithoutChangingDeadline),
                test("rejectsInvalidBusyAndExpiredArms", ObserverCoreTest::rejectsInvalidBusyAndExpiredArms),
                test("rejectsArmBeforeJobCapacityIsExceeded", ObserverCoreTest::rejectsArmBeforeJobCapacityIsExceeded),
                test("faultsOnConflictingRequestReuse", ObserverCoreTest::faultsOnConflictingRequestReuse),
                test("filtersExactSourceAndName", ObserverCoreTest::filtersExactSourceAndName),
                test("correlatesAllOperationProfiles", ObserverCoreTest::correlatesAllOperationProfiles),
                test("correlatesByReferenceIdentity", ObserverCoreTest::correlatesByReferenceIdentity),
                test("correlatesSameNameNashJobsSequentially", ObserverCoreTest::correlatesSameNameNashJobsSequentially),
                test("recordsOkCancelAndError", ObserverCoreTest::recordsOkCancelAndError),
                test("faultsOnInvalidLifecycleAndDescriptorChanges", ObserverCoreTest::faultsOnInvalidLifecycleAndDescriptorChanges),
                test("faultsOnDuplicateScheduleAndDone", ObserverCoreTest::faultsOnDuplicateScheduleAndDone),
                test("emitsUnknownTerminalBeforeFault", ObserverCoreTest::emitsUnknownTerminalBeforeFault),
                test("recordsTrackedDoneAfterFault", ObserverCoreTest::recordsTrackedDoneAfterFault),
                test("recordsPostFaultUnknownTerminalExactly", ObserverCoreTest::recordsPostFaultUnknownTerminalExactly),
                test("recordsTrackedRunningWhenAnotherArmExpires", ObserverCoreTest::recordsTrackedRunningWhenAnotherArmExpires),
                test("rejectsTrackedRunningAfterFault", ObserverCoreTest::rejectsTrackedRunningAfterFault),
                test("faultsOnCallbackTimeRegression", ObserverCoreTest::faultsOnCallbackTimeRegression),
                test("usesCallbackTimeForArmDeadline", ObserverCoreTest::usesCallbackTimeForArmDeadline),
                test("handlesNanoTimeWrap", ObserverCoreTest::handlesNanoTimeWrap),
                test("checkpointsReplayAndExpiryAtomically",
                        ObserverCoreTest::checkpointsReplayAndExpiryAtomically),
                test("rejectsInvalidCheckpointWithoutMutation",
                        ObserverCoreTest::rejectsInvalidCheckpointWithoutMutation),
                test("serializesCheckpointAgainstLifecycleInput",
                        ObserverCoreTest::serializesCheckpointAgainstLifecycleInput),
                test("replayBufferBoundariesAndImmutability", ObserverCoreTest::replayBufferBoundariesAndImmutability),
                test("replayBufferFactoryFailureIsTransactional", ObserverCoreTest::replayBufferFactoryFailureIsTransactional),
                test("replayBufferIsThreadSafe", ObserverCoreTest::replayBufferIsThreadSafe),
                test("eventsExcludeRawIdentityAndSensitiveStatusText", ObserverCoreTest::eventsExcludeRawIdentityAndSensitiveStatusText));

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

    private static void validatesOperationNames() {
        assertTrue(OperationKind.NASH.acceptsExpectedName("AUTO-HU-2: Monte Carlo Sampling"));
        assertTrue(OperationKind.NASH.acceptsExpectedName(
                "A".repeat(100) + ": Monte Carlo Sampling"));
        assertFalse(OperationKind.NASH.acceptsExpectedName(": Monte Carlo Sampling"));
        assertFalse(OperationKind.NASH.acceptsExpectedName("bad name: Monte Carlo Sampling"));
        assertFalse(OperationKind.NASH.acceptsExpectedName(
                "A".repeat(101) + ": Monte Carlo Sampling"));
        assertFalse(OperationKind.NASH.acceptsExpectedName("HU-2.: Monte Carlo Sampling"));
        assertFalse(OperationKind.NASH.acceptsExpectedName("HU-2.HRCV: Monte Carlo Sampling"));
        assertFalse(OperationKind.NASH.acceptsExpectedName("CON: Monte Carlo Sampling"));
        assertFalse(OperationKind.NASH.acceptsExpectedName("com1.batch: Monte Carlo Sampling"));
        assertTrue(OperationKind.VIEWER_SAVE.acceptsExpectedName("Saving hand to: stage-1.hrcv"));
        assertFalse(OperationKind.VIEWER_SAVE.acceptsExpectedName("Saving hand to: stage-1.HRCV"));
        assertFalse(OperationKind.VIEWER_SAVE.acceptsExpectedName("Saving hand to: folder/stage-1.hrcv"));
        assertFalse(OperationKind.VIEWER_SAVE.acceptsExpectedName("Saving hand to: CON.run.hrcv"));
        assertFalse(OperationKind.VIEWER_SAVE.acceptsExpectedName("Saving hand to: stage..hrcv"));
        assertFalse(OperationKind.VIEWER_SAVE.acceptsExpectedName("Saving hand to: stage.hrcv.hrcv"));
        assertTrue(OperationKind.EXPORT.acceptsExpectedName("Exporting ranges to stage-1.zip"));
        assertFalse(OperationKind.EXPORT.acceptsExpectedName("Exporting ranges to .zip"));
        assertFalse(OperationKind.EXPORT.acceptsExpectedName("Exporting ranges to stage-1.txt"));
        assertFalse(OperationKind.EXPORT.acceptsExpectedName("Exporting ranges to stage.zip.zip"));
        assertFalse(OperationKind.EXPORT.acceptsExpectedName("Exporting ranges to bad\n.zip"));
    }

    private static void validatesLifecycleInput() {
        JobDescriptor descriptor = nashDescriptor("A: Monte Carlo Sampling");
        assertThrows(NullPointerException.class,
                () -> LifecycleInput.scheduled(null, descriptor, UTC, 1));
        assertThrows(NullPointerException.class,
                () -> LifecycleInput.scheduled(new Object(), null, UTC, 1));
        assertThrows(NullPointerException.class,
                () -> LifecycleInput.scheduled(new Object(), descriptor, null, 1));
        LifecycleInput missingStatus =
                LifecycleInput.done(new Object(), descriptor, null, UTC, 1);
        assertEquals(LifecycleInput.Kind.DONE, missingStatus.kind());
        assertEquals(null, missingStatus.status());
    }

    private static void faultsOnMissingStatusAndInfrastructureFailure() {
        Fixture missing = scheduledFixture("MISSING-STATUS");
        missing.coordinator.accept(LifecycleInput.done(
                missing.lastIdentity,
                nashDescriptor(nashName("MISSING-STATUS")),
                null,
                UTC.plusNanos(102),
                102));
        assertEquals(FaultReason.MISSING_TERMINAL_STATUS,
                missing.coordinator.faultReason());
        assertEquals(0, eventsOf(missing.events(), JobTerminalEvent.class).size());

        for (InfrastructureFailure failure : InfrastructureFailure.values()) {
            Fixture fixture = fixture(100);
            fixture.coordinator.failInfrastructure(failure, UTC, 101);
            assertEquals(switch (failure) {
                case CALLBACK_CAPTURE_FAILED -> FaultReason.CALLBACK_CAPTURE_FAILED;
                case CALLBACK_QUEUE_OVERFLOW -> FaultReason.CALLBACK_QUEUE_OVERFLOW;
                case CALLBACK_DISPATCH_FAILED -> FaultReason.CALLBACK_DISPATCH_FAILED;
            }, fixture.coordinator.faultReason());
            assertEquals(ArmOutcome.FAULTED,
                    fixture.coordinator.arm(
                            uuid(99), OperationKind.NASH, nashName("BLOCKED"), 10));
        }
    }

    private static void rejectsSourceMismatchWithOrderingPrecedence() {
        Fixture mismatch = fixture(100);
        mismatch.coordinator.arm(uuid(70), OperationKind.NASH, nashName("A"), 50);
        mismatch.coordinator.rejectSourceMismatch(UTC, 101);
        assertEquals(FaultReason.JOB_MISMATCH, mismatch.coordinator.faultReason());

        Fixture beforeArm = fixture(100);
        beforeArm.coordinator.arm(uuid(71), OperationKind.NASH, nashName("A"), 50);
        beforeArm.coordinator.rejectSourceMismatch(UTC, 99);
        assertEquals(FaultReason.EVENT_BEFORE_ARM, beforeArm.coordinator.faultReason());

        Fixture expired = fixture(100);
        expired.coordinator.arm(uuid(72), OperationKind.NASH, nashName("A"), 5);
        expired.coordinator.rejectSourceMismatch(UTC, 105);
        assertEquals(FaultReason.ARM_DEADLINE_EXPIRED, expired.coordinator.faultReason());
    }

    private static void acceptsAndRetriesArmWithoutChangingDeadline() {
        Fixture fixture = fixture(100);
        UUID request = uuid(10);
        assertEquals(ArmOutcome.ACCEPTED,
                fixture.coordinator.arm(request, OperationKind.NASH, nashName("A"), 50));
        ArmAcceptedEvent first = cast(fixture.events().get(0), ArmAcceptedEvent.class);
        fixture.nano.set(120);
        assertEquals(ArmOutcome.IDEMPOTENT,
                fixture.coordinator.arm(request, OperationKind.NASH, nashName("A"), 50));
        assertEquals(1, fixture.events().size());
        assertEquals(150L, first.deadlineNanos());
        fixture.nano.set(150);
        assertTrue(fixture.coordinator.expire());
        assertEquals(FaultReason.ARM_DEADLINE_EXPIRED, fixture.coordinator.faultReason());
    }

    private static void rejectsInvalidBusyAndExpiredArms() {
        Fixture fixture = fixture(10);
        assertEquals(ArmOutcome.REJECTED,
                fixture.coordinator.arm(null, OperationKind.NASH, nashName("A"), 5));
        assertEquals(ArmOutcome.REJECTED,
                fixture.coordinator.arm(uuid(1), OperationKind.NASH, "bad", 5));
        assertEquals(ArmOutcome.REJECTED,
                fixture.coordinator.arm(uuid(1), OperationKind.NASH, nashName("A"), 0));
        assertEquals(ArmOutcome.ACCEPTED,
                fixture.coordinator.arm(uuid(1), OperationKind.NASH, nashName("A"), 5));
        assertEquals(ArmOutcome.BUSY,
                fixture.coordinator.arm(uuid(2), OperationKind.NASH, nashName("B"), 5));
        assertFalse(fixture.coordinator.isFaulted());
        fixture.nano.set(15);
        assertTrue(fixture.coordinator.expire());
        assertEquals(ArmOutcome.FAULTED,
                fixture.coordinator.arm(uuid(2), OperationKind.NASH, nashName("B"), 5));
    }

    private static void faultsOnConflictingRequestReuse() {
        Fixture fixture = fixture(1);
        UUID request = uuid(3);
        fixture.coordinator.arm(request, OperationKind.NASH, nashName("A"), 20);
        assertEquals(ArmOutcome.FAULTED,
                fixture.coordinator.arm(request, OperationKind.NASH, nashName("B"), 20));
        assertEquals(FaultReason.REQUEST_ID_REUSED, fixture.coordinator.faultReason());

        Fixture timeout = fixture(1);
        UUID timeoutRequest = uuid(4);
        timeout.coordinator.arm(
                timeoutRequest, OperationKind.NASH, nashName("A"), 20);
        assertEquals(ArmOutcome.FAULTED,
                timeout.coordinator.arm(
                        timeoutRequest, OperationKind.NASH, nashName("A"), 21));
        assertEquals(FaultReason.REQUEST_ID_REUSED,
                timeout.coordinator.faultReason());
    }

    private static void rejectsArmBeforeJobCapacityIsExceeded() {
        AtomicLong clock = new AtomicLong(100);
        ObserverCoordinator coordinator = new ObserverCoordinator(
                SESSION, profiles(), 2, 1, 16, clock::get, () -> UTC);
        Object first = new Object();
        coordinator.arm(uuid(1), OperationKind.NASH, nashName("A"), 100);
        coordinator.accept(scheduled(first, nashDescriptor(nashName("A")), 101));
        coordinator.accept(running(first, nashDescriptor(nashName("A")), 102));
        coordinator.accept(done(first, nashDescriptor(nashName("A")),
                StatusSnapshot.capture(StatusSnapshot.OK, true, 0, BUNDLE), 103));
        int acceptedBefore = eventsOf(events(coordinator), ArmAcceptedEvent.class).size();
        assertEquals(ArmOutcome.FAULTED,
                coordinator.arm(uuid(2), OperationKind.NASH, nashName("B"), 100));
        assertEquals(FaultReason.JOB_CAPACITY_EXCEEDED, coordinator.faultReason());
        assertEquals(acceptedBefore,
                eventsOf(events(coordinator), ArmAcceptedEvent.class).size());
    }

    private static void filtersExactSourceAndName() {
        Fixture fixture = fixture(100);
        fixture.coordinator.arm(uuid(1), OperationKind.NASH, nashName("A"), 100);
        fixture.coordinator.accept(scheduled(new Object(),
                new JobDescriptor("other.bundle", "1", "other.Job", "unrelated", true, false), 101));
        assertEquals(1, fixture.events().size());

        Fixture wrongBundle = fixture(100);
        wrongBundle.coordinator.arm(uuid(2), OperationKind.NASH, nashName("A"), 100);
        wrongBundle.coordinator.accept(scheduled(new Object(),
                new JobDescriptor("wrong.bundle", VERSION, NASH_CLASS, nashName("A"), true, false), 101));
        assertEquals(FaultReason.JOB_MISMATCH, wrongBundle.coordinator.faultReason());

        Fixture wrongName = fixture(100);
        wrongName.coordinator.arm(uuid(3), OperationKind.NASH, nashName("A"), 100);
        wrongName.coordinator.accept(scheduled(new Object(), nashDescriptor(nashName("B")), 101));
        assertEquals(FaultReason.JOB_MISMATCH, wrongName.coordinator.faultReason());

        Fixture rightNameWrongClass = fixture(100);
        rightNameWrongClass.coordinator.arm(uuid(4), OperationKind.NASH, nashName("A"), 100);
        rightNameWrongClass.coordinator.accept(scheduled(new Object(),
                new JobDescriptor(BUNDLE, VERSION, "other.Job", nashName("A"), true, false), 101));
        assertEquals(FaultReason.JOB_MISMATCH, rightNameWrongClass.coordinator.faultReason());
    }

    private static void correlatesByReferenceIdentity() {
        Fixture fixture = fixture(100);
        EqualIdentity first = new EqualIdentity();
        EqualIdentity second = new EqualIdentity();
        fixture.coordinator.arm(uuid(1), OperationKind.NASH, nashName("A"), 100);
        fixture.coordinator.accept(scheduled(first, nashDescriptor(nashName("A")), 101));
        fixture.coordinator.accept(running(second, nashDescriptor(nashName("A")), 102));
        assertEquals(FaultReason.LIFECYCLE_BEFORE_SCHEDULED, fixture.coordinator.faultReason());
    }

    private static void correlatesAllOperationProfiles() {
        record Case(OperationKind operation, String name, String className) {
        }
        for (Case value : List.of(
                new Case(OperationKind.NASH, nashName("A"), NASH_CLASS),
                new Case(OperationKind.VIEWER_SAVE,
                        "Saving hand to: stage-1.hrcv", VIEWER_CLASS),
                new Case(OperationKind.EXPORT,
                        "Exporting ranges to stage-1.zip", EXPORT_CLASS))) {
            Fixture fixture = fixture(100);
            Object identity = new Object();
            JobDescriptor descriptor = new JobDescriptor(
                    BUNDLE, VERSION, value.className(), value.name(), true, false);
            assertEquals(ArmOutcome.ACCEPTED,
                    fixture.coordinator.arm(uuid(1), value.operation(), value.name(), 100));
            fixture.coordinator.accept(scheduled(identity, descriptor, 101));
            fixture.coordinator.accept(running(identity, descriptor, 102));
            fixture.coordinator.accept(done(identity, descriptor,
                    StatusSnapshot.capture(StatusSnapshot.OK, true, 0, BUNDLE), 103));
            JobTerminalEvent terminal = lastEvent(fixture.events(), JobTerminalEvent.class);
            assertEquals(value.operation(), terminal.operation());
            assertEquals(TerminalResult.OK, terminal.result());
        }
    }

    private static void correlatesSameNameNashJobsSequentially() {
        Fixture fixture = fixture(100);
        String name = nashName("A");
        Object first = new Object();
        Object second = new Object();
        fixture.coordinator.arm(uuid(1), OperationKind.NASH, name, 100);
        fixture.coordinator.accept(scheduled(first, nashDescriptor(name), 101));
        fixture.coordinator.arm(uuid(2), OperationKind.NASH, name, 100);
        fixture.coordinator.accept(scheduled(second, nashDescriptor(name), 102));
        fixture.coordinator.accept(running(first, nashDescriptor(name), 103));
        fixture.coordinator.accept(running(second, nashDescriptor(name), 104));
        List<JobScheduledEvent> scheduled = eventsOf(fixture.events(), JobScheduledEvent.class);
        assertEquals(2, scheduled.size());
        assertEquals(1L, scheduled.get(0).jobId());
        assertEquals(2L, scheduled.get(1).jobId());
        assertEquals(uuid(1), scheduled.get(0).requestId());
        assertEquals(uuid(2), scheduled.get(1).requestId());
    }

    private static void recordsOkCancelAndError() {
        Fixture ok = scheduledFixture("OK");
        Object okIdentity = ok.lastIdentity;
        ok.coordinator.accept(running(okIdentity, nashDescriptor(nashName("OK")), 102));
        ok.coordinator.accept(done(okIdentity, nashDescriptor(nashName("OK")),
                StatusSnapshot.capture(StatusSnapshot.OK, true, 0, "org.eclipse.core.runtime"), 103));
        JobTerminalEvent okEvent = lastEvent(ok.events(), JobTerminalEvent.class);
        assertEquals(TerminalResult.OK, okEvent.result());

        Fixture cancel = scheduledFixture("CANCEL");
        cancel.coordinator.accept(done(cancel.lastIdentity, nashDescriptor(nashName("CANCEL")),
                StatusSnapshot.capture(StatusSnapshot.CANCEL, false, 1, "org.eclipse.core.runtime"), 102));
        JobTerminalEvent cancelEvent = lastEvent(cancel.events(), JobTerminalEvent.class);
        assertEquals(TerminalResult.CANCEL, cancelEvent.result());
        assertFalse(cancelEvent.runningSeen());
        assertFalse(cancel.coordinator.isFaulted());

        Fixture error = scheduledFixture("ERROR");
        error.coordinator.accept(running(error.lastIdentity, nashDescriptor(nashName("ERROR")), 102));
        error.coordinator.accept(done(error.lastIdentity, nashDescriptor(nashName("ERROR")),
                StatusSnapshot.capture(StatusSnapshot.ERROR, false, 2, "net.holdemresources.calculator"), 103));
        assertEquals(TerminalResult.ERROR,
                lastEvent(error.events(), JobTerminalEvent.class).result());
    }

    private static void faultsOnInvalidLifecycleAndDescriptorChanges() {
        Fixture before = fixture(100);
        before.coordinator.accept(running(new Object(), nashDescriptor(nashName("A")), 101));
        assertEquals(FaultReason.LIFECYCLE_BEFORE_SCHEDULED, before.coordinator.faultReason());

        Fixture duplicate = scheduledFixture("A");
        duplicate.coordinator.accept(running(duplicate.lastIdentity, nashDescriptor(nashName("A")), 102));
        duplicate.coordinator.accept(running(duplicate.lastIdentity, nashDescriptor(nashName("A")), 103));
        assertEquals(FaultReason.DUPLICATE_RUNNING, duplicate.coordinator.faultReason());

        Fixture mutation = scheduledFixture("A");
        mutation.coordinator.accept(running(mutation.lastIdentity,
                new JobDescriptor(BUNDLE, VERSION, NASH_CLASS, nashName("A"), false, false), 102));
        assertEquals(FaultReason.JOB_DESCRIPTOR_CHANGED, mutation.coordinator.faultReason());

        Fixture okWithoutRunning = scheduledFixture("A");
        okWithoutRunning.coordinator.accept(done(okWithoutRunning.lastIdentity,
                nashDescriptor(nashName("A")),
                StatusSnapshot.capture(StatusSnapshot.OK, true, 0, BUNDLE), 102));
        assertEquals(FaultReason.DONE_BEFORE_RUNNING, okWithoutRunning.coordinator.faultReason());
        assertEquals(0, eventsOf(okWithoutRunning.events(), JobTerminalEvent.class).size());
        JobTerminalRejectedEvent rejectedOk =
                lastEvent(okWithoutRunning.events(), JobTerminalRejectedEvent.class);
        assertEquals(FaultReason.DONE_BEFORE_RUNNING, rejectedOk.rejectionReason());
    }

    private static void emitsUnknownTerminalBeforeFault() {
        int[] invalidSeverities = {StatusSnapshot.INFO, StatusSnapshot.WARNING, 16};
        for (int severity : invalidSeverities) {
            Fixture fixture = scheduledFixture("A" + severity);
            fixture.coordinator.accept(running(fixture.lastIdentity,
                    nashDescriptor(nashName("A" + severity)), 102));
            fixture.coordinator.accept(done(fixture.lastIdentity,
                    nashDescriptor(nashName("A" + severity)),
                    StatusSnapshot.capture(severity, false, 7, "plugin"), 103));
            List<ObserverEvent> events = fixture.events();
            JobTerminalRejectedEvent terminal =
                    cast(events.get(events.size() - 2), JobTerminalRejectedEvent.class);
            assertEquals(TerminalResult.UNKNOWN, terminal.observedResult());
            assertEquals(FaultReason.UNKNOWN_TERMINAL_STATUS, terminal.rejectionReason());
            assertEquals(FaultReason.UNKNOWN_TERMINAL_STATUS, fixture.coordinator.faultReason());
        }

        Fixture contradictory = scheduledFixture("X");
        contradictory.coordinator.accept(running(contradictory.lastIdentity,
                nashDescriptor(nashName("X")), 102));
        contradictory.coordinator.accept(done(contradictory.lastIdentity,
                nashDescriptor(nashName("X")),
                StatusSnapshot.capture(StatusSnapshot.OK, false, 0, "plugin"), 103));
        assertEquals(FaultReason.UNKNOWN_TERMINAL_STATUS, contradictory.coordinator.faultReason());

        Fixture combined = scheduledFixture("COMBINED");
        combined.coordinator.accept(running(combined.lastIdentity,
                nashDescriptor(nashName("COMBINED")), 102));
        combined.coordinator.accept(done(combined.lastIdentity,
                nashDescriptor(nashName("COMBINED")),
                StatusSnapshot.capture(StatusSnapshot.WARNING, false, 0, "bad\nplugin"), 103));
        JobTerminalRejectedEvent combinedEvent =
                lastEvent(combined.events(), JobTerminalRejectedEvent.class);
        assertEquals(TerminalResult.UNKNOWN, combinedEvent.observedResult());
        assertTrue(combinedEvent.statusPluginOmitted());
        assertEquals(FaultReason.UNKNOWN_TERMINAL_STATUS, combinedEvent.rejectionReason());
    }

    private static void faultsOnDuplicateScheduleAndDone() {
        Fixture duplicateSchedule = fixture(100);
        Object identity = new Object();
        duplicateSchedule.coordinator.arm(uuid(1), OperationKind.NASH, nashName("A"), 100);
        LifecycleInput scheduled = scheduled(identity, nashDescriptor(nashName("A")), 101);
        duplicateSchedule.coordinator.accept(scheduled);
        duplicateSchedule.coordinator.accept(scheduled);
        assertEquals(FaultReason.DUPLICATE_SCHEDULED,
                duplicateSchedule.coordinator.faultReason());

        Fixture duplicateDone = scheduledFixture("B");
        duplicateDone.coordinator.accept(running(duplicateDone.lastIdentity,
                nashDescriptor(nashName("B")), 102));
        LifecycleInput done = done(duplicateDone.lastIdentity,
                nashDescriptor(nashName("B")),
                StatusSnapshot.capture(StatusSnapshot.OK, true, 0, BUNDLE), 103);
        duplicateDone.coordinator.accept(done);
        duplicateDone.coordinator.accept(done);
        assertEquals(FaultReason.DUPLICATE_DONE, duplicateDone.coordinator.faultReason());
    }

    private static void recordsTrackedDoneAfterFault() {
        Fixture fixture = scheduledFixture("A");
        Object first = fixture.lastIdentity;
        fixture.coordinator.accept(running(first, nashDescriptor(nashName("A")), 102));
        fixture.coordinator.accept(scheduled(new Object(), nashDescriptor(nashName("UNARMED")), 103));
        assertEquals(FaultReason.UNEXPECTED_RELEVANT_JOB, fixture.coordinator.faultReason());
        fixture.coordinator.accept(done(first, nashDescriptor(nashName("A")),
                StatusSnapshot.capture(StatusSnapshot.ERROR, false, 5, BUNDLE), 104));
        JobTerminalRejectedEvent terminal =
                lastEvent(fixture.events(), JobTerminalRejectedEvent.class);
        assertEquals(TerminalResult.ERROR, terminal.observedResult());
        assertEquals(FaultReason.TERMINAL_EVENT_REJECTED, terminal.rejectionReason());
        assertEquals(ArmOutcome.FAULTED,
                fixture.coordinator.arm(uuid(9), OperationKind.NASH, nashName("B"), 10));
    }

    private static void usesCallbackTimeForArmDeadline() {
        Fixture valid = fixture(100);
        Object identity = new Object();
        valid.coordinator.arm(uuid(1), OperationKind.NASH, nashName("A"), 10);
        valid.nano.set(1_000);
        valid.coordinator.accept(scheduled(identity, nashDescriptor(nashName("A")), 105));
        assertFalse(valid.coordinator.isFaulted());

        Fixture expired = fixture(100);
        expired.coordinator.arm(uuid(1), OperationKind.NASH, nashName("A"), 10);
        expired.coordinator.accept(scheduled(new Object(), nashDescriptor(nashName("A")), 110));
        assertEquals(FaultReason.ARM_DEADLINE_EXPIRED, expired.coordinator.faultReason());

        Fixture beforeArm = fixture(100);
        beforeArm.coordinator.arm(uuid(1), OperationKind.NASH, nashName("A"), 10);
        beforeArm.coordinator.accept(scheduled(new Object(), nashDescriptor(nashName("A")), 99));
        assertEquals(FaultReason.EVENT_BEFORE_ARM, beforeArm.coordinator.faultReason());
    }

    private static void recordsTrackedRunningWhenAnotherArmExpires() {
        Fixture fixture = fixture(100);
        String firstName = nashName("A");
        Object first = new Object();
        fixture.coordinator.arm(uuid(1), OperationKind.NASH, firstName, 100);
        fixture.coordinator.accept(scheduled(first, nashDescriptor(firstName), 101));
        fixture.nano.set(110);
        fixture.coordinator.arm(uuid(2), OperationKind.NASH, nashName("B"), 10);
        fixture.coordinator.accept(running(first, nashDescriptor(firstName), 120));
        assertEquals(FaultReason.ARM_DEADLINE_EXPIRED, fixture.coordinator.faultReason());
        assertEquals(0, eventsOf(fixture.events(), JobRunningEvent.class).size());
        assertEquals(1, eventsOf(fixture.events(), JobRunningRejectedEvent.class).size());
        fixture.coordinator.accept(done(first, nashDescriptor(firstName),
                StatusSnapshot.capture(StatusSnapshot.OK, true, 0, BUNDLE), 121));
        JobTerminalRejectedEvent terminal =
                lastEvent(fixture.events(), JobTerminalRejectedEvent.class);
        assertTrue(terminal.runningSeen());
        assertEquals(FaultReason.TERMINAL_EVENT_REJECTED, terminal.rejectionReason());
    }

    private static void rejectsTrackedRunningAfterFault() {
        Fixture fixture = scheduledFixture("POST-FAULT-RUNNING");
        fixture.coordinator.accept(scheduled(
                new Object(), nashDescriptor(nashName("UNARMED")), 102));
        assertEquals(FaultReason.UNEXPECTED_RELEVANT_JOB, fixture.coordinator.faultReason());
        fixture.coordinator.accept(running(
                fixture.lastIdentity,
                nashDescriptor(nashName("POST-FAULT-RUNNING")),
                103));
        assertEquals(0, eventsOf(fixture.events(), JobRunningEvent.class).size());
        JobRunningRejectedEvent rejected =
                lastEvent(fixture.events(), JobRunningRejectedEvent.class);
        assertEquals(FaultReason.TERMINAL_EVENT_REJECTED, rejected.rejectionReason());
    }

    private static void faultsOnCallbackTimeRegression() {
        Fixture runningRegression = scheduledFixture("RUNNING-REGRESSION");
        runningRegression.coordinator.accept(running(
                runningRegression.lastIdentity,
                nashDescriptor(nashName("RUNNING-REGRESSION")),
                100));
        assertEquals(FaultReason.CALLBACK_TIME_REGRESSED,
                runningRegression.coordinator.faultReason());
        assertEquals(0, eventsOf(runningRegression.events(), JobRunningEvent.class).size());

        Fixture doneRegression = scheduledFixture("DONE-REGRESSION");
        doneRegression.coordinator.accept(running(
                doneRegression.lastIdentity,
                nashDescriptor(nashName("DONE-REGRESSION")),
                103));
        doneRegression.coordinator.accept(done(
                doneRegression.lastIdentity,
                nashDescriptor(nashName("DONE-REGRESSION")),
                StatusSnapshot.capture(StatusSnapshot.OK, true, 0, BUNDLE),
                102));
        assertEquals(FaultReason.CALLBACK_TIME_REGRESSED,
                doneRegression.coordinator.faultReason());
        assertEquals(0, eventsOf(doneRegression.events(), JobTerminalEvent.class).size());

        long scheduledNanos = Long.MAX_VALUE - 2;
        Fixture wrapForward = fixture(Long.MAX_VALUE - 5);
        Object identity = new Object();
        wrapForward.coordinator.arm(
                uuid(3), OperationKind.NASH, nashName("WRAP-CALLBACK"), 20);
        wrapForward.coordinator.accept(scheduled(
                identity,
                nashDescriptor(nashName("WRAP-CALLBACK")),
                scheduledNanos));
        wrapForward.coordinator.accept(running(
                identity,
                nashDescriptor(nashName("WRAP-CALLBACK")),
                Long.MIN_VALUE + 2));
        assertFalse(wrapForward.coordinator.isFaulted());
        assertEquals(1, eventsOf(wrapForward.events(), JobRunningEvent.class).size());
    }

    private static void recordsPostFaultUnknownTerminalExactly() {
        Fixture fixture = scheduledFixture("A");
        Object first = fixture.lastIdentity;
        fixture.coordinator.accept(running(first, nashDescriptor(nashName("A")), 102));
        fixture.coordinator.accept(scheduled(new Object(), nashDescriptor(nashName("UNARMED")), 103));
        fixture.coordinator.accept(done(first, nashDescriptor(nashName("A")),
                StatusSnapshot.capture(StatusSnapshot.WARNING, false, 9, BUNDLE), 104));
        JobTerminalRejectedEvent terminal =
                lastEvent(fixture.events(), JobTerminalRejectedEvent.class);
        assertEquals(TerminalResult.UNKNOWN, terminal.observedResult());
        assertEquals(FaultReason.UNKNOWN_TERMINAL_STATUS, terminal.rejectionReason());
        assertEquals(FaultReason.UNEXPECTED_RELEVANT_JOB, fixture.coordinator.faultReason());
    }

    private static void handlesNanoTimeWrap() {
        Fixture fixture = fixture(Long.MAX_VALUE - 5);
        fixture.coordinator.arm(uuid(1), OperationKind.NASH, nashName("A"), 10);
        fixture.coordinator.accept(scheduled(new Object(), nashDescriptor(nashName("A")),
                Long.MIN_VALUE + 2));
        assertFalse(fixture.coordinator.isFaulted());

        Fixture expired = fixture(Long.MAX_VALUE - 5);
        expired.coordinator.arm(uuid(1), OperationKind.NASH, nashName("A"), 10);
        expired.nano.set(Long.MIN_VALUE + 5);
        assertTrue(expired.coordinator.expire());
    }

    private static void replayBufferBoundariesAndImmutability() {
        ReplayBuffer buffer = new ReplayBuffer(2);
        appendFault(buffer, FaultReason.REQUEST_ID_REUSED);
        appendFault(buffer, FaultReason.JOB_MISMATCH);
        appendFault(buffer, FaultReason.DUPLICATE_DONE);
        ReplayQuery allAvailable = buffer.replayAfter(1);
        assertEquals(ReplayQuery.Disposition.OK, allAvailable.disposition());
        assertEquals(List.of(2L, 3L), sequences(allAvailable.events()));
        assertEquals(ReplayQuery.Disposition.GAP, buffer.replayAfter(0).disposition());
        assertEquals(ReplayQuery.Disposition.CURSOR_AHEAD, buffer.replayAfter(4).disposition());
        assertThrows(UnsupportedOperationException.class,
                () -> allAvailable.events().add(allAvailable.events().get(0)));

        ReplayBuffer empty = new ReplayBuffer(1);
        ReplayQuery emptyQuery = empty.replayAfter(0);
        assertEquals(ReplayQuery.Disposition.OK, emptyQuery.disposition());
        assertEquals(1L, emptyQuery.oldestAvailable());
        assertEquals(0L, emptyQuery.latestAvailable());
        assertThrows(IllegalArgumentException.class, () -> empty.replayAfter(-1));

        ReplayBuffer exhausted = new ReplayBuffer(1, Long.MAX_VALUE);
        assertThrows(IllegalStateException.class,
                () -> appendFault(exhausted, FaultReason.DUPLICATE_DONE));
    }

    private static void replayBufferIsThreadSafe() throws Exception {
        ReplayBuffer buffer = new ReplayBuffer(64);
        CountDownLatch start = new CountDownLatch(1);
        AtomicReference<Throwable> failure = new AtomicReference<>();
        Thread writer = new Thread(() -> {
            try {
                start.await();
                for (int i = 0; i < 50; i++) {
                    appendFault(buffer, FaultReason.REQUEST_ID_REUSED);
                }
            } catch (Throwable thrown) {
                failure.set(thrown);
            }
        });
        Thread reader = new Thread(() -> {
            try {
                start.await();
                for (int i = 0; i < 50; i++) {
                    ReplayQuery query = buffer.replayAfter(0);
                    if (query.disposition() == ReplayQuery.Disposition.OK) {
                        long previous = 0;
                        for (ObserverEvent event : query.events()) {
                            assertTrue(event.sequence() > previous);
                            previous = event.sequence();
                        }
                    }
                }
            } catch (Throwable thrown) {
                failure.set(thrown);
            }
        });
        writer.start();
        reader.start();
        start.countDown();
        writer.join(5_000);
        reader.join(5_000);
        assertFalse(writer.isAlive());
        assertFalse(reader.isAlive());
        if (failure.get() != null) {
            throw new AssertionError("thread failure", failure.get());
        }
        assertEquals(50L, buffer.replayAfter(50).latestAvailable());
    }

    private static void replayBufferFactoryFailureIsTransactional() {
        ReplayBuffer buffer = new ReplayBuffer(3);
        appendFault(buffer, FaultReason.REQUEST_ID_REUSED);
        assertThrows(IllegalStateException.class,
                () -> buffer.append(sequence -> {
                    throw new IllegalStateException("synthetic factory failure");
                }));
        ObserverEvent second = appendFault(buffer, FaultReason.JOB_MISMATCH);
        assertEquals(2L, second.sequence());
        ReplayQuery query = buffer.replayAfter(0);
        assertEquals(ReplayQuery.Disposition.OK, query.disposition());
        assertEquals(List.of(1L, 2L), sequences(query.events()));
        assertEquals(2L, query.latestAvailable());
    }

    private static void eventsExcludeRawIdentityAndSensitiveStatusText() {
        for (Class<?> eventType : List.of(
                ArmAcceptedEvent.class,
                JobScheduledEvent.class,
                JobRunningEvent.class,
                JobRunningRejectedEvent.class,
                JobTerminalEvent.class,
                JobTerminalRejectedEvent.class,
                ObserverFaultEvent.class)) {
            for (Method accessor : eventType.getDeclaredMethods()) {
                assertFalse(accessor.getReturnType() == Object.class);
                assertFalse(Throwable.class.isAssignableFrom(accessor.getReturnType()));
            }
        }
        for (Method accessor : StatusSnapshot.class.getDeclaredMethods()) {
            assertFalse(accessor.getName().toLowerCase().contains("message"));
            assertFalse(accessor.getName().toLowerCase().contains("stack"));
        }
        Fixture fixture = scheduledFixture("A");
        fixture.coordinator.accept(running(fixture.lastIdentity, nashDescriptor(nashName("A")), 102));
        fixture.coordinator.accept(done(fixture.lastIdentity, nashDescriptor(nashName("A")),
                StatusSnapshot.capture(StatusSnapshot.CANCEL, false, 1, "bad\nsecret"), 102));
        JobTerminalRejectedEvent event =
                lastEvent(fixture.events(), JobTerminalRejectedEvent.class);
        assertEquals("", event.statusPlugin());
        assertTrue(event.statusPluginOmitted());
        assertEquals(FaultReason.STATUS_PLUGIN_OMITTED, event.rejectionReason());
    }

    private static Fixture scheduledFixture(String hand) {
        Fixture fixture = fixture(100);
        Object identity = new Object();
        fixture.lastIdentity = identity;
        fixture.coordinator.arm(uuid(1), OperationKind.NASH, nashName(hand), 100);
        fixture.coordinator.accept(scheduled(identity, nashDescriptor(nashName(hand)), 101));
        return fixture;
    }

    private static void checkpointsReplayAndExpiryAtomically() {
        Fixture fixture = fixture(100);
        assertEquals(SESSION, fixture.coordinator.sessionId());
        assertEquals(128, fixture.coordinator.replayCapacity());
        assertEquals(ArmOutcome.ACCEPTED, fixture.coordinator.arm(
                uuid(30), OperationKind.NASH, nashName("CHECKPOINT"), 10));

        fixture.nano.set(111);
        ObserverCoreSnapshot snapshot = fixture.coordinator.checkpoint(0);

        assertEquals(FaultReason.ARM_DEADLINE_EXPIRED, snapshot.faultReason());
        assertEquals(ReplayQuery.Disposition.OK, snapshot.replay().disposition());
        assertEquals(2, snapshot.replay().events().size());
        assertTrue(snapshot.replay().events().get(0) instanceof ArmAcceptedEvent);
        assertTrue(snapshot.replay().events().get(1) instanceof ObserverFaultEvent);
        assertEquals(FaultReason.ARM_DEADLINE_EXPIRED,
                ((ObserverFaultEvent) snapshot.replay().events().get(1)).reason());
    }

    private static void rejectsInvalidCheckpointWithoutMutation() {
        Fixture fixture = fixture(100);
        assertEquals(ArmOutcome.ACCEPTED, fixture.coordinator.arm(
                uuid(31), OperationKind.NASH, nashName("INVALID-CURSOR"), 10));
        fixture.nano.set(111);

        assertThrows(IllegalArgumentException.class,
                () -> fixture.coordinator.checkpoint(-1));

        assertFalse(fixture.coordinator.isFaulted());
        assertEquals(1, fixture.events().size());
        assertTrue(fixture.events().get(0) instanceof ArmAcceptedEvent);
    }

    private static void serializesCheckpointAgainstLifecycleInput()
            throws Exception {
        AtomicLong clock = new AtomicLong(100);
        AtomicBoolean blockClock = new AtomicBoolean();
        CountDownLatch clockEntered = new CountDownLatch(1);
        CountDownLatch releaseClock = new CountDownLatch(1);
        ObserverCoordinator coordinator = new ObserverCoordinator(
                SESSION,
                profiles(),
                32,
                32,
                128,
                () -> {
                    if (blockClock.compareAndSet(true, false)) {
                        clockEntered.countDown();
                        awaitLatch(releaseClock, "checkpoint clock release");
                    }
                    return clock.get();
                },
                () -> UTC);
        assertEquals(ArmOutcome.ACCEPTED, coordinator.arm(
                uuid(32), OperationKind.NASH, nashName("SERIALIZED"), 100));
        Object identity = new Object();
        AtomicReference<ObserverCoreSnapshot> snapshot = new AtomicReference<>();
        AtomicReference<Throwable> failure = new AtomicReference<>();
        CountDownLatch callbackStarted = new CountDownLatch(1);
        CountDownLatch callbackFinished = new CountDownLatch(1);
        blockClock.set(true);

        Thread checkpointThread = new Thread(() -> {
            try {
                snapshot.set(coordinator.checkpoint(0));
            } catch (Throwable thrown) {
                failure.compareAndSet(null, thrown);
            }
        }, "observer-checkpoint-test");
        checkpointThread.start();
        assertTrue(clockEntered.await(2, TimeUnit.SECONDS));

        Thread callbackThread = new Thread(() -> {
            callbackStarted.countDown();
            try {
                coordinator.accept(scheduled(
                        identity,
                        nashDescriptor(nashName("SERIALIZED")),
                        101));
            } catch (Throwable thrown) {
                failure.compareAndSet(null, thrown);
            } finally {
                callbackFinished.countDown();
            }
        }, "observer-callback-test");
        callbackThread.start();
        assertTrue(callbackStarted.await(2, TimeUnit.SECONDS));
        assertFalse(callbackFinished.await(100, TimeUnit.MILLISECONDS));

        releaseClock.countDown();
        checkpointThread.join(2_000);
        callbackThread.join(2_000);
        assertFalse(checkpointThread.isAlive());
        assertFalse(callbackThread.isAlive());
        if (failure.get() != null) {
            throw new AssertionError("concurrent checkpoint failed", failure.get());
        }

        assertEquals(1, snapshot.get().replay().events().size());
        assertTrue(snapshot.get().replay().events().get(0)
                instanceof ArmAcceptedEvent);
        assertEquals(2, coordinator.replayAfter(0).events().size());
        assertTrue(coordinator.replayAfter(0).events().get(1)
                instanceof JobScheduledEvent);
    }

    private static void awaitLatch(CountDownLatch latch, String description) {
        try {
            if (!latch.await(2, TimeUnit.SECONDS)) {
                throw new AssertionError("timed out waiting for " + description);
            }
        } catch (InterruptedException interrupted) {
            Thread.currentThread().interrupt();
            throw new AssertionError("interrupted waiting for " + description,
                    interrupted);
        }
    }

    private static Fixture fixture(long initialNanos) {
        AtomicLong clock = new AtomicLong(initialNanos);
        ObserverCoordinator coordinator = new ObserverCoordinator(
                SESSION,
                profiles(),
                32,
                32,
                128,
                clock::get,
                () -> UTC.plusNanos(Math.floorMod(clock.get(), 1_000_000_000L)));
        return new Fixture(coordinator, clock);
    }

    private static LifecycleInput scheduled(Object identity, JobDescriptor job, long nanos) {
        return LifecycleInput.scheduled(identity, job, UTC.plusNanos(Math.floorMod(nanos, 1_000_000_000L)), nanos);
    }

    private static LifecycleInput running(Object identity, JobDescriptor job, long nanos) {
        return LifecycleInput.running(identity, job, UTC.plusNanos(Math.floorMod(nanos, 1_000_000_000L)), nanos);
    }

    private static LifecycleInput done(
            Object identity, JobDescriptor job, StatusSnapshot status, long nanos) {
        return LifecycleInput.done(identity, job, status,
                UTC.plusNanos(Math.floorMod(nanos, 1_000_000_000L)), nanos);
    }

    private static JobDescriptor nashDescriptor(String name) {
        return new JobDescriptor(BUNDLE, VERSION, NASH_CLASS, name, true, false);
    }

    private static String nashName(String hand) {
        return hand + ": Monte Carlo Sampling";
    }

    private static UUID uuid(long value) {
        return new UUID(0, value);
    }

    private static List<OperationProfile> profiles() {
        return List.of(
                new OperationProfile(OperationKind.NASH, BUNDLE, VERSION, NASH_CLASS),
                new OperationProfile(OperationKind.VIEWER_SAVE, BUNDLE, VERSION, VIEWER_CLASS),
                new OperationProfile(OperationKind.EXPORT, BUNDLE, VERSION, EXPORT_CLASS));
    }

    private static List<ObserverEvent> events(ObserverCoordinator coordinator) {
        return coordinator.replayAfter(0).events();
    }

    private static <T> List<T> eventsOf(List<ObserverEvent> events, Class<T> type) {
        List<T> matches = new ArrayList<>();
        for (ObserverEvent event : events) {
            if (type.isInstance(event)) {
                matches.add(type.cast(event));
            }
        }
        return matches;
    }

    private static <T> T lastEvent(List<ObserverEvent> events, Class<T> type) {
        List<T> matches = eventsOf(events, type);
        if (matches.isEmpty()) {
            throw new AssertionError("missing event " + type.getSimpleName());
        }
        return matches.get(matches.size() - 1);
    }

    private static List<Long> sequences(List<ObserverEvent> events) {
        List<Long> values = new ArrayList<>();
        for (ObserverEvent event : events) {
            values.add(event.sequence());
        }
        return values;
    }

    private static ObserverEvent appendFault(ReplayBuffer buffer, FaultReason reason) {
        return buffer.append(sequence -> new ObserverFaultEvent(
                new EventMetadata(sequence, UTC, sequence, SESSION), reason));
    }

    private static <T> T cast(Object value, Class<T> type) {
        if (!type.isInstance(value)) {
            throw new AssertionError("expected " + type.getSimpleName() + " but was " + value);
        }
        return type.cast(value);
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
        if (!java.util.Objects.equals(expected, actual)) {
            throw new AssertionError("expected " + expected + " but was " + actual);
        }
    }

    private static void assertThrows(Class<? extends Throwable> expected, ThrowingRunnable body) {
        try {
            body.run();
        } catch (Throwable actual) {
            if (expected.isInstance(actual)) {
                return;
            }
            throw new AssertionError("expected " + expected.getSimpleName() + " but got " + actual, actual);
        }
        throw new AssertionError("expected " + expected.getSimpleName());
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

    private static final class Fixture {
        private final ObserverCoordinator coordinator;
        private final AtomicLong nano;
        private Object lastIdentity;

        private Fixture(ObserverCoordinator coordinator, AtomicLong nano) {
            this.coordinator = coordinator;
            this.nano = nano;
        }

        private List<ObserverEvent> events() {
            return ObserverCoreTest.events(coordinator);
        }
    }

    private static final class EqualIdentity {
        @Override
        public boolean equals(Object other) {
            return other instanceof EqualIdentity;
        }

        @Override
        public int hashCode() {
            return 1;
        }
    }
}
