package net.hrcautomation.jobobserver.packaging;

import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Dependency-free synthetic-fixture harness for the offline planner. */
public final class SimpleConfiguratorPackagingTest {
    private static final String INTENDED =
            "net.hrcautomation.jobobserver,0.1.0,"
                    + "plugins/net.hrcautomation.jobobserver_0.1.0.jar,4,true";
    private static final String CONFIG_LF = String.join("\n",
            "# synthetic config fixture",
            "eclipse.application=net.holdemresources.calculator.application",
            "eclipse.product=net.holdemresources.calculator.product",
            "org.eclipse.equinox.simpleconfigurator.configUrl=file\\:org.eclipse.equinox.simpleconfigurator/bundles.info",
            "osgi.bundles=reference\\:file\\:org.eclipse.equinox.simpleconfigurator_1.5.400.v20250129-0942.jar@1\\:start",
            "osgi.bundles.defaultStartLevel=4",
            "synthetic.unrelated=true",
            "");
    private static final String BUNDLES_LF = String.join("\n",
            "#encoding=UTF-8",
            "#version=1",
            "net.holdemresources.calculator,4.1.1.202607211244,plugins/net.holdemresources.calculator_4.1.1.202607211244.jar,5,false",
            "org.eclipse.core.jobs,3.15.500.v20250204-0817,plugins/org.eclipse.core.jobs_3.15.500.v20250204-0817.jar,4,false",
            "org.eclipse.equinox.common,3.20.0.v20250129-1348,plugins/org.eclipse.equinox.common_3.20.0.v20250129-1348.jar,2,true",
            "org.eclipse.equinox.simpleconfigurator,1.5.400.v20250129-0942,plugins/org.eclipse.equinox.simpleconfigurator_1.5.400.v20250129-0942.jar,1,true",
            "org.eclipse.osgi,3.23.0.v20250228-0640,plugins/org.eclipse.osgi_3.23.0.v20250228-0640.jar,-1,true",
            "synthetic.directory,1.2.3,plugins/synthetic.directory_1.2.3/,4,false",
            "synthetic.unrelated,1.2.3,plugins/synthetic.unrelated_1.2.3.jar,4,false",
            "");

    private static int passed;

    private SimpleConfiguratorPackagingTest() {
    }

    public static void main(String[] args) {
        run("recorded policy", SimpleConfiguratorPackagingTest::recordedPolicy);
        run("LF proposal", SimpleConfiguratorPackagingTest::lfProposal);
        run("CRLF proposal without final newline",
                SimpleConfiguratorPackagingTest::crlfProposalWithoutFinalNewline);
        run("determinism and defensive copy",
                SimpleConfiguratorPackagingTest::determinismAndDefensiveCopy);
        run("config hash mismatch", SimpleConfiguratorPackagingTest::configHashMismatch);
        run("bundles hash mismatch", SimpleConfiguratorPackagingTest::bundlesHashMismatch);
        run("config fact mismatch", SimpleConfiguratorPackagingTest::configFactMismatch);
        run("required bundle mismatch", SimpleConfiguratorPackagingTest::requiredBundleMismatch);
        run("existing observer rejected", SimpleConfiguratorPackagingTest::existingObserverRejected);
        run("invalid UTF-8 rejected", SimpleConfiguratorPackagingTest::invalidUtf8Rejected);
        run("mixed line endings rejected", SimpleConfiguratorPackagingTest::mixedLineEndingsRejected);
        run("invalid header rejected", SimpleConfiguratorPackagingTest::invalidHeaderRejected);
        run("unsafe directory location rejected",
                SimpleConfiguratorPackagingTest::unsafeDirectoryLocationRejected);
        System.out.println("SimpleConfiguratorPackagingTest: " + passed + "/13 passed");
    }

