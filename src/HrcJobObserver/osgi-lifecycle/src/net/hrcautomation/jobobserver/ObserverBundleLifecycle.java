package net.hrcautomation.jobobserver;

import java.time.Duration;
import java.util.Arrays;
import java.util.Objects;
import org.eclipse.core.runtime.jobs.Job;

/**
 * Owns one offline-assembled observer instance. It deliberately exposes no
 * live bootstrap path; callers must inject every prerequisite explicitly.
 */
final class ObserverBundleLifecycle {
    enum State {
        NEW,
        STARTING,
        ACTIVE,
        STOPPING,
        STOPPED,
        UNSAFE
    }

    private final ObserverLifecycleConfiguration configuration;
    private final JobManagerAccess jobManager;
    private final EclipseJobSourceClassifier sourceClassifier;
    private final EclipseCallbackMailbox mailbox;
    private final ListenerRegistrationGate listener;
    private final ObserverTransportControl control;
    private final ObserverTransportLifecycle transport;
    private final ObserverEndpointPublisher endpointPublisher;
    private final byte[] bearerToken;
    private State state = State.NEW;
    private boolean listenerRemovalRequired;
    private boolean transportClosed;
    private boolean mailboxClosed;
    private ObserverEndpointPublication publication;

    static ObserverBundleLifecycle assemble(
            ObserverLifecycleConfiguration configuration,
            ObserverLifecycleDependencies dependencies)
            throws ObserverLifecycleException {
        Objects.requireNonNull(configuration, "configuration");
        Objects.requireNonNull(dependencies, "dependencies");
        byte[] token = null;
        try {
            token = requireToken(dependencies.createToken());
            ObserverCoordinator coordinator = new ObserverCoordinator(
                    dependencies.sessionId(),
                    configuration.profiles(),
                    configuration.requestCapacity(),
                    configuration.jobCapacity(),
                    configuration.replayCapacity(),
                    dependencies.monotonicClock(),
                    dependencies.wallClock());
            EclipseCallbackMailbox mailbox = new EclipseCallbackMailbox(
                    configuration.mailboxCapacity(), coordinator);
            EclipseLifecycleCapture capture = new EclipseLifecycleCapture(
                    configuration.profiles(), dependencies.bundleResolver());
            EclipseJobChangeListener delegate = new EclipseJobChangeListener(
                    capture, mailbox, dependencies.observationClock());
            EclipseJobSourceClassifier classifier = new EclipseJobSourceClassifier(
                    configuration.profiles(), dependencies.bundleResolver());
            ListenerRegistrationGate listener = new ListenerRegistrationGate(
                    classifier, delegate);
            ObserverTransportControl control = new OrderedObserverTransportControl(
                    coordinator, mailbox, configuration.controlTimeout());

            byte[] transportToken = token.clone();
            ObserverTransportLifecycle transport;
            try {
                transport = Objects.requireNonNull(
                        dependencies.transportFactory().create(
                                control,
                                transportToken,
                                configuration.socketTimeout()),
                        "transport");
            } finally {
                Arrays.fill(transportToken, (byte) 0);
            }
            return new ObserverBundleLifecycle(
                    configuration,
                    dependencies.jobManager(),
                    classifier,
                    mailbox,
                    listener,
                    control,
                    transport,
                    dependencies.endpointPublisher(),
                    token);
        } catch (VirtualMachineError | ThreadDeath fatal) {
            wipe(token);
            throw fatal;
        } catch (ObserverLifecycleException failure) {
            wipe(token);
            throw failure;
        } catch (Throwable failure) {
            wipe(token);
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.RUNTIME_ASSEMBLY_FAILED);
        }
    }

    private ObserverBundleLifecycle(
            ObserverLifecycleConfiguration configuration,
            JobManagerAccess jobManager,
            EclipseJobSourceClassifier sourceClassifier,
            EclipseCallbackMailbox mailbox,
            ListenerRegistrationGate listener,
            ObserverTransportControl control,
            ObserverTransportLifecycle transport,
            ObserverEndpointPublisher endpointPublisher,
            byte[] bearerToken) {
        this.configuration = configuration;
        this.jobManager = jobManager;
        this.sourceClassifier = sourceClassifier;
        this.mailbox = mailbox;
        this.listener = listener;
        this.control = control;
        this.transport = transport;
        this.endpointPublisher = endpointPublisher;
        this.bearerToken = bearerToken;
    }

    synchronized void start() throws Exception {
        if (state != State.NEW) {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.ACTIVATOR_STATE_INVALID);
        }
        state = State.STARTING;
        try {
            startMailbox();
            registerListener();
            queryAndVerifyBaseline();
            listener.sealStartup();
            queryAndVerifyBaseline();
            ObserverLifecycleException.Reason startupFailure =
                    listener.activateAfterStartup(
                            configuration.lifecycleTimeout());
            if (startupFailure != null) {
                throw new ObserverLifecycleException(startupFailure);
            }
            requireStartupHealth();
            TransportEndpoint endpoint = startTransport();
            requireStartupHealth();
            requireTransportHealthy();
            publish(endpoint);
            state = State.ACTIVE;
        } catch (VirtualMachineError | ThreadDeath fatal) {
            ObserverLifecycleException cleanup = cleanup();
            if (cleanup != null) {
                fatal.addSuppressed(cleanup);
            }
            state = cleanup == null ? State.STOPPED : State.UNSAFE;
            throw fatal;
        } catch (ObserverLifecycleException failure) {
            ObserverLifecycleException cleanup = cleanup();
            if (cleanup != null) {
                failure.addSuppressed(cleanup);
            }
            state = cleanup == null ? State.STOPPED : State.UNSAFE;
            throw failure;
        } catch (Throwable failure) {
            ObserverLifecycleException safeFailure = new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.RUNTIME_ASSEMBLY_FAILED);
            ObserverLifecycleException cleanup = cleanup();
            if (cleanup != null) {
                safeFailure.addSuppressed(cleanup);
            }
            state = cleanup == null ? State.STOPPED : State.UNSAFE;
            throw safeFailure;
        }
    }

    synchronized void stop() throws Exception {
        if (state == State.STOPPED) {
            return;
        }
        if (state == State.NEW) {
            state = State.STOPPING;
        } else if (state == State.ACTIVE || state == State.UNSAFE) {
            state = State.STOPPING;
        } else {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.ACTIVATOR_STATE_INVALID);
        }
        ObserverLifecycleException cleanup = cleanup();
        state = cleanup == null ? State.STOPPED : State.UNSAFE;
        if (cleanup != null) {
            throw cleanup;
        }
    }

    synchronized State state() {
        return state;
    }

    ListenerRegistrationGate listener() {
        return listener;
    }

    ObserverCheckpoint checkpoint(long afterSequence) {
        return control.checkpoint(afterSequence);
    }

    private void startMailbox() throws ObserverLifecycleException {
        try {
            mailbox.start();
        } catch (VirtualMachineError | ThreadDeath fatal) {
            throw fatal;
        } catch (Throwable failure) {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.MAILBOX_START_FAILED);
        }
    }

    private void registerListener() throws ObserverLifecycleException {
        listenerRemovalRequired = true;
        try {
            jobManager.add(listener);
        } catch (VirtualMachineError | ThreadDeath fatal) {
            throw fatal;
        } catch (Throwable failure) {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.LISTENER_REGISTRATION_FAILED);
        }
    }

    private void queryAndVerifyBaseline() throws ObserverLifecycleException {
        Job[] jobs;
        try {
            jobs = Objects.requireNonNull(jobManager.findAll(), "baseline jobs");
        } catch (VirtualMachineError | ThreadDeath fatal) {
            throw fatal;
        } catch (Throwable failure) {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.BASELINE_QUERY_FAILED);
        }
        if (jobs.length > configuration.baselineJobCapacity()) {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.BASELINE_TOO_LARGE);
        }
        for (Job job : jobs) {
            EclipseJobSourceClassifier.Classification classification;
            try {
                classification = sourceClassifier.classify(job);
            } catch (VirtualMachineError | ThreadDeath fatal) {
                throw fatal;
            } catch (Throwable failure) {
                throw new ObserverLifecycleException(
                        ObserverLifecycleException.Reason.BASELINE_QUERY_FAILED);
            }
            if (classification == EclipseJobSourceClassifier.Classification.MATCH) {
                throw new ObserverLifecycleException(
                        ObserverLifecycleException.Reason.RELEVANT_JOB_PRESENT);
            }
            if (classification
                    == EclipseJobSourceClassifier.Classification.SOURCE_MISMATCH) {
                throw new ObserverLifecycleException(
                        ObserverLifecycleException.Reason.SOURCE_MISMATCH);
            }
        }
    }

    private void requireStartupHealth() throws ObserverLifecycleException {
        try {
            ObserverCheckpoint checkpoint = control.checkpoint(0);
            if (!checkpoint.actionable()
                    || !control.sessionId().equals(checkpoint.sessionId())) {
                throw new ObserverLifecycleException(
                        ObserverLifecycleException.Reason.STARTUP_HEALTH_FAILED);
            }
        } catch (VirtualMachineError | ThreadDeath fatal) {
            throw fatal;
        } catch (ObserverLifecycleException failure) {
            throw failure;
        } catch (Throwable failure) {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.STARTUP_HEALTH_FAILED);
        }
    }

    private TransportEndpoint startTransport() throws ObserverLifecycleException {
        try {
            return Objects.requireNonNull(transport.start(), "transport endpoint");
        } catch (VirtualMachineError | ThreadDeath fatal) {
            throw fatal;
        } catch (Throwable failure) {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.TRANSPORT_START_FAILED);
        }
    }

    private void requireTransportHealthy() throws ObserverLifecycleException {
        try {
            transport.requireHealthy();
        } catch (VirtualMachineError | ThreadDeath fatal) {
            throw fatal;
        } catch (Throwable failure) {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.TRANSPORT_HEALTH_FAILED);
        }
    }

    private void publish(TransportEndpoint endpoint)
            throws ObserverLifecycleException {
        byte[] publicationToken = bearerToken.clone();
        try {
            publication = Objects.requireNonNull(
                    endpointPublisher.publish(endpoint, publicationToken),
                    "endpoint publication");
        } catch (VirtualMachineError | ThreadDeath fatal) {
            throw fatal;
        } catch (Throwable failure) {
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.ENDPOINT_PUBLICATION_FAILED);
        } finally {
            Arrays.fill(publicationToken, (byte) 0);
            wipe(bearerToken);
        }
    }

    private ObserverLifecycleException cleanup() {
        ObserverLifecycleException aggregate = null;
        if (publication != null) {
            try {
                publication.close();
                publication = null;
            } catch (Exception failure) {
                aggregate = append(
                        aggregate,
                        ObserverLifecycleException.Reason.ENDPOINT_REVOCATION_FAILED);
            }
        }

        if (!transportClosed) {
            try {
                transport.closeAndAwait(configuration.lifecycleTimeout());
                transportClosed = true;
            } catch (Exception failure) {
                aggregate = append(
                        aggregate,
                        ObserverLifecycleException.Reason.TRANSPORT_SHUTDOWN_UNCLEAN);
            }
        }

        listener.closeAdmissions();
        if (listenerRemovalRequired) {
            try {
                jobManager.remove(listener);
                listenerRemovalRequired = false;
            } catch (RuntimeException failure) {
                aggregate = append(
                        aggregate,
                        ObserverLifecycleException.Reason.LISTENER_REMOVAL_FAILED);
            }
        }
        if (!listener.awaitAdmittedInvocations(
                configuration.lifecycleTimeout())) {
            aggregate = append(
                    aggregate,
                    ObserverLifecycleException.Reason.LISTENER_DRAIN_TIMEOUT);
        }

        if (!mailboxClosed) {
            try {
                MailboxCloseResult result = mailbox.closeAndAwait(
                        configuration.lifecycleTimeout());
                if (result.clean()) {
                    mailboxClosed = true;
                } else {
                    aggregate = append(
                            aggregate,
                            ObserverLifecycleException.Reason.MAILBOX_SHUTDOWN_UNCLEAN);
                }
            } catch (RuntimeException failure) {
                aggregate = append(
                        aggregate,
                        ObserverLifecycleException.Reason.MAILBOX_SHUTDOWN_UNCLEAN);
            }
        }
        if (listener.callbacksInFlight() != 0
                || mailbox.callbacksInFlightCount() != 0
                || mailbox.retainedCallbackCount() != 0
                || mailbox.reservedCallbackCount() != 0) {
            aggregate = append(
                    aggregate, ObserverLifecycleException.Reason.CALLBACKS_REMAIN);
        }
        wipe(bearerToken);
        return aggregate;
    }

    private static ObserverLifecycleException append(
            ObserverLifecycleException aggregate,
            ObserverLifecycleException.Reason reason) {
        ObserverLifecycleException next = new ObserverLifecycleException(reason);
        if (aggregate == null) {
            return next;
        }
        aggregate.addSuppressed(next);
        return aggregate;
    }

    private static byte[] requireToken(byte[] token)
            throws ObserverLifecycleException {
        if (token == null || token.length != LocalObserverServer.TOKEN_BYTES) {
            wipe(token);
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.TOKEN_INVALID);
        }
        boolean allZero = true;
        for (byte value : token) {
            allZero &= value == 0;
        }
        if (allZero) {
            wipe(token);
            throw new ObserverLifecycleException(
                    ObserverLifecycleException.Reason.TOKEN_INVALID);
        }
        return token;
    }

    private static void wipe(byte[] value) {
        if (value != null) {
            Arrays.fill(value, (byte) 0);
        }
    }
}
