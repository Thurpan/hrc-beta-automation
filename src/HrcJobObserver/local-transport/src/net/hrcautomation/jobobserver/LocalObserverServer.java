package net.hrcautomation.jobobserver;

import java.io.BufferedOutputStream;
import java.io.IOException;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.time.Duration;
import java.util.Base64;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

/**
 * Single-client authenticated loopback protocol. This class owns no endpoint
 * descriptor or token persistence and is not a runtime entry point.
 */
final class LocalObserverServer {
    static final int PROTOCOL_VERSION = 1;
    static final int MAX_FRAME_BYTES = 8 * 1024;
    static final int TOKEN_BYTES = 32;
    private static final byte[] HELLO_PREFIX = ("HELLO\t"
            + PROTOCOL_VERSION + "\t").getBytes(StandardCharsets.US_ASCII);
    private static final int ENCODED_TOKEN_BYTES =
            (TOKEN_BYTES * 8 + 5) / 6;
    private static final int HELLO_FRAME_BYTES =
            HELLO_PREFIX.length + ENCODED_TOKEN_BYTES;
    static final long MIN_ARM_TIMEOUT_MILLIS = Duration.ofSeconds(5).toMillis();
    static final long MAX_ARM_TIMEOUT_MILLIS = Duration.ofMinutes(5).toMillis();

    private enum State {
        NEW,
        ACTIVE,
        CLOSING,
        CLOSED
    }

    private final ObserverTransportControl control;
    private final UUID sessionId;
    private final byte[] token;
    private final int socketTimeoutMillis;
    private final AtomicReference<TransportFailure> firstFailure = new AtomicReference<>();
    private final AtomicReference<State> state = new AtomicReference<>(State.NEW);
    private final Object lifecycleLock = new Object();
    private final CountDownLatch workerTerminated = new CountDownLatch(1);
    private final Thread worker;
    private volatile ServerSocket serverSocket;
    private volatile Socket clientSocket;
    private volatile TransportEndpoint endpoint;

    LocalObserverServer(
            ObserverTransportControl control, byte[] token, Duration socketTimeout) {
        this.control = Objects.requireNonNull(control, "control");
        sessionId = Objects.requireNonNull(control.sessionId(), "control sessionId");
        Objects.requireNonNull(token, "token");
        if (token.length != TOKEN_BYTES) {
            throw new IllegalArgumentException("token must contain 32 bytes");
        }
        boolean allZero = true;
        for (byte value : token) {
            allZero &= value == 0;
        }
        if (allZero) {
            throw new IllegalArgumentException("token must not be all zero");
        }
        Objects.requireNonNull(socketTimeout, "socketTimeout");
        long timeoutMillis = socketTimeout.toMillis();
        if (timeoutMillis < 1 || timeoutMillis > 60_000) {
            throw new IllegalArgumentException("socket timeout is invalid");
        }
        this.token = token.clone();
        socketTimeoutMillis = Math.toIntExact(timeoutMillis);
        worker = new Thread(this::run, "hrc-job-observer-local-transport");
        worker.setDaemon(true);
    }

    TransportEndpoint start() throws IOException {
        synchronized (lifecycleLock) {
            if (!state.compareAndSet(State.NEW, State.ACTIVE)) {
                throw new IllegalStateException("transport can start only once");
            }
            ServerSocket listener = null;
            try {
                listener = new ServerSocket();
                listener.bind(new InetSocketAddress(
                        InetAddress.getByName("127.0.0.1"), 0), 1);
                serverSocket = listener;
                endpoint = new TransportEndpoint(
                        PROTOCOL_VERSION,
                        "127.0.0.1",
                        listener.getLocalPort(),
                        sessionId);
                worker.start();
                return endpoint;
            } catch (IOException | RuntimeException failure) {
                abandonStart(listener);
                throw failure;
            } catch (Error failure) {
                abandonStart(listener);
                throw failure;
            }
        }
    }

