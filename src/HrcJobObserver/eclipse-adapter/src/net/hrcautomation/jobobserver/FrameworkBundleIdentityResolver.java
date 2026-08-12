package net.hrcautomation.jobobserver;

import java.util.Objects;
import org.osgi.framework.Bundle;
import org.osgi.framework.FrameworkUtil;

/** Resolves the defining OSGi Bundle of the concrete Job implementation class. */
final class FrameworkBundleIdentityResolver implements BundleIdentityResolver {
    @Override
    public BundleIdentity resolve(Class<?> jobClass) {
        Objects.requireNonNull(jobClass, "jobClass");
        Bundle bundle = FrameworkUtil.getBundle(jobClass);
        if (bundle == null) {
            throw new IllegalStateException("Job class is not defined by an OSGi Bundle");
        }
        return new BundleIdentity(
                bundle.getSymbolicName(),
                Objects.requireNonNull(bundle.getVersion(), "bundle version").toString());
    }
}
