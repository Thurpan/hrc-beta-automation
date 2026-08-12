package net.hrcautomation.jobobserver;

import java.util.Objects;
import org.eclipse.core.runtime.jobs.IJobChangeListener;
import org.eclipse.core.runtime.jobs.IJobManager;
import org.eclipse.core.runtime.jobs.Job;

/** Production-shaped facade; no lifecycle code uses Eclipse internal APIs. */
final class EclipseJobManagerAccess implements JobManagerAccess {
    private final IJobManager manager;

    EclipseJobManagerAccess() {
        this(Job.getJobManager());
    }

    EclipseJobManagerAccess(IJobManager manager) {
        this.manager = Objects.requireNonNull(manager, "manager");
    }

    @Override
    public void add(IJobChangeListener listener) {
        manager.addJobChangeListener(listener);
    }

    @Override
    public Job[] findAll() {
        return manager.find(null);
    }

    @Override
    public void remove(IJobChangeListener listener) {
        manager.removeJobChangeListener(listener);
    }
}
