package net.hrcautomation.jobobserver;

/** One synchronized projection of replay and the core's first fault. */
record ObserverCoreSnapshot(ReplayQuery replay, FaultReason faultReason) {
    ObserverCoreSnapshot {
        if (replay == null) {
            throw new NullPointerException("replay");
        }
    }
}
