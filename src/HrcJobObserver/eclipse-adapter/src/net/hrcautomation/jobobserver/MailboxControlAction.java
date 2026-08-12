package net.hrcautomation.jobobserver;

/** Bounded action executed only by the ordered mailbox worker. */
@FunctionalInterface
interface MailboxControlAction<T> {
    T execute(MailboxHealthSnapshot before);
}
