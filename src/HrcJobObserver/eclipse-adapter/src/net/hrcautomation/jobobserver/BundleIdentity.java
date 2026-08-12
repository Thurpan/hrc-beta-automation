package net.hrcautomation.jobobserver;

import java.util.Objects;

/** Public OSGi identity needed to project a Job without retaining its Bundle. */
record BundleIdentity(String symbolicName, String version) {
    BundleIdentity {
        symbolicName = requireToken(symbolicName, "symbolicName");
        version = requireToken(version, "version");
    }

    private static String requireToken(String value, String field) {
        Objects.requireNonNull(value, field);
        if (value.isBlank() || value.length() > 200
                || value.chars().anyMatch(character ->
                        character <= 0x20 || character == 0x7f)) {
            throw new IllegalArgumentException(field + " is invalid");
        }
        return value;
    }
}
