package net.hrcautomation.jobobserver.startlevelfixture.observer;

import net.hrcautomation.jobobserver.startlevelfixture.FixtureProbe;
import org.eclipse.core.runtime.jobs.IJobChangeEvent;
import org.eclipse.core.runtime.jobs.IJobManager;
import org.eclipse.core.runtime.jobs.Job;
import org.eclipse.core.runtime.jobs.JobChangeAdapter;
import org.osgi.framework.BundleActivator;
import org.osgi.framework.Bundle;
import org.osgi.framework.BundleContext;
import org.osgi.framework.startlevel.BundleStartLevel;
import org.osgi.framework.startlevel.FrameworkStartLevel;

/** Test-only listener Bundle generated and installed only in temporary storage. */
public final class ObserverActivator implements BundleActivator {
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
        for (String symbolicName : new String[]{
                "org.eclipse.core.jobs", "org.eclipse.core.runtime"}) {
            for (Bundle bundle : context.getBundles()) {
                if (symbolicName.equals(bundle.getSymbolicName())) {
                    BundleStartLevel startLevel = bundle.adapt(BundleStartLevel.class);
                    FixtureProbe.providerState(
                            symbolicName,
                            bundle.getState(),
                            startLevel.getStartLevel(),
                            startLevel.isPersistentlyStarted());
                }
            }
        }
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
