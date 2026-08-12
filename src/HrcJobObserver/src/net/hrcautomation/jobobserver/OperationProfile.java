package net.hrcautomation.jobobserver;

import java.util.Objects;

/** Version-gated recogniser for one critical HRC Job type. */
record OperationProfile(
        OperationKind operation,
        String bundleSymbolicName,
        String bundleVersion,
        String className) {

    OperationProfile {
        Objects.requireNonNull(operation, "operation");
        bundleSymbolicName = requireToken(bundleSymbolicName, "bundleSymbolicName");
        bundleVersion = requireToken(bundleVersion, "bundleVersion");
        className = requireToken(className, "className");
    }

    boolean matches(ArmRequest arm, JobDescriptor job) {
        return arm.operation() == operation
                && arm.expectedJobName().equals(job.name())
                && bundleSymbolicName.equals(job.bundleSymbolicName())
                && bundleVersion.equals(job.bundleVersion())
                && className.equals(job.className());
    }

    boolean classMatches(JobDescriptor job) {
        return className.equals(job.className());
    }

    boolean sourceMatches(JobDescriptor job) {
        return bundleSymbolicName.equals(job.bundleSymbolicName())
                && bundleVersion.equals(job.bundleVersion());
    }

    private static String requireToken(String value, String field) {
        Objects.requireNonNull(value, field);
        if (value.isBlank() || value.length() > 300
                || value.chars().anyMatch(character -> character <= 0x20 || character == 0x7f)) {
            throw new IllegalArgumentException(field + " is invalid");
        }
        return value;
    }
}
