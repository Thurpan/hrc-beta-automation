package net.hrcautomation.jobobserver;

/** Explicit shutdown health. Callers must not discard an unclean result. */
record TransportCloseResult(TransportFailure failure, boolean workerTerminated) {
    boolean clean() {
        return failure == null && workerTerminated;
    }
}
