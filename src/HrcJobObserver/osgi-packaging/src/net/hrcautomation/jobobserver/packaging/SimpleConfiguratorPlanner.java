package net.hrcautomation.jobobserver.packaging;

import java.nio.ByteBuffer;
import java.nio.charset.CharacterCodingException;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.HexFormat;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.regex.Pattern;

/**
 * Validates the recorded simpleconfigurator baseline and produces an in-memory
 * proposal. This class deliberately has no filesystem, process, OSGi, or
 * installation API.
 */
public final class SimpleConfiguratorPlanner {
    private static final int MAX_INPUT_BYTES = 4 * 1024 * 1024;
    private static final Pattern HASH = Pattern.compile("[0-9A-F]{64}");
    private static final Pattern SYMBOLIC_NAME = Pattern.compile(
            "[A-Za-z0-9_-]+(?:\\.[A-Za-z0-9_-]+)*");
    private static final Pattern VERSION = Pattern.compile(
            "[0-9]+(?:\\.[0-9]+){2}(?:\\.[A-Za-z0-9_-]+)?");
    private static final Pattern LOCATION = Pattern.compile(
            "plugins/[A-Za-z0-9._-]+(?:\\.jar|/)");

    private static final String RECORDED_CONFIG_SHA256 =
            "7FB69262C0FCB2C96A605A95B1834C4FC3756724634378E1973C49ACAC0A3C72";
    private static final String RECORDED_BUNDLES_SHA256 =
            "A3B776136BAF2323357731CECEEF004C95B2553DB09900979FB277F1FFB2ED41";
    private static final BundleRow INTENDED_ROW = BundleRow.parse(
            "net.hrcautomation.jobobserver,0.1.0,"
                    + "plugins/net.hrcautomation.jobobserver_0.1.0.jar,4,true");

    private SimpleConfiguratorPlanner() {
    }

    /**
     * Validates bytes supplied by the caller against the recorded baseline and
     * returns an offline proposal. It never reads or writes a path.
     *
     * @param configIni exact bytes of the candidate {@code config.ini}
     * @param bundlesInfo exact bytes of the candidate {@code bundles.info}
     * @return immutable metadata and a defensive copy of the proposed bytes
     * @throws PlanException when either input differs from the recorded baseline
     */
    public static Plan plan(byte[] configIni, byte[] bundlesInfo) {
        return plan(configIni, bundlesInfo, recordedPolicy());
    }

    static Plan plan(byte[] configIni, byte[] bundlesInfo, Policy policy) {
        Objects.requireNonNull(configIni, "configIni");
        Objects.requireNonNull(bundlesInfo, "bundlesInfo");
        Objects.requireNonNull(policy, "policy");
        checkSize(configIni);
        checkSize(bundlesInfo);

        String configHash = sha256(configIni);
        String bundlesHash = sha256(bundlesInfo);
        if (!configHash.equals(policy.configSha256())
                || !bundlesHash.equals(policy.bundlesSha256())) {
            throw new PlanException(Failure.HASH_MISMATCH);
        }

        TextDocument configDocument = decode(configIni);
        Map<String, String> properties = parseProperties(configDocument);
        for (Map.Entry<String, String> required : policy.requiredProperties().entrySet()) {
            if (!required.getValue().equals(properties.get(required.getKey()))) {
                throw new PlanException(Failure.RECORDED_FACT_MISMATCH);
            }
        }

        int defaultStartLevel;
        try {
            defaultStartLevel = Integer.parseInt(
                    properties.get("osgi.bundles.defaultStartLevel"));
        } catch (NumberFormatException exception) {
            throw new PlanException(Failure.RECORDED_FACT_MISMATCH);
        }
        if (defaultStartLevel != policy.intendedRow().startLevel()
                || !policy.intendedRow().autoStart()) {
            throw new PlanException(Failure.RECORDED_FACT_MISMATCH);
        }

        TextDocument bundlesDocument = decode(bundlesInfo);
        List<BundleRow> rows = parseBundles(bundlesDocument);
        for (BundleRow required : policy.requiredRows()) {
            if (!rows.contains(required)) {
                throw new PlanException(Failure.RECORDED_FACT_MISMATCH);
            }
        }
        if (rows.stream().anyMatch(row -> row.symbolicName().equals(
                policy.intendedRow().symbolicName()))) {
            throw new PlanException(Failure.OBSERVER_ALREADY_PRESENT);
        }

        StringBuilder proposedText = new StringBuilder(bundlesDocument.text());
        if (!bundlesDocument.endsWithLineEnding()) {
            proposedText.append(bundlesDocument.lineEnding());
        }
        proposedText.append(policy.intendedRow().line())
                .append(bundlesDocument.lineEnding());
        byte[] proposed = proposedText.toString().getBytes(StandardCharsets.UTF_8);
        checkSize(proposed);

        List<BundleRow> proposedRows = parseBundles(decode(proposed));
        long intendedCount = proposedRows.stream()
                .filter(policy.intendedRow()::equals)
                .count();
        if (intendedCount != 1) {
            throw new PlanException(Failure.INVALID_BUNDLES_INFO);
        }

        return new Plan(
                configHash,
                bundlesHash,
                sha256(proposed),
                policy.intendedRow().line(),
                proposed,
                Disposition.OFFLINE_PLAN_ONLY);
    }