    private static void recordedPolicy() {
        SimpleConfiguratorPlanner.Policy policy = SimpleConfiguratorPlanner.recordedPolicy();
        equal("7FB69262C0FCB2C96A605A95B1834C4FC3756724634378E1973C49ACAC0A3C72",
                policy.configSha256());
        equal("A3B776136BAF2323357731CECEEF004C95B2553DB09900979FB277F1FFB2ED41",
                policy.bundlesSha256());
        equal("net.holdemresources.calculator.application",
                policy.requiredProperties().get("eclipse.application"));
        equal("net.holdemresources.calculator.product",
                policy.requiredProperties().get("eclipse.product"));
        equal("file\\:org.eclipse.equinox.simpleconfigurator/bundles.info",
                policy.requiredProperties().get(
                        "org.eclipse.equinox.simpleconfigurator.configUrl"));
        equal("reference\\:file\\:org.eclipse.equinox.simpleconfigurator_1.5.400.v20250129-0942.jar@1\\:start",
                policy.requiredProperties().get("osgi.bundles"));
        equal("4", policy.requiredProperties().get("osgi.bundles.defaultStartLevel"));
        equal(List.of(
                "net.holdemresources.calculator,4.1.1.202607211244,plugins/net.holdemresources.calculator_4.1.1.202607211244.jar,5,false",
                "org.eclipse.core.jobs,3.15.500.v20250204-0817,plugins/org.eclipse.core.jobs_3.15.500.v20250204-0817.jar,4,false",
                "org.eclipse.equinox.common,3.20.0.v20250129-1348,plugins/org.eclipse.equinox.common_3.20.0.v20250129-1348.jar,2,true",
                "org.eclipse.equinox.simpleconfigurator,1.5.400.v20250129-0942,plugins/org.eclipse.equinox.simpleconfigurator_1.5.400.v20250129-0942.jar,1,true",
                "org.eclipse.osgi,3.23.0.v20250228-0640,plugins/org.eclipse.osgi_3.23.0.v20250228-0640.jar,-1,true"),
                policy.requiredRows().stream()
                        .map(SimpleConfiguratorPlanner.BundleRow::line)
                        .toList());
        equal(INTENDED, policy.intendedRow().line());
    }

    private static void lfProposal() {
        byte[] config = bytes(CONFIG_LF);
        byte[] bundles = bytes(BUNDLES_LF);
        SimpleConfiguratorPlanner.Plan plan = SimpleConfiguratorPlanner.plan(
                config, bundles, syntheticPolicy(config, bundles));
        equal(SimpleConfiguratorPlanner.Disposition.OFFLINE_PLAN_ONLY, plan.disposition());
        equal(INTENDED, plan.intendedRow());
        equal(BUNDLES_LF + INTENDED + "\n",
                new String(plan.proposedBundlesInfo(), StandardCharsets.UTF_8));
        equal(SimpleConfiguratorPlanner.sha256(config), plan.configSha256());
        equal(SimpleConfiguratorPlanner.sha256(bundles), plan.sourceBundlesSha256());
        equal(SimpleConfiguratorPlanner.sha256(plan.proposedBundlesInfo()),
                plan.proposedBundlesSha256());
    }

    private static void crlfProposalWithoutFinalNewline() {
        byte[] config = bytes(CONFIG_LF.replace("\n", "\r\n"));
        String source = BUNDLES_LF.substring(0, BUNDLES_LF.length() - 1)
                .replace("\n", "\r\n");
        byte[] bundles = bytes(source);
        SimpleConfiguratorPlanner.Plan plan = SimpleConfiguratorPlanner.plan(
                config, bundles, syntheticPolicy(config, bundles));
        equal(source + "\r\n" + INTENDED + "\r\n",
                new String(plan.proposedBundlesInfo(), StandardCharsets.UTF_8));
    }

    private static void determinismAndDefensiveCopy() {
        byte[] config = bytes(CONFIG_LF);
        byte[] bundles = bytes(BUNDLES_LF);
        SimpleConfiguratorPlanner.Policy policy = syntheticPolicy(config, bundles);
        SimpleConfiguratorPlanner.Plan first = SimpleConfiguratorPlanner.plan(
                config, bundles, policy);
        SimpleConfiguratorPlanner.Plan second = SimpleConfiguratorPlanner.plan(
                config, bundles, policy);
        equal(first.proposedBundlesSha256(), second.proposedBundlesSha256());
        check(Arrays.equals(first.proposedBundlesInfo(), second.proposedBundlesInfo()));
        byte[] exposed = first.proposedBundlesInfo();
        exposed[0] = 'X';
        check(first.proposedBundlesInfo()[0] == '#');
    }

    private static void configHashMismatch() {
        byte[] config = bytes(CONFIG_LF);
        byte[] bundles = bytes(BUNDLES_LF);
        byte[] changed = bytes(CONFIG_LF.replace("synthetic.unrelated=true",
                "synthetic.unrelated=false"));
        fails(SimpleConfiguratorPlanner.Failure.HASH_MISMATCH,
                () -> SimpleConfiguratorPlanner.plan(
                        changed, bundles, syntheticPolicy(config, bundles)));
    }

    private static void bundlesHashMismatch() {
        byte[] config = bytes(CONFIG_LF);
        byte[] bundles = bytes(BUNDLES_LF);
        byte[] changed = bytes(BUNDLES_LF.replace(
                "synthetic.unrelated,1.2.3", "synthetic.unrelated,1.2.4"));
        fails(SimpleConfiguratorPlanner.Failure.HASH_MISMATCH,
                () -> SimpleConfiguratorPlanner.plan(
                        config, changed, syntheticPolicy(config, bundles)));
    }