    TransportEndpoint endpoint() {
        TransportEndpoint current = endpoint;
        if (current == null || state.get() != State.ACTIVE) {
            throw new IllegalStateException("transport is not active");
        }
        return current;
    }

    TransportFailure firstFailure() {
        return firstFailure.get();
    }

    TransportCloseResult closeAndAwait(Duration timeout) {
        Objects.requireNonNull(timeout, "timeout");
        long timeoutNanos;
        try {
            timeoutNanos = timeout.toNanos();
        } catch (ArithmeticException failure) {
            throw new IllegalArgumentException("close timeout is too large", failure);
        }
        if (timeoutNanos <= 0) {
            throw new IllegalArgumentException("close timeout must be positive");
        }
        Socket clientToClose;
        ServerSocket serverToClose;
        synchronized (lifecycleLock) {
            State current = state.get();
            if (current == State.NEW) {
                state.set(State.CLOSED);
                wipeToken();
                workerTerminated.countDown();
                return result(true);
            }
            if (current == State.ACTIVE) {
                state.set(State.CLOSING);
            }
            clientToClose = clientSocket;
            serverToClose = serverSocket;
        }
        closeQuietly(clientToClose);
        closeQuietly(serverToClose);
        boolean terminated = false;
        try {
            terminated = workerTerminated.await(timeoutNanos, TimeUnit.NANOSECONDS);
        } catch (InterruptedException interrupted) {
            Thread.currentThread().interrupt();
        }
        if (!terminated) {
            firstFailure.compareAndSet(null, TransportFailure.SHUTDOWN_TIMEOUT);
            worker.interrupt();
        }
        return result(terminated);
    }

    private void run() {
        try (ServerSocket listener = serverSocket) {
            while (state.get() == State.ACTIVE) {
                Socket accepted;
                try {
                    accepted = listener.accept();
                } catch (IOException failure) {
                    if (state.get() == State.ACTIVE) {
                        firstFailure.compareAndSet(
                                null, TransportFailure.INTERNAL_FAILURE);
                    }
                    return;
                }
                try (Socket socket = accepted) {
                    if (!registerClient(socket)) {
                        return;
                    }
                    requireLoopback(socket);
                    socket.setSoTimeout(socketTimeoutMillis);
                    socket.setTcpNoDelay(true);
                    serve(socket);
                } catch (ProtocolFailure failure) {
                    firstFailure.compareAndSet(null, failure.reason());
                    return;
                } catch (IOException failure) {
                    if (state.get() != State.ACTIVE) {
                        return;
                    }
                    // A disconnected or timed-out client may reconnect and replay.
                } finally {
                    clearClient(accepted);
                }
            }
        } catch (IOException failure) {
            if (state.get() == State.ACTIVE) {
                firstFailure.compareAndSet(null, TransportFailure.INTERNAL_FAILURE);
            }
        } catch (VirtualMachineError | ThreadDeath fatal) {
            firstFailure.compareAndSet(null, TransportFailure.INTERNAL_FAILURE);
            throw fatal;
        } catch (Throwable failure) {
            firstFailure.compareAndSet(null, TransportFailure.INTERNAL_FAILURE);
        } finally {
            synchronized (lifecycleLock) {
                clientSocket = null;
                serverSocket = null;
            }
            wipeToken();
            state.set(State.CLOSED);
            workerTerminated.countDown();
        }
    }

    private void serve(Socket socket) throws IOException, ProtocolFailure {
        BoundedAsciiLineReader reader =
                new BoundedAsciiLineReader(socket.getInputStream(), MAX_FRAME_BYTES);
        BufferedOutputStream output = new BufferedOutputStream(socket.getOutputStream());
        authenticate(reader.readFrame());
        write(output, "READY\t" + PROTOCOL_VERSION + "\t" + sessionId);
        while (state.get() == State.ACTIVE) {
            String frame = reader.readLine();
            if (frame == null) {
                return;
            }
            if (handle(frame, output)) {
                return;
            }
        }
    }

