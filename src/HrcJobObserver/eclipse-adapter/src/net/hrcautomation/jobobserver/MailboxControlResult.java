package net.hrcautomation.jobobserver;

import java.util.Objects;

/** One ordered action plus authoritative health immediately before and after. */
record MailboxControlResult<T>(
        long barrierId,
        T value,
        MailboxHealthSnapshot before,
        MailboxHealthSnapshot after) {

    MailboxControlResult {
        if (barrierId <= 0) {
            throw new IllegalArgumentException("barrier id must be positive");
        }
        Objects.requireNonNull(value, "value");
        Objects.requireNonNull(before, "before");
        Objects.requireNonNull(after, "after");
    }
}
