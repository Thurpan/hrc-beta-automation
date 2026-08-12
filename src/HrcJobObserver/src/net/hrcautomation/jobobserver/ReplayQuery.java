package net.hrcautomation.jobobserver;

import java.util.List;

record ReplayQuery(
        Disposition disposition,
        List<ObserverEvent> events,
        long oldestAvailable,
        long latestAvailable) {

    enum Disposition {
        OK,
        GAP,
        CURSOR_AHEAD
    }

    ReplayQuery {
        events = List.copyOf(events);
    }
}
