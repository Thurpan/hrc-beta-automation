package net.hrcautomation.jobobserver.startlevelfixture;

import java.nio.file.Path;
import java.time.Duration;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.ServiceLoader;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;
import org.osgi.framework.Bundle;
import org.osgi.framework.BundleContext;
import org.osgi.framework.Constants;
import org.osgi.framework.FrameworkEvent;
import org.osgi.framework.FrameworkListener;
import org.osgi.framework.launch.Framework;
import org.osgi.framework.launch.FrameworkFactory;
import org.osgi.framework.startlevel.BundleStartLevel;
import org.osgi.framework.startlevel.FrameworkStartLevel;
import org.osgi.framework.wiring.BundleRequirement;
import org.osgi.framework.wiring.BundleRevision;
import org.osgi.framework.wiring.FrameworkWiring;

/**
 * Starts one clean synthetic Equinox framework. The build invokes this class
 * in a fresh JVM for each scenario.
 */
public final class EquinoxStartLevelFixtureTest {
    private static final int OBSERVER_LEVEL = 4;
    private static final int PRODUCER_LEVEL = 5;
    private static final Duration WAIT = Duration.ofSeconds(10);

    private EquinoxStartLevelFixtureTest() {
    }

    public static void main(String[] args) throws Exception {
        Arguments arguments = Arguments.parse(args);
        boolean failureScenario = arguments.scenario() == Scenario.OBSERVER_FAILURE;
        FixtureProbe.reset(failureScenario);
        ScenarioResult result = runFramework(arguments);

        List<TestCase> tests = switch (arguments.scenario()) {
            case PREREQUISITE_SUCCESS -> successTests(result);
            case RECORDED_PROVIDER_ROWS -> recordedProviderRowsTests(result);
            case OBSERVER_FAILURE -> failureTests(result);
        };
        int passed = 0;
        for (TestCase test : tests) {
            test.body().run();
            passed++;
            System.out.println("PASS " + test.name());
        }
        System.out.println("PASS " + passed + "/" + tests.size());
    }