    static Policy recordedPolicy() {
        Map<String, String> properties = Map.of(
                "eclipse.application",
                "net.holdemresources.calculator.application",
                "eclipse.product",
                "net.holdemresources.calculator.product",
                "org.eclipse.equinox.simpleconfigurator.configUrl",
                "file\\:org.eclipse.equinox.simpleconfigurator/bundles.info",
                "osgi.bundles",
                "reference\\:file\\:org.eclipse.equinox.simpleconfigurator_1.5.400.v20250129-0942.jar@1\\:start",
                "osgi.bundles.defaultStartLevel",
                "4");
        List<BundleRow> rows = List.of(
                BundleRow.parse(
                        "net.holdemresources.calculator,4.1.1.202607211244,"
                                + "plugins/net.holdemresources.calculator_4.1.1.202607211244.jar,5,false"),
                BundleRow.parse(
                        "org.eclipse.core.jobs,3.15.500.v20250204-0817,"
                                + "plugins/org.eclipse.core.jobs_3.15.500.v20250204-0817.jar,4,false"),
                BundleRow.parse(
                        "org.eclipse.equinox.common,3.20.0.v20250129-1348,"
                                + "plugins/org.eclipse.equinox.common_3.20.0.v20250129-1348.jar,2,true"),
                BundleRow.parse(
                        "org.eclipse.equinox.simpleconfigurator,1.5.400.v20250129-0942,"
                                + "plugins/org.eclipse.equinox.simpleconfigurator_1.5.400.v20250129-0942.jar,1,true"),
                BundleRow.parse(
                        "org.eclipse.osgi,3.23.0.v20250228-0640,"
                                + "plugins/org.eclipse.osgi_3.23.0.v20250228-0640.jar,-1,true"));
        return new Policy(
                RECORDED_CONFIG_SHA256,
                RECORDED_BUNDLES_SHA256,
                properties,
                rows,
                INTENDED_ROW);
    }

    static String sha256(byte[] bytes) {
        try {
            return HexFormat.of().withUpperCase().formatHex(
                    MessageDigest.getInstance("SHA-256").digest(bytes));
        } catch (NoSuchAlgorithmException exception) {
            throw new AssertionError("SHA-256 is required by the Java platform", exception);
        }
    }

    private static void checkSize(byte[] bytes) {
        if (bytes.length == 0 || bytes.length > MAX_INPUT_BYTES) {
            throw new PlanException(Failure.INVALID_SIZE);
        }
    }

