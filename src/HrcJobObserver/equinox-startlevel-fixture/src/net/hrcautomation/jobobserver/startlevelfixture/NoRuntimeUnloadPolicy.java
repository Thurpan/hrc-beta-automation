package net.hrcautomation.jobobserver.startlevelfixture;

import java.util.EnumMap;
import java.util.Map;
import java.util.Objects;

/**
 * Test-only model for the policy that keeps observer code loaded until JVM
 * shutdown. Runtime mutation requests are refused rather than executed.
 */
public final class NoRuntimeUnloadPolicy {
    /** Lifecycle states observable by the synthetic controller. */
    public enum State {
        NEW,
        STARTING,
        ACTIVE,
        STOPPING,
        TERMINAL
    }

    /** Operations prohibited while the synthetic JVM continues. */
    public enum ForbiddenAction {
        REPUBLISH,
        RESTART,
        UPDATE,
        UNINSTALL,
        REFRESH
    }

    private final Map<ForbiddenAction, Integer> refusalCounts =
            new EnumMap<>(ForbiddenAction.class);
    private State state = State.NEW;
    private boolean admissionOpen;
    private boolean publicationActive;
    private int admittedCallbacks;
    private int rejectedCallbacks;

    /** Begins the single permitted observer start attempt. */
    public synchronized void beginStart() {
        requireState(State.NEW);
        state = State.STARTING;
    }

    /** Makes controller admission possible only after publication succeeds. */
    public synchronized void activatePublication() {
        requireState(State.STARTING);
        admissionOpen = true;
        publicationActive = true;
        state = State.ACTIVE;
    }

    /** Permanently closes a failed start without publishing. */
    public synchronized void failStart() {
        requireState(State.STARTING);
        admissionOpen = false;
        publicationActive = false;
        state = State.TERMINAL;
    }

    /** Atomically closes callback admission before listener removal. */
    public synchronized void beginTerminalStop() {
        requireState(State.ACTIVE);
        admissionOpen = false;
        state = State.STOPPING;
    }

    /** Revokes the publication and makes the lifecycle terminal. */
    public synchronized void revokeAndTerminate() {
        requireState(State.STOPPING);
        publicationActive = false;
        state = State.TERMINAL;
    }

    /** Returns whether a controller may act on the current publication. */
    public synchronized boolean controllerAllowed() {
        return state == State.ACTIVE && admissionOpen && publicationActive;
    }

    /**
     * Attempts to admit one provider callback. A callback arriving after
     * closure is counted and rejected without invoking observer logic.
     */
    public synchronized boolean tryAdmitCallback() {
        if (state != State.ACTIVE || !admissionOpen || !publicationActive) {
            rejectedCallbacks++;
            return false;
        }
        admittedCallbacks++;
        return true;
    }

    /** Records and refuses a prohibited same-JVM lifecycle mutation. */
    public synchronized boolean request(ForbiddenAction action) {
        Objects.requireNonNull(action, "action");
        refusalCounts.merge(action, 1, Integer::sum);
        return false;
    }

    public synchronized State state() {
        return state;
    }

    public synchronized boolean admissionOpen() {
        return admissionOpen;
    }

    public synchronized boolean publicationActive() {
        return publicationActive;
    }

    public synchronized int admittedCallbacks() {
        return admittedCallbacks;
    }

    public synchronized int rejectedCallbacks() {
        return rejectedCallbacks;
    }

    public synchronized int refusalCount(ForbiddenAction action) {
        return refusalCounts.getOrDefault(
                Objects.requireNonNull(action, "action"), 0);
    }

    private void requireState(State expected) {
        if (state != expected) {
            throw new IllegalStateException(
                    "Expected " + expected + " but was " + state);
        }
    }
}