    private static ScenarioResult runFramework(Arguments arguments) throws Exception {
        FrameworkFactory factory = ServiceLoader.load(FrameworkFactory.class)
                .findFirst()
                .orElseThrow(() -> new IllegalStateException(
                        "No public OSGi FrameworkFactory provider found"));
        Map<String, String> configuration = new HashMap<>();
        configuration.put(Constants.FRAMEWORK_STORAGE,
                arguments.storage().toAbsolutePath().toString());
        configuration.put(Constants.FRAMEWORK_STORAGE_CLEAN,
                Constants.FRAMEWORK_STORAGE_CLEAN_ONFIRSTINIT);
        configuration.put(Constants.FRAMEWORK_BEGINNING_STARTLEVEL, "1");
        configuration.put(Constants.FRAMEWORK_BUNDLE_PARENT,
                Constants.FRAMEWORK_BUNDLE_PARENT_APP);
        configuration.put(Constants.FRAMEWORK_SYSTEMPACKAGES_EXTRA,
                FixtureProbe.class.getPackageName() + ";version=1.0.0");

        Framework framework = factory.newFramework(configuration);
        Bundle observer = null;
        Bundle producer = null;
        List<String> atLevelFive = List.of();
        int observerState = -1;
        int producerState = -1;
        int reachedLevel = -1;
        FrameworkDiagnostics diagnostics = new FrameworkDiagnostics();
        try {
            framework.init();
            BundleContext context = Objects.requireNonNull(
                    framework.getBundleContext(), "framework context");
            context.addFrameworkListener(diagnostics);
            framework.start();
            Bundle common = install(context, arguments.common());
            Bundle jobs = install(context, arguments.jobs());
            Bundle coreRuntime = install(context, arguments.coreRuntime());
            Bundle contentType = install(context, arguments.contentType());
            Bundle app = install(context, arguments.app());
            Bundle preferences = install(context, arguments.preferences());
            Bundle registry = install(context, arguments.registry());
            Bundle prefsService = install(context, arguments.prefsService());
            observer = install(context, arguments.observer());
            producer = install(context, arguments.producer());

            setBundleStartLevel(common, 2);
            int jobsLevel = arguments.scenario() == Scenario.RECORDED_PROVIDER_ROWS
                    ? 4 : 3;
            setBundleStartLevel(jobs, jobsLevel);
            setBundleStartLevel(coreRuntime, 4);
            setBundleStartLevel(contentType, 4);
            setBundleStartLevel(app, 4);
            setBundleStartLevel(preferences, 4);
            setBundleStartLevel(registry, 4);
            setBundleStartLevel(prefsService, 4);
            setBundleStartLevel(observer, OBSERVER_LEVEL);
            setBundleStartLevel(producer, PRODUCER_LEVEL);
            if (arguments.scenario() == Scenario.RECORDED_PROVIDER_ROWS) {
                clearPersistentStart(jobs);
                clearPersistentStart(contentType);
                clearPersistentStart(app);
                clearPersistentStart(preferences);
                clearPersistentStart(registry);
                clearPersistentStart(prefsService);
                FixtureProbe.providerStateAt(
                        "PRE_RESOLVE",
                        jobs.getSymbolicName(),
                        jobs.getState(),
                        bundleLevel(jobs),
                        persistentlyStarted(jobs));
            }
            requireResolved(framework, List.of(
                    common, jobs, coreRuntime, contentType, app, preferences,
                    registry, prefsService, observer, producer));
            if (arguments.scenario() == Scenario.RECORDED_PROVIDER_ROWS) {
                FixtureProbe.providerStateAt(
                        "POST_RESOLVE",
                        jobs.getSymbolicName(),
                        jobs.getState(),
                        bundleLevel(jobs),
                        persistentlyStarted(jobs));
            }
            common.start();
            if (arguments.scenario() != Scenario.RECORDED_PROVIDER_ROWS) {
                jobs.start();
            }
            if (arguments.scenario() == Scenario.RECORDED_PROVIDER_ROWS) {
                FixtureProbe.providerStateAt(
                        "PRE_RUNTIME",
                        jobs.getSymbolicName(),
                        jobs.getState(),
                        bundleLevel(jobs),
                        persistentlyStarted(jobs));
                coreRuntime.start();
            }
            observer.start();
            producer.start();

            FrameworkStartLevel frameworkLevel = Objects.requireNonNull(
                    framework.adapt(FrameworkStartLevel.class),
                    "framework start level");
            awaitStartLevel(frameworkLevel, PRODUCER_LEVEL);
            reachedLevel = frameworkLevel.getStartLevel();
            observerState = observer.getState();
            producerState = producer.getState();
            atLevelFive = FixtureProbe.events();

            for (NoRuntimeUnloadPolicy.ForbiddenAction action
                    : NoRuntimeUnloadPolicy.ForbiddenAction.values()) {
                assertFalse(FixtureProbe.policy().request(action));
            }
        } finally {
            if (framework.getState() == Bundle.ACTIVE
                    || framework.getState() == Bundle.STARTING) {
                framework.stop();
                FrameworkEvent stopped = framework.waitForStop(WAIT.toMillis());
                if (stopped.getType() != FrameworkEvent.STOPPED) {
                    throw new AssertionError(
                            "Framework did not stop cleanly: " + stopped.getType());
                }
            }
        }

        for (NoRuntimeUnloadPolicy.ForbiddenAction action
                : NoRuntimeUnloadPolicy.ForbiddenAction.values()) {
            assertFalse(FixtureProbe.policy().request(action));
        }
        return new ScenarioResult(
                reachedLevel,
                observerState,
                producerState,
                atLevelFive,
                FixtureProbe.events(),
                FixtureProbe.policy(),
                FixtureProbe.listenerIsRegistered(),
                FixtureProbe.publicationCount(),
                FixtureProbe.controllerAcceptances(),
                FixtureProbe.controllerRefusals(),
                diagnostics.values());
    }

    private static Bundle install(BundleContext context, Path bundlePath)
            throws Exception {
        return context.installBundle(bundlePath.toUri().toString());
    }

    private static void setBundleStartLevel(Bundle bundle, int level) {
        BundleStartLevel startLevel = Objects.requireNonNull(
                bundle.adapt(BundleStartLevel.class), "bundle start level");
        startLevel.setStartLevel(level);
        assertEquals(level, startLevel.getStartLevel());
    }