    private static TextDocument decode(byte[] bytes) {
        String text;
        try {
            text = StandardCharsets.UTF_8.newDecoder()
                    .onMalformedInput(CodingErrorAction.REPORT)
                    .onUnmappableCharacter(CodingErrorAction.REPORT)
                    .decode(ByteBuffer.wrap(bytes))
                    .toString();
        } catch (CharacterCodingException exception) {
            throw new PlanException(Failure.INVALID_UTF8);
        }
        if (text.isEmpty() || text.charAt(0) == '\uFEFF' || text.indexOf('\0') >= 0) {
            throw new PlanException(Failure.INVALID_UTF8);
        }

        String lineEnding = null;
        for (int index = 0; index < text.length(); index++) {
            char current = text.charAt(index);
            String seen = null;
            if (current == '\r') {
                if (index + 1 >= text.length() || text.charAt(index + 1) != '\n') {
                    throw new PlanException(Failure.INVALID_LINE_ENDING);
                }
                seen = "\r\n";
                index++;
            } else if (current == '\n') {
                seen = "\n";
            }
            if (seen != null) {
                if (lineEnding != null && !lineEnding.equals(seen)) {
                    throw new PlanException(Failure.INVALID_LINE_ENDING);
                }
                lineEnding = seen;
            }
        }
        if (lineEnding == null) {
            throw new PlanException(Failure.INVALID_LINE_ENDING);
        }
        return new TextDocument(
                text,
                lineEnding,
                text.endsWith(lineEnding),
                splitLines(text, lineEnding));
    }

    private static List<String> splitLines(String text, String lineEnding) {
        List<String> lines = new ArrayList<>();
        int start = 0;
        int next;
        while ((next = text.indexOf(lineEnding, start)) >= 0) {
            lines.add(text.substring(start, next));
            start = next + lineEnding.length();
        }
        if (start < text.length()) {
            lines.add(text.substring(start));
        }
        return List.copyOf(lines);
    }

    private static Map<String, String> parseProperties(TextDocument document) {
        Map<String, String> properties = new HashMap<>();
        for (String sourceLine : document.lines()) {
            String line = sourceLine.strip();
            if (line.isEmpty() || line.startsWith("#") || line.startsWith("!")) {
                continue;
            }
            int trailingBackslashes = 0;
            for (int index = line.length() - 1;
                    index >= 0 && line.charAt(index) == '\\'; index--) {
                trailingBackslashes++;
            }
            if ((trailingBackslashes & 1) != 0) {
                throw new PlanException(Failure.INVALID_CONFIG);
            }
            int separator = line.indexOf('=');
            if (separator <= 0) {
                throw new PlanException(Failure.INVALID_CONFIG);
            }
            String key = line.substring(0, separator).strip();
            String value = line.substring(separator + 1).strip();
            if (key.isEmpty() || properties.putIfAbsent(key, value) != null) {
                throw new PlanException(Failure.INVALID_CONFIG);
            }
        }
        return Map.copyOf(properties);
    }

    private static List<BundleRow> parseBundles(TextDocument document) {
        List<String> lines = document.lines();
        if (lines.size() < 2
                || !"#encoding=UTF-8".equals(lines.get(0))
                || !"#version=1".equals(lines.get(1))) {
            throw new PlanException(Failure.INVALID_BUNDLES_INFO);
        }

        List<BundleRow> rows = new ArrayList<>();
        for (int index = 2; index < lines.size(); index++) {
            String line = lines.get(index);
            if (line.isEmpty() || line.startsWith("#")) {
                continue;
            }
            BundleRow row = BundleRow.parse(line);
            if (rows.contains(row)) {
                throw new PlanException(Failure.INVALID_BUNDLES_INFO);
            }
            rows.add(row);
        }
        return List.copyOf(rows);
    }

    /** The proposal is intentionally marked as unusable for installation. */
    public enum Disposition {
        OFFLINE_PLAN_ONLY
    }

    /** Stable, non-sensitive failure categories for callers and tests. */
    public enum Failure {
        HASH_MISMATCH,
        INVALID_BUNDLES_INFO,
        INVALID_CONFIG,
        INVALID_LINE_ENDING,
        INVALID_SIZE,
        INVALID_UTF8,
        OBSERVER_ALREADY_PRESENT,
        RECORDED_FACT_MISMATCH
    }

