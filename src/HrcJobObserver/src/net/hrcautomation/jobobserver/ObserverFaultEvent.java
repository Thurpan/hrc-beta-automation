package net.hrcautomation.jobobserver;

import java.util.Objects;

record ObserverFaultEvent(
        EventMetadata metadata,
        FaultReason reason) implements ObserverEvent {

    ObserverFaultEvent {
        Objects.requireNonNull(metadata, "metadata");
        Objects.requireNonNull(reason, "reason");
    }
}