    private static int bundleLevel(Bundle bundle) {
        return Objects.requireNonNull(
                bundle.adapt(BundleStartLevel.class), "bundle start level")
                .getStartLevel();
    }

    private static boolean persistentlyStarted(Bundle bundle) {
        return Objects.requireNonNull(
                bundle.adapt(BundleStartLevel.class), "bundle start level")
                .isPersistentlyStarted();
    }

    private static void clearPersistentStart(Bundle bundle) throws Exception {
        if (persistentlyStarted(bundle)) {
            bundle.stop();
        }
        assertFalse(persistentlyStarted(bundle));
    }

    private static void requireResolved(Framework framework, List<Bundle> bundles) {
        FrameworkWiring wiring = Objects.requireNonNull(
                framework.adapt(FrameworkWiring.class), "framework wiring");
        if (wiring.resolveBundles(bundles)) {
            return;
        }
        List<String> diagnostics = new ArrayList<>();
        for (Bundle bundle : bundles) {
            if (bundle.getState() != Bundle.INSTALLED) {
                continue;
            }
            BundleRevision revision = Objects.requireNonNull(
                    bundle.adapt(BundleRevision.class), "bundle revision");
            diagnostics.add(bundle.getSymbolicName() + ":state=" + bundle.getState());
            for (BundleRequirement requirement
                    : revision.getDeclaredRequirements(null)) {
                diagnostics.add("  " + requirement.getNamespace()
                        + ":providers=" + wiring.findProviders(requirement).size()
                        + ":directives=" + requirement.getDirectives()
                        + ":attributes=" + requirement.getAttributes());
            }
        }
        throw new AssertionError("Bundle resolution failed: " + diagnostics);
    }

    private static void awaitStartLevel(
            FrameworkStartLevel startLevel, int requested) throws Exception {
        CountDownLatch reached = new CountDownLatch(1);
        AtomicReference<FrameworkEvent> event = new AtomicReference<>();
        startLevel.setStartLevel(requested, value -> {
            event.set(value);
            reached.countDown();
        });
        if (!reached.await(WAIT.toMillis(), TimeUnit.MILLISECONDS)) {
            throw new AssertionError("Timed out awaiting framework start level");
        }
        FrameworkEvent completed = Objects.requireNonNull(
                event.get(), "start-level event");
        assertEquals(FrameworkEvent.STARTLEVEL_CHANGED, completed.getType());
    }

    private static List<TestCase> successTests(ScenarioResult result) {
        List<TestCase> tests = new ArrayList<>();
        tests.add(test("framework reached level five", () ->
                assertEquals(PRODUCER_LEVEL, result.frameworkLevel())));
        tests.add(test("observer and producer activated", () -> {
            assertEquals(Bundle.ACTIVE, result.observerStateAtLevelFive());
            assertEquals(Bundle.ACTIVE, result.producerStateAtLevelFive());
        }));
        tests.add(test("successful advancement emitted no framework error", () ->
                assertTrue(result.frameworkDiagnostics().isEmpty())));
        tests.add(test("level four publication precedes level five producer", () ->
                assertOrdered(result.eventsAtLevelFive(),
                        "OBSERVER_START:L4:F4",
                        "LISTENER_REGISTERED",
                        "PUBLICATION_ACTIVE",
                        "PRODUCER_START:L5:F5")));
        tests.add(test("controller admitted only after publication", () -> {
            assertOrdered(result.eventsAtLevelFive(),
                    "PUBLICATION_ACTIVE", "CONTROLLER_ACCEPTED");
            assertEquals(1, result.controllerAcceptances());
            assertEquals(0, result.controllerRefusals());
        }));
        tests.add(test("immediate Job lifecycle captured", () ->
                assertOrdered(result.eventsAtLevelFive(),
                        "JOB_SCHEDULED", "JOB_RUNNING", "JOB_RUN",
                        "JOB_DONE", "JOB_JOINED_OK")));
        tests.add(test("publication was unique", () ->
                assertEquals(1, result.publicationCount())));
        tests.add(test("terminal stop closes before removal and revocation", () ->
                assertOrdered(result.finalEvents(),
                        "ADMISSION_CLOSED", "LISTENER_REMOVED",
                        "CALLBACK_REJECTED:STALE", "PUBLICATION_REVOKED",
                        "OBSERVER_TERMINAL")));
        tests.add(test("stale callback was rejected", () -> {
            assertEquals(1, result.policy().rejectedCallbacks());
            assertEquals(3, result.policy().admittedCallbacks());
        }));
        tests.add(test("publication revocation is terminal", () -> {
            assertEquals(NoRuntimeUnloadPolicy.State.TERMINAL,
                    result.policy().state());
            assertFalse(result.policy().publicationActive());
            assertFalse(result.policy().admissionOpen());
            assertFalse(result.listenerRegistered());
        }));
        tests.add(test("same-JVM runtime mutations refused", () -> {
            for (NoRuntimeUnloadPolicy.ForbiddenAction action
                    : NoRuntimeUnloadPolicy.ForbiddenAction.values()) {
                assertEquals(2, result.policy().refusalCount(action));
            }
        }));
        tests.add(test("terminal lifecycle cannot restart or republish", () -> {
            assertThrows(IllegalStateException.class, result.policy()::beginStart);
            assertThrows(IllegalStateException.class,
                    result.policy()::activatePublication);
            assertEquals(1, result.publicationCount());
        }));
        return List.copyOf(tests);
    }

