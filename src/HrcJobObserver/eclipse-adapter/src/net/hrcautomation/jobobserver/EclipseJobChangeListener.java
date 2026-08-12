package net.hrcautomation.jobobserver;

import java.util.Objects;
import org.eclipse.core.runtime.jobs.IJobChangeEvent;
import org.eclipse.core.runtime.jobs.JobChangeAdapter;

/**
 * Prompt Eclipse callback adapter. A callback only captures bounded public
 * data and offers it through the mailbox's non-waiting callback hand-off. It
 * never calls the observer core, performs I/O, logs, serializes, mutates a Job,
 * or touches UI. The hand-off does not wait for the mailbox worker.
 */
final class EclipseJobChangeListener extends JobChangeAdapter {
    private final EclipseLifecycleCapture capture;
    private final EclipseCallbackMailbox mailbox;
    private final ObservationClock clock;

    EclipseJobChangeListener(
            EclipseLifecycleCapture capture,
            EclipseCallbackMailbox mailbox,
            ObservationClock clock) {
        this.capture = Objects.requireNonNull(capture, "capture");
        this.mailbox = Objects.requireNonNull(mailbox, "mailbox");
        this.clock = Objects.requireNonNull(clock, "clock");
    }

    @Override
    public void scheduled(IJobChangeEvent event) {
        handle(LifecycleInput.Kind.SCHEDULED, event);
    }

    @Override
    public void running(IJobChangeEvent event) {
        handle(LifecycleInput.Kind.RUNNING, event);
    }

    @Override
    public void done(IJobChangeEvent event) {
        handle(LifecycleInput.Kind.DONE, event);
    }

    private void handle(LifecycleInput.Kind kind, IJobChangeEvent event) {
        CallbackEntry entry = mailbox.beginCallback();
        if (entry == null) {
            return;
        }
        ObservationTime observed = null;
        try {
            ObservationTime callbackTime = Objects.requireNonNull(
                    clock.capture(), "observation time");
            observed = callbackTime;
            if (!mailbox.admitCallback(entry, callbackTime)) {
                return;
            }
            CapturedLifecycle captured = capture.capture(kind, event, callbackTime);
            mailbox.completeCallback(entry, captured);
        } catch (VirtualMachineError | ThreadDeath fatal) {
            InfrastructureIncident incident = observed == null
                    ? InfrastructureIncident.unobserved(
                            InfrastructureFailure.CALLBACK_CAPTURE_FAILED)
                    : InfrastructureIncident.observed(
                            InfrastructureFailure.CALLBACK_CAPTURE_FAILED, observed);
            mailbox.failCallback(entry, incident);
            throw fatal;
        } catch (Throwable failure) {
            InfrastructureIncident incident = observed == null
                    ? InfrastructureIncident.unobserved(
                            InfrastructureFailure.CALLBACK_CAPTURE_FAILED)
                    : InfrastructureIncident.observed(
                            InfrastructureFailure.CALLBACK_CAPTURE_FAILED, observed);
            mailbox.failCallback(entry, incident);
        }
    }
}
