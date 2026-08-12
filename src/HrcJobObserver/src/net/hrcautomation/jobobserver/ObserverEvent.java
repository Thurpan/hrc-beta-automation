package net.hrcautomation.jobobserver;

sealed interface ObserverEvent permits ArmAcceptedEvent, ArmConfirmedEvent,
        JobScheduledEvent, JobRunningEvent, JobRunningRejectedEvent,
        JobTerminalEvent, JobTerminalRejectedEvent, ObserverFaultEvent {

    EventMetadata metadata();

    default long sequence() {
        return metadata().sequence();
    }
}
