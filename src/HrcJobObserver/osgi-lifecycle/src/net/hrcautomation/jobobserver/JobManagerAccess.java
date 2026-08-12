package net.hrcautomation.jobobserver;

import org.eclipse.core.runtime.jobs.IJobChangeListener;
import org.eclipse.core.runtime.jobs.Job;

/** Narrow public-API seam used to deterministically test manager races offline. */
interface JobManagerAccess {
    void add(IJobChangeListener listener);

    Job[] findAll();

    void remove(IJobChangeListener listener);
}