    private static List<TestCase> recordedProviderRowsTests(ScenarioResult result) {
        List<TestCase> tests = new ArrayList<>(successTests(result));
        tests.add(test("recorded dependency rows observed at observer start", () -> {
            assertContains(result.eventsAtLevelFive(),
                    "PROVIDER:org.eclipse.equinox.common:S32:L2:Ptrue");
            assertContains(result.eventsAtLevelFive(),
                    "PROVIDER:org.eclipse.core.contenttype:S4:L4:Pfalse");
            assertContains(result.eventsAtLevelFive(),
                    "PROVIDER:org.eclipse.equinox.app:S4:L4:Pfalse");
            assertContains(result.eventsAtLevelFive(),
                    "PROVIDER:org.eclipse.equinox.preferences:S4:L4:Pfalse");
            assertContains(result.eventsAtLevelFive(),
                    "PROVIDER:org.eclipse.equinox.registry:S4:L4:Pfalse");
            assertContains(result.eventsAtLevelFive(),
                    "PROVIDER:org.osgi.service.prefs:S4:L4:Pfalse");
        }));
        tests.add(test("Core Jobs row began installed and non-persistent", () ->
                assertContains(result.eventsAtLevelFive(),
                        "PRE_RESOLVE:org.eclipse.core.jobs:S2:L4:Pfalse")));
        tests.add(test("resolution preserved Core Jobs non-persistent state", () ->
                assertContains(result.eventsAtLevelFive(),
                        "POST_RESOLVE:org.eclipse.core.jobs:S4:L4:Pfalse")));
        tests.add(test("same-level Common start preserved Core Jobs row", () ->
                assertContains(result.eventsAtLevelFive(),
                        "PRE_RUNTIME:org.eclipse.core.jobs:S4:L4:Pfalse")));
        tests.add(test("recorded provider rows visible at observer start", () -> {
            assertContains(result.eventsAtLevelFive(),
                    "PROVIDER:org.eclipse.core.jobs:S4:L4:Pfalse");
            assertContains(result.eventsAtLevelFive(),
                    "PROVIDER:org.eclipse.core.runtime:S32:L4:Ptrue");
        }));
        tests.add(test("same-level providers precede publication", () ->
                assertOrdered(result.eventsAtLevelFive(),
                        "PRE_RESOLVE:org.eclipse.core.jobs:S2:L4:Pfalse",
                        "POST_RESOLVE:org.eclipse.core.jobs:S4:L4:Pfalse",
                        "PRE_RUNTIME:org.eclipse.core.jobs:S4:L4:Pfalse",
                        "OBSERVER_START:L4:F4",
                        "PROVIDER:org.eclipse.equinox.common:S32:L2:Ptrue",
                        "PROVIDER:org.eclipse.core.contenttype:S4:L4:Pfalse",
                        "PROVIDER:org.eclipse.equinox.app:S4:L4:Pfalse",
                        "PROVIDER:org.eclipse.equinox.preferences:S4:L4:Pfalse",
                        "PROVIDER:org.eclipse.equinox.registry:S4:L4:Pfalse",
                        "PROVIDER:org.osgi.service.prefs:S4:L4:Pfalse",
                        "PROVIDER:org.eclipse.core.jobs:S4:L4:Pfalse",
                        "PROVIDER:org.eclipse.core.runtime:S32:L4:Ptrue",
                        "LISTENER_REGISTERED", "PUBLICATION_ACTIVE",
                        "PRODUCER_START:L5:F5")));
        return List.copyOf(tests);
    }

