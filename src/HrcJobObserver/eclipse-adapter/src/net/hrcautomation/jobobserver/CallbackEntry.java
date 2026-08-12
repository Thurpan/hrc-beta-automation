package net.hrcautomation.jobobserver;

import java.util.concurrent.atomic.AtomicLong;

/** Single-use lease for one callback that entered while the mailbox was active. */
final class CallbackEntry {
    private static final long ENTERED = 0;
    private static final long TERMINATED = -1;

    private final EclipseCallbackMailbox owner;
    private final AtomicLong state = new AtomicLong(ENTERED);

    CallbackEntry(EclipseCallbackMailbox owner) {
        this.owner = owner;
    }

    boolean belongsTo(EclipseCallbackMailbox mailbox) {
        return owner == mailbox;
    }

    boolean admit(long ticket) {
        if (ticket <= 0) {
            throw new IllegalArgumentException("callback ticket must be positive");
        }
        return state.compareAndSet(ENTERED, ticket);
    }

    long admittedTicket() {
        long current = state.get();
        return current > 0 ? current : 0;
    }

    boolean terminate() {
        while (true) {
            long current = state.get();
            if (current == TERMINATED) {
                return false;
            }
            if (state.compareAndSet(current, TERMINATED)) {
                return true;
            }
        }
    }
}