    private boolean handle(String frame, BufferedOutputStream output)
            throws IOException, ProtocolFailure {
        String[] fields = frame.split("\\t", -1);
        if (fields.length == 1 && "BYE".equals(fields[0])) {
            write(output, "BYE");
            return true;
        }
        if (fields.length == 2 && "PING".equals(fields[0])) {
            requireSession(fields[1]);
            write(output, "PONG\t" + sessionId);
            return false;
        }
        if (fields.length == 3 && "CHECKPOINT".equals(fields[0])) {
            requireSession(fields[1]);
            long cursor = parseNonNegativeLong(fields[2]);
            if (!admitControlCall()) {
                return true;
            }
            ObserverCheckpoint checkpoint;
            try {
                checkpoint = Objects.requireNonNull(
                        control.checkpoint(cursor), "checkpoint");
            } catch (RuntimeException failure) {
                throw new ProtocolFailure(TransportFailure.CONTROL_FAILURE);
            }
            if (!sessionId.equals(checkpoint.sessionId())) {
                throw new ProtocolFailure(TransportFailure.SESSION_MISMATCH);
            }
            if (checkpoint.afterSequence() != cursor) {
                throw new ProtocolFailure(TransportFailure.CHECKPOINT_MISMATCH);
            }
            write(output, "CHECKPOINT\t" + CheckpointJson.encode(checkpoint));
            return false;
        }
        if (fields.length == 6 && "ARM".equals(fields[0])) {
            requireSession(fields[1]);
            UUID requestId = parseUuid(fields[2]);
            OperationKind operation = parseOperation(fields[3]);
            String expectedName = decodeName(fields[4]);
            if (!operation.acceptsExpectedName(expectedName)) {
                throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
            }
            long timeoutMillis = parsePositiveLong(fields[5], MAX_ARM_TIMEOUT_MILLIS);
            if (timeoutMillis < MIN_ARM_TIMEOUT_MILLIS) {
                throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
            }
            if (!admitControlCall()) {
                return true;
            }
            ArmOutcome outcome;
            try {
                outcome = Objects.requireNonNull(
                        control.armIfHealthy(
                                requestId,
                                operation,
                                expectedName,
                                Math.multiplyExact(timeoutMillis, 1_000_000L)),
                        "arm outcome");
            } catch (RuntimeException failure) {
                throw new ProtocolFailure(TransportFailure.CONTROL_FAILURE);
            }
            write(output, "ARM\t" + requestId + "\t" + outcome.name());
            return false;
        }
        throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
    }

    void authenticate(byte[] frame) throws ProtocolFailure {
        byte[] supplied = new byte[TOKEN_BYTES];
        byte[] encoded = new byte[ENCODED_TOKEN_BYTES];
        boolean syntaxValid = frame != null
                && frame.length == HELLO_FRAME_BYTES
                && prefixMatches(frame);
        try {
            if (syntaxValid) {
                System.arraycopy(
                        frame, HELLO_PREFIX.length, encoded, 0, encoded.length);
                try {
                    syntaxValid = Base64.getUrlDecoder().decode(
                            encoded, supplied) == TOKEN_BYTES;
                    if (syntaxValid) {
                        byte[] canonical = Base64.getUrlEncoder()
                                .withoutPadding().encode(supplied);
                        try {
                            syntaxValid = MessageDigest.isEqual(
                                    encoded, canonical);
                        } finally {
                            java.util.Arrays.fill(canonical, (byte) 0);
                        }
                    }
                } catch (IllegalArgumentException ignored) {
                    syntaxValid = false;
                }
            }
            if (!syntaxValid || !MessageDigest.isEqual(token, supplied)) {
                throw new ProtocolFailure(
                        TransportFailure.AUTHENTICATION_FAILED);
            }
        } finally {
            java.util.Arrays.fill(encoded, (byte) 0);
            java.util.Arrays.fill(supplied, (byte) 0);
            if (frame != null) {
                java.util.Arrays.fill(frame, (byte) 0);
            }
        }
    }

