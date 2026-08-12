package net.hrcautomation.jobobserver;

@FunctionalInterface
interface ObservationClock {
    ObservationTime capture();
}