    private static List<TestCase> failureTests(ScenarioResult result) {
        List<TestCase> tests = new ArrayList<>();
        tests.add(test("framework advanced despite observer failure", () ->
                assertEquals(PRODUCER_LEVEL, result.frameworkLevel())));
        tests.add(test("failed observer did not activate", () -> {
            assertEquals(Bundle.RESOLVED, result.observerStateAtLevelFive());
            assertEquals(Bundle.ACTIVE, result.producerStateAtLevelFive());
        }));
        tests.add(test("activation failure emitted a BundleException error", () ->
                assertTrue(result.frameworkDiagnostics().stream().anyMatch(value ->
                        value.type() == FrameworkEvent.ERROR
                                && "net.hrcautomation.jobobserver.startlevelfixture.observer"
                                        .equals(value.bundleSymbolicName())
                                && "org.osgi.framework.BundleException"
                                        .equals(value.throwableClass())))));
        tests.add(test("level five producer followed failed level four start", () ->
                assertOrdered(result.eventsAtLevelFive(),
                        "OBSERVER_START:L4:F4", "OBSERVER_START_FAILED",
                        "PRODUCER_START:L5:F5")));
        tests.add(test("failed observer never published", () -> {
            assertEquals(0, result.publicationCount());
            assertFalse(result.eventsAtLevelFive().contains("LISTENER_REGISTERED"));
            assertFalse(result.eventsAtLevelFive().contains("PUBLICATION_ACTIVE"));
            assertFalse(result.listenerRegistered());
        }));
        tests.add(test("controller refused without publication", () -> {
            assertEquals(0, result.controllerAcceptances());
            assertEquals(1, result.controllerRefusals());
            assertTrue(result.eventsAtLevelFive().contains("CONTROLLER_REFUSED"));
        }));
        tests.add(test("refused producer scheduled no Job", () -> {
            for (String event : result.eventsAtLevelFive()) {
                assertFalse(event.startsWith("JOB_"));
            }
            assertEquals(0, result.policy().admittedCallbacks());
        }));
        tests.add(test("failed start is terminal", () -> {
            assertEquals(NoRuntimeUnloadPolicy.State.TERMINAL,
                    result.policy().state());
            assertFalse(result.policy().publicationActive());
            assertFalse(result.policy().admissionOpen());
        }));
        tests.add(test("failed JVM refuses runtime mutations", () -> {
            for (NoRuntimeUnloadPolicy.ForbiddenAction action
                    : NoRuntimeUnloadPolicy.ForbiddenAction.values()) {
                assertEquals(2, result.policy().refusalCount(action));
            }
            assertThrows(IllegalStateException.class, result.policy()::beginStart);
            assertThrows(IllegalStateException.class,
                    result.policy()::activatePublication);
        }));
        return List.copyOf(tests);
    }

    private static TestCase test(String name, ThrowingRunnable body) {
        return new TestCase(name, body);
    }

    private static void assertOrdered(List<String> events, String... expected) {
        int previous = -1;
        for (String value : expected) {
            int position = events.indexOf(value);
            if (position <= previous) {
                throw new AssertionError(
                        "Expected ordered event " + value + " in " + events);
            }
            previous = position;
        }
    }

    private static void assertTrue(boolean condition) {
        if (!condition) {
            throw new AssertionError("Expected true");
        }
    }

    private static void assertContains(List<String> values, String expected) {
        if (!values.contains(expected)) {
            throw new AssertionError("Expected " + expected + " in " + values);
        }
    }

    private static void assertFalse(boolean condition) {
        if (condition) {
            throw new AssertionError("Expected false");
        }
    }

    private static void assertEquals(Object expected, Object actual) {
        if (!Objects.equals(expected, actual)) {
            throw new AssertionError(
                    "Expected " + expected + " but got " + actual);
        }
    }

