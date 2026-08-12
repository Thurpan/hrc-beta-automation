package net.hrcautomation.jobobserver.startlevelfixture;

import java.util.List;
import java.util.Objects;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;

/**
 * System-package bridge between the isolated test harness and generated test
 * Bundles. It carries only synthetic state and contains no HRC data.
 */
public final class FixtureProbe {
    public static final String TARGET_JOB_NAME =
            "synthetic-immediate-observer-fixture-job";

    private static final List<String> EVENTS = new CopyOnWriteArrayList<>();
    private static final AtomicBoolean FAIL_OBSERVER_START = new AtomicBoolean();
    private static final AtomicBoolean LISTENER_REGISTERED = new AtomicBoolean();
    private static final AtomicInteger PUBLICATION_COUNT = new AtomicInteger();
    private static final AtomicInteger CONTROLLER_ACCEPTANCES = new AtomicInteger();
    private static final AtomicInteger CONTROLLER_REFUSALS = new AtomicInteger();
    private static final AtomicReference<Runnable> STALE_CALLBACK =
            new AtomicReference<>();
    private static volatile NoRuntimeUnloadPolicy policy =
            new NoRuntimeUnloadPolicy();

    private FixtureProbe() {
    }

    public static void reset(boolean failObserverStart) {
        EVENTS.clear();
        FAIL_OBSERVER_START.set(failObserverStart);
        LISTENER_REGISTERED.set(false);
        PUBLICATION_COUNT.set(0);
        CONTROLLER_ACCEPTANCES.set(0);
        CONTROLLER_REFUSALS.set(0);
        STALE_CALLBACK.set(null);
        policy = new NoRuntimeUnloadPolicy();
    }

    public static boolean shouldFailObserverStart() {
        return FAIL_OBSERVER_START.get();
    }

    public static void observerStarting(int bundleLevel, int frameworkLevel) {
        EVENTS.add("OBSERVER_START:L" + bundleLevel + ":F" + frameworkLevel);
        policy.beginStart();
        EVENTS.add("OBSERVER_POLICY_STARTING");
    }

    public static void providerState(
            String symbolicName, int state, int level, boolean persistentlyStarted) {
        EVENTS.add("PROVIDER:" + Objects.requireNonNull(symbolicName, "symbolicName")
                + ":S" + state + ":L" + level + ":P" + persistentlyStarted);
    }

    public static void recordedProviderRows() {
        EVENTS.add("ROWS:common:L2:Atrue:jobs:L4:Afalse"
                + ":runtime:L4:Atrue:observer:L4:Atrue:producer:L5:Atrue");
    }

    public static void providerStateAt(
            String phase,
            String symbolicName,
            int state,
            int level,
            boolean persistentlyStarted) {
        EVENTS.add(Objects.requireNonNull(phase, "phase") + ":"
                + Objects.requireNonNull(symbolicName, "symbolicName")
                + ":S" + state + ":L" + level + ":P" + persistentlyStarted);
    }

    public static void observerStartFailed() {
        policy.failStart();
        EVENTS.add("OBSERVER_START_FAILED");
    }

    public static void listenerRegistered() {
        if (!LISTENER_REGISTERED.compareAndSet(false, true)) {
            throw new IllegalStateException("Listener registered more than once");
        }
        EVENTS.add("LISTENER_REGISTERED");
    }

    public static void registerStaleCallback(Runnable callback) {
        if (!STALE_CALLBACK.compareAndSet(
                null, Objects.requireNonNull(callback, "callback"))) {
            throw new IllegalStateException("Stale callback already registered");
        }
    }

    public static void publicationActivated() {
        if (!LISTENER_REGISTERED.get()) {
            throw new IllegalStateException("Publication preceded listener registration");
        }
        policy.activatePublication();
        PUBLICATION_COUNT.incrementAndGet();
        EVENTS.add("PUBLICATION_ACTIVE");
    }

    public static void producerStarting(int bundleLevel, int frameworkLevel) {
        EVENTS.add("PRODUCER_START:L" + bundleLevel + ":F" + frameworkLevel);
    }

    public static boolean controllerAttempt() {
        boolean allowed = policy.controllerAllowed();
        if (allowed) {
            CONTROLLER_ACCEPTANCES.incrementAndGet();
            EVENTS.add("CONTROLLER_ACCEPTED");
        } else {
            CONTROLLER_REFUSALS.incrementAndGet();
            EVENTS.add("CONTROLLER_REFUSED");
        }
        return allowed;
    }

    public static void jobCallback(String kind, String jobName) {
        Objects.requireNonNull(kind, "kind");
        if (!TARGET_JOB_NAME.equals(jobName)) {
            return;
        }
        if (policy.tryAdmitCallback()) {
            EVENTS.add("JOB_" + kind);
        } else {
            EVENTS.add("CALLBACK_REJECTED:" + kind);
        }
    }

    public static void jobRun() {
        EVENTS.add("JOB_RUN");
    }

    public static void jobJoined(boolean successful) {
        EVENTS.add(successful ? "JOB_JOINED_OK" : "JOB_JOINED_FAILED");
    }

    public static void beginTerminalStop() {
        policy.beginTerminalStop();
        EVENTS.add("ADMISSION_CLOSED");
    }

    public static void listenerRemoved() {
        LISTENER_REGISTERED.set(false);
        EVENTS.add("LISTENER_REMOVED");
    }

    public static void invokeStaleCallback() {
        Runnable callback = Objects.requireNonNull(
                STALE_CALLBACK.get(), "stale callback");
        callback.run();
    }

    public static void revokeAndTerminate() {
        policy.revokeAndTerminate();
        EVENTS.add("PUBLICATION_REVOKED");
        EVENTS.add("OBSERVER_TERMINAL");
    }

    public static List<String> events() {
        return List.copyOf(EVENTS);
    }

    public static NoRuntimeUnloadPolicy policy() {
        return policy;
    }

    public static boolean listenerIsRegistered() {
        return LISTENER_REGISTERED.get();
    }

    public static int publicationCount() {
        return PUBLICATION_COUNT.get();
    }

    public static int controllerAcceptances() {
        return CONTROLLER_ACCEPTANCES.get();
    }

    public static int controllerRefusals() {
        return CONTROLLER_REFUSALS.get();
    }
}