    private static boolean prefixMatches(byte[] frame) {
        for (int index = 0; index < HELLO_PREFIX.length; index++) {
            if (frame[index] != HELLO_PREFIX[index]) {
                return false;
            }
        }
        return true;
    }

    private void requireSession(String value) throws ProtocolFailure {
        UUID session = parseUuid(value);
        if (!sessionId.equals(session)) {
            throw new ProtocolFailure(TransportFailure.SESSION_MISMATCH);
        }
    }

    private static UUID parseUuid(String value) throws ProtocolFailure {
        try {
            return UUID.fromString(value);
        } catch (IllegalArgumentException failure) {
            throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
        }
    }

    private static OperationKind parseOperation(String value) throws ProtocolFailure {
        try {
            return OperationKind.valueOf(value);
        } catch (IllegalArgumentException failure) {
            throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
        }
    }

    private static long parseNonNegativeLong(String value) throws ProtocolFailure {
        try {
            long parsed = Long.parseLong(value);
            if (parsed < 0) {
                throw new NumberFormatException();
            }
            return parsed;
        } catch (NumberFormatException failure) {
            throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
        }
    }

    private static long parsePositiveLong(String value, long maximum)
            throws ProtocolFailure {
        long parsed = parseNonNegativeLong(value);
        if (parsed == 0 || parsed > maximum) {
            throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
        }
        return parsed;
    }

    private static String decodeName(String value) throws ProtocolFailure {
        try {
            byte[] decoded = Base64.getUrlDecoder().decode(value);
            if (decoded.length == 0 || decoded.length > 300) {
                throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
            }
            String name = new String(decoded, StandardCharsets.UTF_8);
            byte[] roundTrip = name.getBytes(StandardCharsets.UTF_8);
            if (!java.util.Arrays.equals(decoded, roundTrip)) {
                throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
            }
            return name;
        } catch (IllegalArgumentException failure) {
            throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
        }
    }

    private static void requireLoopback(Socket socket) throws ProtocolFailure {
        if (!socket.getInetAddress().isLoopbackAddress()
                || !socket.getLocalAddress().isLoopbackAddress()) {
            throw new ProtocolFailure(TransportFailure.PROTOCOL_VIOLATION);
        }
    }

    private boolean registerClient(Socket socket) {
        synchronized (lifecycleLock) {
            if (state.get() != State.ACTIVE) {
                return false;
            }
            clientSocket = socket;
            return true;
        }
    }

    private void clearClient(Socket socket) {
        synchronized (lifecycleLock) {
            if (clientSocket == socket) {
                clientSocket = null;
            }
        }
    }

    /**
     * Linearises a control call before or after shutdown admission. A call that
     * obtains this lease before CLOSING may finish while shutdown waits for the
     * worker; no call can obtain a lease after CLOSING is visible.
     */
    private boolean admitControlCall() {
        synchronized (lifecycleLock) {
            return state.get() == State.ACTIVE;
        }
    }

    private static void write(BufferedOutputStream output, String frame)
            throws IOException, ProtocolFailure {
        byte[] bytes = frame.getBytes(StandardCharsets.UTF_8);
        if (bytes.length > CheckpointJson.MAX_RESPONSE_BYTES) {
            throw new ProtocolFailure(TransportFailure.SERIALISATION_FAILED);
        }
        output.write(bytes);
        output.write('\n');
        output.flush();
    }

    private void wipeToken() {
        java.util.Arrays.fill(token, (byte) 0);
    }

    private void abandonStart(ServerSocket listener) {
        closeQuietly(listener);
        serverSocket = null;
        endpoint = null;
        state.set(State.CLOSED);
        workerTerminated.countDown();
        wipeToken();
    }

    private static void closeQuietly(AutoCloseable closeable) {
        if (closeable != null) {
            try {
                closeable.close();
            } catch (Exception ignored) {
                // Shutdown health is reported through the worker result.
            }
        }
    }

    private TransportCloseResult result(boolean terminated) {
        return new TransportCloseResult(firstFailure.get(), terminated);
    }
}