    private static void assertThrows(
            Class<? extends Throwable> expected, ThrowingRunnable action) {
        try {
            action.run();
        } catch (Throwable failure) {
            if (expected.isInstance(failure)) {
                return;
            }
            throw new AssertionError(
                    "Expected " + expected.getName() + " but got " + failure,
                    failure);
        }
        throw new AssertionError("Expected " + expected.getName());
    }

    private enum Scenario {
        PREREQUISITE_SUCCESS("prerequisite-success"),
        RECORDED_PROVIDER_ROWS("recorded-provider-rows"),
        OBSERVER_FAILURE("observer-failure");

        private final String argument;

        Scenario(String argument) {
            this.argument = argument;
        }

        private static Scenario parse(String value) {
            for (Scenario scenario : values()) {
                if (scenario.argument.equals(value)) {
                    return scenario;
                }
            }
            throw new IllegalArgumentException("Unknown scenario: " + value);
        }
    }

    private record Arguments(
            Scenario scenario,
            Path storage,
            Path common,
            Path jobs,
            Path coreRuntime,
            Path contentType,
            Path app,
            Path preferences,
            Path registry,
            Path prefsService,
            Path observer,
            Path producer) {
        private Arguments {
            Objects.requireNonNull(scenario, "scenario");
            Objects.requireNonNull(storage, "storage");
            Objects.requireNonNull(common, "common");
            Objects.requireNonNull(jobs, "jobs");
            Objects.requireNonNull(coreRuntime, "coreRuntime");
            Objects.requireNonNull(contentType, "contentType");
            Objects.requireNonNull(app, "app");
            Objects.requireNonNull(preferences, "preferences");
            Objects.requireNonNull(registry, "registry");
            Objects.requireNonNull(prefsService, "prefsService");
            Objects.requireNonNull(observer, "observer");
            Objects.requireNonNull(producer, "producer");
        }

        private static Arguments parse(String[] args) {
            if (args.length != 12) {
                throw new IllegalArgumentException(
                        "Expected scenario, storage, common, jobs, core runtime, "
                                + "content type, app, preferences, registry, prefs service, "
                                + "observer, producer");
            }
            return new Arguments(
                    Scenario.parse(args[0]),
                    Path.of(args[1]),
                    Path.of(args[2]),
                    Path.of(args[3]),
                    Path.of(args[4]),
                    Path.of(args[5]),
                    Path.of(args[6]),
                    Path.of(args[7]),
                    Path.of(args[8]),
                    Path.of(args[9]),
                    Path.of(args[10]),
                    Path.of(args[11]));
        }
    }

    private record ScenarioResult(
            int frameworkLevel,
            int observerStateAtLevelFive,
            int producerStateAtLevelFive,
            List<String> eventsAtLevelFive,
            List<String> finalEvents,
            NoRuntimeUnloadPolicy policy,
            boolean listenerRegistered,
            int publicationCount,
            int controllerAcceptances,
            int controllerRefusals,
            List<FrameworkDiagnostic> frameworkDiagnostics) {
        private ScenarioResult {
            eventsAtLevelFive = List.copyOf(eventsAtLevelFive);
            finalEvents = List.copyOf(finalEvents);
            Objects.requireNonNull(policy, "policy");
            frameworkDiagnostics = List.copyOf(frameworkDiagnostics);
        }
    }

    private record FrameworkDiagnostic(
            int type,
            String bundleSymbolicName,
            String throwableClass) {
    }

    private static final class FrameworkDiagnostics implements FrameworkListener {
        private final List<FrameworkDiagnostic> values = new ArrayList<>();

        @Override
        public synchronized void frameworkEvent(FrameworkEvent event) {
            if (event.getType() != FrameworkEvent.ERROR) {
                return;
            }
            Bundle bundle = event.getBundle();
            Throwable throwable = event.getThrowable();
            values.add(new FrameworkDiagnostic(
                    event.getType(),
                    bundle == null ? null : bundle.getSymbolicName(),
                    throwable == null ? null : throwable.getClass().getName()));
        }

        private synchronized List<FrameworkDiagnostic> values() {
            return List.copyOf(values);
        }
    }

    private record TestCase(String name, ThrowingRunnable body) {
    }

    @FunctionalInterface
    private interface ThrowingRunnable {
        void run() throws Exception;
    }
}
