package net.hrcautomation.jobobserver;

import java.util.concurrent.atomic.AtomicInteger;

/** Single-use, pre-ticketed lease for one callback admitted while active. */
final class CallbackEntry {
    private static final int ENTERED = 0;
    private static final int ADMITTED = 1;
    private static final int TERMINATED = 2;

    private final EclipseCallbackMailbox owner;
    private volatile long ticket;
    private final AtomicInteger state = new AtomicInteger(ENTERED);

    CallbackEntry(EclipseCallbackMailbox owner) {
        this.owner = owner;
    }

    boolean belongsTo(EclipseCallbackMailbox mailbox) {
        return owner == mailbox;
    }

    long ticket() {
        if (ticket <= 0) {
            throw new IllegalStateException("callback ticket is not assigned");
        }
        return ticket;
    }

    void assignTicket(long assignedTicket) {
        if (assignedTicket <= 0 || ticket != 0) {
            throw new IllegalStateException("callback ticket cannot be assigned");
        }
        ticket = assignedTicket;
    }

    boolean admit() {
        return state.compareAndSet(ENTERED, ADMITTED);
    }

    long admittedTicket() {
        return state.get() == ADMITTED ? ticket : 0;
    }

    boolean terminate() {
        while (true) {
            int current = state.get();
            if (current == TERMINATED) {
                return false;
            }
            if (state.compareAndSet(current, TERMINATED)) {
                return true;
            }
        }
    }
}
