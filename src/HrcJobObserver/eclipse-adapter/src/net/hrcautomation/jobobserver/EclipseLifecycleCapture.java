package net.hrcautomation.jobobserver;

import java.util.Objects;
import org.eclipse.core.runtime.IStatus;
import org.eclipse.core.runtime.jobs.IJobChangeEvent;
import org.eclipse.core.runtime.jobs.Job;

/**
 * Converts one public Eclipse callback into a bounded mailbox item. Unknown Job
 * classes are ignored before their name, Bundle, flags, or status are read.
 * A recognised class must pass the Bundle identity check before those fields
 * are read.
 */
final class EclipseLifecycleCapture {
    private final OperationProfileSet profiles;
    private final BundleIdentityResolver bundleResolver;

    EclipseLifecycleCapture(
            OperationProfileSet profiles, BundleIdentityResolver bundleResolver) {
        this.profiles = Objects.requireNonNull(profiles, "profiles");
        this.bundleResolver = Objects.requireNonNull(bundleResolver, "bundleResolver");
    }

    CapturedLifecycle capture(
            LifecycleInput.Kind kind,
            IJobChangeEvent event,
            ObservationTime observed) {
        Objects.requireNonNull(kind, "kind");
        Objects.requireNonNull(event, "event");
        Objects.requireNonNull(observed, "observed");
        Job job = Objects.requireNonNull(event.getJob(), "event job");
        Class<?> jobClass = job.getClass();
        String className = jobClass.getName();
        OperationProfile profile = profiles.forClassName(className);
        if (profile == null) {
            return null;
        }

        BundleIdentity bundle = Objects.requireNonNull(
                bundleResolver.resolve(jobClass), "bundle identity");
        if (!profile.bundleSymbolicName().equals(bundle.symbolicName())
                || !profile.bundleVersion().equals(bundle.version())) {
            return new SourceMismatchLifecycle(
                    observed.utc(), observed.monotonicNanos());
        }
        JobDescriptor descriptor = new JobDescriptor(
                bundle.symbolicName(),
                bundle.version(),
                className,
                job.getName(),
                job.isUser(),
                job.isSystem());

        LifecycleInput input = switch (kind) {
            case SCHEDULED -> LifecycleInput.scheduled(
                    job, descriptor, observed.utc(), observed.monotonicNanos());
            case RUNNING -> LifecycleInput.running(
                    job, descriptor, observed.utc(), observed.monotonicNanos());
            case DONE -> LifecycleInput.done(
                    job,
                    descriptor,
                    captureStatus(event.getResult()),
                    observed.utc(),
                    observed.monotonicNanos());
        };
        return new ProfiledLifecycle(input);
    }

    private static StatusSnapshot captureStatus(IStatus status) {
        if (status == null) {
            return null;
        }
        return StatusSnapshot.capture(
                status.getSeverity(), status.isOK(), status.getCode(), status.getPlugin());
    }
}
