package net.hrcautomation.jobobserver;

import java.util.Objects;
import org.eclipse.core.runtime.jobs.Job;

/**
 * Minimal startup classifier. Unknown classes are rejected from further reads;
 * recognised classes are checked against their exact defining Bundle.
 */
final class EclipseJobSourceClassifier {
    enum Classification {
        IRRELEVANT,
        MATCH,
        SOURCE_MISMATCH
    }

    private final OperationProfileSet profiles;
    private final BundleIdentityResolver bundleResolver;

    EclipseJobSourceClassifier(
            OperationProfileSet profiles,
            BundleIdentityResolver bundleResolver) {
        this.profiles = Objects.requireNonNull(profiles, "profiles");
        this.bundleResolver = Objects.requireNonNull(
                bundleResolver, "bundleResolver");
    }

    Classification classify(Job job) {
        Objects.requireNonNull(job, "job");
        Class<?> jobClass = job.getClass();
        OperationProfile profile = profiles.forClassName(jobClass.getName());
        if (profile == null) {
            return Classification.IRRELEVANT;
        }
        BundleIdentity bundle = Objects.requireNonNull(
                bundleResolver.resolve(jobClass), "bundle identity");
        if (!profile.bundleSymbolicName().equals(bundle.symbolicName())
                || !profile.bundleVersion().equals(bundle.version())) {
            return Classification.SOURCE_MISMATCH;
        }
        return Classification.MATCH;
    }
}
