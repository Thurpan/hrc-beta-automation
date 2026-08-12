package net.hrcautomation.jobobserver;

import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.List;
import java.util.function.LongFunction;

/** Bounded, sequenced event history for one observer session. */
final class ReplayBuffer {
    private final int capacity;
    private final ArrayDeque<ObserverEvent> events;
    private long nextSequence;

    ReplayBuffer(int capacity) {
        this(capacity, 1);
    }

    ReplayBuffer(int capacity, long initialSequence) {
        if (capacity < 1) {
            throw new IllegalArgumentException("capacity must be positive");
        }
        if (initialSequence < 1) {
            throw new IllegalArgumentException("initialSequence must be positive");
        }
        this.capacity = capacity;
        this.events = new ArrayDeque<>(capacity);
        this.nextSequence = initialSequence;
    }

    int capacity() {
        return capacity;
    }

    synchronized ObserverEvent append(LongFunction<ObserverEvent> eventFactory) {
        if (nextSequence == Long.MAX_VALUE) {
            throw new IllegalStateException("event sequence exhausted");
        }
        long sequence = nextSequence;
        ObserverEvent event = eventFactory.apply(sequence);
        if (event == null || event.sequence() != sequence) {
            throw new IllegalArgumentException("event factory returned an invalid sequence");
        }
        if (events.size() == capacity) {
            events.removeFirst();
        }
        events.addLast(event);
        nextSequence++;
        return event;
    }

    synchronized ReplayQuery replayAfter(long lastSeenSequence) {
        if (lastSeenSequence < 0) {
            throw new IllegalArgumentException("lastSeenSequence must not be negative");
        }
        long latest = nextSequence - 1;
        if (events.isEmpty()) {
            ReplayQuery.Disposition disposition;
            if (lastSeenSequence == latest) {
                disposition = ReplayQuery.Disposition.OK;
            } else if (lastSeenSequence < latest) {
                disposition = ReplayQuery.Disposition.GAP;
            } else {
                disposition = ReplayQuery.Disposition.CURSOR_AHEAD;
            }
            return new ReplayQuery(disposition, List.of(), nextSequence, latest);
        }

        long oldest = events.getFirst().sequence();
        if (lastSeenSequence > latest) {
            return new ReplayQuery(ReplayQuery.Disposition.CURSOR_AHEAD, List.of(), oldest, latest);
        }
        if (lastSeenSequence < oldest - 1) {
            return new ReplayQuery(ReplayQuery.Disposition.GAP, List.of(), oldest, latest);
        }

        List<ObserverEvent> replay = new ArrayList<>();
        for (ObserverEvent event : events) {
            if (event.sequence() > lastSeenSequence) {
                replay.add(event);
            }
        }
        return new ReplayQuery(ReplayQuery.Disposition.OK, replay, oldest, latest);
    }
}
