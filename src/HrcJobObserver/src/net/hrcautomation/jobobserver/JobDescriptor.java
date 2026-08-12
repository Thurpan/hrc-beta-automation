package net.hrcautomation.jobobserver;

import java.util.Objects;

record JobDescriptor(
        String bundleSymbolicName,
        String bundleVersion,
        String className,
        String name,
        boolean user,
        boolean system) {

    JobDescriptor {
        bundleSymbolicName = requireBounded(bundleSymbolicName, "bundleSymbolicName", 200);
        bundleVersion = requireBounded(bundleVersion, "bundleVersion", 100);
        className = requireBounded(className, "className", 300);
        name = requireBounded(name, "name", 300);
    }

    private static String requireBounded(String value, String field, int maximumLength) {
        Objects.requireNonNull(value, field);
        if (value.isBlank() || value.length() > maximumLength
                || value.chars().anyMatch(character -> character < 0x20 || character == 0x7f)) {
            throw new IllegalArgumentException(field + " is invalid");
        }
        return value;
    }
}
