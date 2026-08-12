package net.hrcautomation.jobobserver;

import java.util.Objects;
import org.osgi.framework.BundleActivator;
import org.osgi.framework.BundleContext;

/**
 * Structurally valid but operationally disabled OSGi entry point. The public
 * constructor cannot register, open a socket, or create a worker. Offline tests
 * use the package-private constructor to inject a fully owned lifecycle.
 */
public final class HrcJobObserverActivator implements BundleActivator {
    private final ObserverBundleLifecycleFactory lifecycleFactory;
    private boolean startAttempted;
    private ObserverBundleLifecycle lifecycle;

    public HrcJobObserverActivator() {
        this(context -> {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.BOOTSTRAP_DISABLED);
        });
    }

    HrcJobObserverActivator(ObserverBundleLifecycleFactory lifecycleFactory) {
        this.lifecycleFactory = Objects.requireNonNull(
                lifecycleFactory, "lifecycleFactory");
    }

    @Override
    public synchronized void start(BundleContext context) throws Exception {
        Objects.requireNonNull(context, "context");
        if (startAttempted || lifecycle != null) {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.ACTIVATOR_STATE_INVALID);
        }
        startAttempted = true;
        ObserverBundleLifecycle candidate;
        try {
            candidate = Objects.requireNonNull(
                    lifecycleFactory.create(context), "lifecycle");
        } catch (VirtualMachineError | ThreadDeath fatal) {
            throw fatal;
        } catch (ObserverLifecycleException failure) {
            throw failure;
        } catch (Throwable failure) {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.RUNTIME_ASSEMBLY_FAILED);
        }
        candidate.start();
        lifecycle = candidate;
    }

    @Override
    public synchronized void stop(BundleContext context) throws Exception {
        Objects.requireNonNull(context, "context");
        if (lifecycle == null) {
            return;
        }
        lifecycle.stop();
        lifecycle = null;
    }
}

@FunctionalInterface
interface ObserverBundleLifecycleFactory {
    ObserverBundleLifecycle create(BundleContext context) throws Exception;
}