    private static void configFactMismatch() {
        byte[] config = bytes(CONFIG_LF.replace(
                "osgi.bundles.defaultStartLevel=4",
                "osgi.bundles.defaultStartLevel=5"));
        byte[] bundles = bytes(BUNDLES_LF);
        fails(SimpleConfiguratorPlanner.Failure.RECORDED_FACT_MISMATCH,
                () -> SimpleConfiguratorPlanner.plan(
                        config, bundles, syntheticPolicy(config, bundles)));
    }

    private static void requiredBundleMismatch() {
        byte[] config = bytes(CONFIG_LF);
        byte[] bundles = bytes(BUNDLES_LF.replace(
                "org.eclipse.core.jobs,3.15.500.v20250204-0817",
                "org.eclipse.core.jobs,3.15.501.v20250204-0817"));
        fails(SimpleConfiguratorPlanner.Failure.RECORDED_FACT_MISMATCH,
                () -> SimpleConfiguratorPlanner.plan(
                        config, bundles, syntheticPolicy(config, bundles)));
    }

    private static void existingObserverRejected() {
        byte[] config = bytes(CONFIG_LF);
        byte[] bundles = bytes(BUNDLES_LF + INTENDED + "\n");
        fails(SimpleConfiguratorPlanner.Failure.OBSERVER_ALREADY_PRESENT,
                () -> SimpleConfiguratorPlanner.plan(
                        config, bundles, syntheticPolicy(config, bundles)));
    }

    private static void invalidUtf8Rejected() {
        byte[] config = {(byte) 0xC3, (byte) 0x28, (byte) '\n'};
        byte[] bundles = bytes(BUNDLES_LF);
        fails(SimpleConfiguratorPlanner.Failure.INVALID_UTF8,
                () -> SimpleConfiguratorPlanner.plan(
                        config, bundles, syntheticPolicy(config, bundles)));
    }

    private static void mixedLineEndingsRejected() {
        byte[] config = bytes(CONFIG_LF.replaceFirst("\n", "\r\n"));
        byte[] bundles = bytes(BUNDLES_LF);
        fails(SimpleConfiguratorPlanner.Failure.INVALID_LINE_ENDING,
                () -> SimpleConfiguratorPlanner.plan(
                        config, bundles, syntheticPolicy(config, bundles)));
    }

    private static void invalidHeaderRejected() {
        byte[] config = bytes(CONFIG_LF);
        byte[] bundles = bytes(BUNDLES_LF.replace(
                "#version=1", "#version=2"));
        fails(SimpleConfiguratorPlanner.Failure.INVALID_BUNDLES_INFO,
                () -> SimpleConfiguratorPlanner.plan(
                        config, bundles, syntheticPolicy(config, bundles)));
    }

    private static void unsafeDirectoryLocationRejected() {
        byte[] config = bytes(CONFIG_LF);
        byte[] bundles = bytes(BUNDLES_LF.replace(
                "plugins/synthetic.directory_1.2.3/",
                "plugins/../synthetic.directory_1.2.3/"));
        fails(SimpleConfiguratorPlanner.Failure.INVALID_BUNDLES_INFO,
                () -> SimpleConfiguratorPlanner.plan(
                        config, bundles, syntheticPolicy(config, bundles)));
    }

    private static SimpleConfiguratorPlanner.Policy syntheticPolicy(
            byte[] config, byte[] bundles) {
        SimpleConfiguratorPlanner.Policy recorded = SimpleConfiguratorPlanner.recordedPolicy();
        Map<String, String> properties = new LinkedHashMap<>(
                recorded.requiredProperties());
        List<SimpleConfiguratorPlanner.BundleRow> rows = List.copyOf(
                recorded.requiredRows());
        return new SimpleConfiguratorPlanner.Policy(
                SimpleConfiguratorPlanner.sha256(config),
                SimpleConfiguratorPlanner.sha256(bundles),
                properties,
                rows,
                recorded.intendedRow());
    }

    private static byte[] bytes(String value) {
        return value.getBytes(StandardCharsets.UTF_8);
    }

    private static void run(String name, Runnable test) {
        try {
            test.run();
            passed++;
        } catch (RuntimeException | AssertionError exception) {
            throw new AssertionError("Failed: " + name, exception);
        }
    }

    private static void fails(SimpleConfiguratorPlanner.Failure expected, Runnable action) {
        try {
            action.run();
        } catch (SimpleConfiguratorPlanner.PlanException exception) {
            equal(expected, exception.failure());
            equal(expected.name(), exception.getMessage());
            return;
        }
        throw new AssertionError("Expected failure: " + expected);
    }

    private static void check(boolean condition) {
        if (!condition) {
            throw new AssertionError("Check failed");
        }
    }

    private static void equal(Object expected, Object actual) {
        if (!expected.equals(actual)) {
            throw new AssertionError("Expected " + expected + " but got " + actual);
        }
    }
}