    /** A validation failure which does not include input content or paths. */
    public static final class PlanException extends IllegalArgumentException {
        private static final long serialVersionUID = 1L;

        private final Failure failure;

        PlanException(Failure failure) {
            super(Objects.requireNonNull(failure, "failure").name());
            this.failure = failure;
        }

        public Failure failure() {
            return failure;
        }
    }

    /** Immutable offline proposal. The byte-array accessor returns a copy. */
    public record Plan(
            String configSha256,
            String sourceBundlesSha256,
            String proposedBundlesSha256,
            String intendedRow,
            byte[] proposedBundlesInfo,
            Disposition disposition) {
        public Plan {
            Objects.requireNonNull(configSha256, "configSha256");
            Objects.requireNonNull(sourceBundlesSha256, "sourceBundlesSha256");
            Objects.requireNonNull(proposedBundlesSha256, "proposedBundlesSha256");
            Objects.requireNonNull(intendedRow, "intendedRow");
            proposedBundlesInfo = Objects.requireNonNull(
                    proposedBundlesInfo, "proposedBundlesInfo").clone();
            Objects.requireNonNull(disposition, "disposition");
        }

        @Override
        public byte[] proposedBundlesInfo() {
            return proposedBundlesInfo.clone();
        }
    }

    record Policy(
            String configSha256,
            String bundlesSha256,
            Map<String, String> requiredProperties,
            List<BundleRow> requiredRows,
            BundleRow intendedRow) {
        Policy {
            if (!HASH.matcher(Objects.requireNonNull(configSha256, "configSha256")).matches()
                    || !HASH.matcher(Objects.requireNonNull(
                            bundlesSha256, "bundlesSha256")).matches()) {
                throw new IllegalArgumentException("Policy hashes must be uppercase SHA-256");
            }
            requiredProperties = Map.copyOf(Objects.requireNonNull(
                    requiredProperties, "requiredProperties"));
            requiredRows = List.copyOf(Objects.requireNonNull(requiredRows, "requiredRows"));
            intendedRow = Objects.requireNonNull(intendedRow, "intendedRow");
        }
    }

    record BundleRow(
            String symbolicName,
            String version,
            String location,
            int startLevel,
            boolean autoStart) {
        BundleRow {
            if (!SYMBOLIC_NAME.matcher(Objects.requireNonNull(
                            symbolicName, "symbolicName")).matches()
                    || !VERSION.matcher(Objects.requireNonNull(version, "version")).matches()
                    || !LOCATION.matcher(Objects.requireNonNull(
                            location, "location")).matches()
                    || location.contains("..")
                    || (startLevel != -1 && startLevel < 1)) {
                throw new PlanException(Failure.INVALID_BUNDLES_INFO);
            }
        }

        static BundleRow parse(String line) {
            String[] fields = Objects.requireNonNull(line, "line").split(",", -1);
            if (fields.length != 5
                    || Arrays.stream(fields).anyMatch(String::isEmpty)
                    || (!"true".equals(fields[4]) && !"false".equals(fields[4]))) {
                throw new PlanException(Failure.INVALID_BUNDLES_INFO);
            }
            int parsedStartLevel;
            try {
                parsedStartLevel = Integer.parseInt(fields[3]);
            } catch (NumberFormatException exception) {
                throw new PlanException(Failure.INVALID_BUNDLES_INFO);
            }
            return new BundleRow(
                    fields[0],
                    fields[1],
                    fields[2],
                    parsedStartLevel,
                    Boolean.parseBoolean(fields[4]));
        }

        String line() {
            return String.join(",",
                    symbolicName,
                    version,
                    location,
                    Integer.toString(startLevel),
                    Boolean.toString(autoStart));
        }
    }

    private record TextDocument(
            String text,
            String lineEnding,
            boolean endsWithLineEnding,
            List<String> lines) {
        private TextDocument {
            lines = List.copyOf(lines);
        }
    }
}
