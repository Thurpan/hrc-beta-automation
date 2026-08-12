package net.hrcautomation.jobobserver.startlevelfixture.producer;

import net.hrcautomation.jobobserver.startlevelfixture.FixtureProbe;
import org.eclipse.core.runtime.IStatus;
import org.eclipse.core.runtime.Status;
import org.eclipse.core.runtime.jobs.Job;
import org.osgi.framework.BundleActivator;
import org.osgi.framework.BundleContext;
import org.osgi.framework.startlevel.BundleStartLevel;
import org.osgi.framework.startlevel.FrameworkStartLevel;

/** Test-only immediate Job producer generated only in temporary storage. */
public final class ProducerActivator implements BundleActivator {
    @Override
    public void start(BundleContext context) throws Exception {
        FixtureProbe.producerStarting(
                context.getBundle().adapt(BundleStartLevel.class).getStartLevel(),
                context.getBundle(0).adapt(FrameworkStartLevel.class).getStartLevel());
        if (!FixtureProbe.controllerAttempt()) {
            return;
        }

        Job job = Job.create(FixtureProbe.TARGET_JOB_NAME, monitor -> {
            FixtureProbe.jobRun();
            return Status.OK_STATUS;
        });
        job.schedule();
        job.join();
        IStatus result = job.getResult();
        FixtureProbe.jobJoined(result != null && result.isOK());
    }

    @Override
    public void stop(BundleContext context) {
        // No producer-owned state survives the immediate Job.
    }
}
