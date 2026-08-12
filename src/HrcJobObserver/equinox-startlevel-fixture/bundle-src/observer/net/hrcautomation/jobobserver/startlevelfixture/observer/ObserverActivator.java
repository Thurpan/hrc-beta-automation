package net.hrcautomation.jobobserver.startlevelfixture.observer;

import net.hrcautomation.jobobserver.startlevelfixture.FixtureProbe;
import org.eclipse.core.runtime.jobs.IJobChangeEvent;
import org.eclipse.core.runtime.jobs.IJobManager;
import org.eclipse.core.runtime.jobs.Job;
import org.eclipse.core.runtime.jobs.JobChangeAdapter;
import org.osgi.framework.Bundle;
import org.osgi.framework.BundleActivator;
import org.osgi.framework.BundleContext;
import org.osgi.framework.startlevel.BundleStartLevel;
import org.osgi.framework.startlevel.FrameworkStartLevel;

/** Test-only listener Bundle generated and installed only in temporary storage. */
public final class ObserverActivator implements BundleActivator {
    private static final String[] PROVIDER_SYMBOLIC_NAMES = {
        "org.eclipse.equinox.common",
        "org.eclipse.core.contenttype",
        "org.eclipse.equinox.app",
        "org.eclipse.equinox.preferences",
        "org.eclipse.equinox.registry",
        "org.osgi.service.prefs",
        "org.eclipse.core.jobs",
        "org.eclipse.core.runtime"
    };

    private IJobManager manager;
    private AdmissionListener listener;

    @Override
    public void start(BundleContext context) {
        FixtureProbe.observerStarting(
                context.getBundle().adapt(BundleStartLevel.class).getStartLevel(),
                context.getBundle(0).adapt(FrameworkStartLevel.class).getStartLevel());
        recordProviderStates(context);
        if (FixtureProbe.shouldFailObserverStart()) {
            FixtureProbe.observerStartFailed();
            throw new IllegalStateException("SYNTHETIC_OBSERVER_START_FAILURE");
        }

        AdmissionListener candidate = new AdmissionListener();
        IJobManager jobManager = Job.getJobManager();
        jobManager.addJobChangeListener(candidate);
        manager = jobManager;
        listener = candidate;
        FixtureProbe.listenerRegistered();
        FixtureProbe.registerStaleCallback(candidate::syntheticStaleCallback);
        FixtureProbe.publicationActivated();
    }

    private static void recordProviderStates(BundleContext context) {
        for (String symbolicName : PROVIDER_SYMBOLIC_NAMES) {
            Bundle bundle = findUniqueBundle(context, symbolicName);
            BundleStartLevel startLevel = bundle.adapt(BundleStartLevel.class);
            if (startLevel == null) {
                throw new IllegalStateException(
                        "Provider has no BundleStartLevel: " + symbolicName);
            }
            FixtureProbe.providerState(
                    symbolicName,
                    bundle.getState(),
                    startLevel.getStartLevel(),
                    startLevel.isPersistentlyStarted());
        }
    }

    private static Bundle findUniqueBundle(
            BundleContext context, String symbolicName) {
        Bundle match = null;
        for (Bundle candidate : context.getBundles()) {
            if (!symbolicName.equals(candidate.getSymbolicName())) {
                continue;
            }
            if (match != null) {
                throw new IllegalStateException(
                        "Duplicate provider Bundle: " + symbolicName);
            }
            match = candidate;
        }
        if (match == null) {
            throw new IllegalStateException(
                    "Missing provider Bundle: " + symbolicName);
        }
        return match;
    }

    @Override
    public void stop(BundleContext context) {
        FixtureProbe.beginTerminalStop();
        manager.removeJobChangeListener(listener);
        FixtureProbe.listenerRemoved();
        FixtureProbe.invokeStaleCallback();
        FixtureProbe.revokeAndTerminate();
        manager = null;
        listener = null;
    }

    private static final class AdmissionListener extends JobChangeAdapter {
        @Override
        public void scheduled(IJobChangeEvent event) {
            FixtureProbe.jobCallback("SCHEDULED", event.getJob().getName());
        }

        @Override
        public void running(IJobChangeEvent event) {
            FixtureProbe.jobCallback("RUNNING", event.getJob().getName());
        }

        @Override
        public void done(IJobChangeEvent event) {
            FixtureProbe.jobCallback("DONE", event.getJob().getName());
        }

        private void syntheticStaleCallback() {
            FixtureProbe.jobCallback("STALE", FixtureProbe.TARGET_JOB_NAME);
        }
    }
}
