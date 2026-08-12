package net.hrcautomation.jobobserver;

import java.util.regex.Pattern;

enum OperationKind {
    NASH,
    VIEWER_SAVE,
    EXPORT;

    private static final Pattern SAFE_SIMULATION_BASE =
            Pattern.compile("[A-Za-z0-9][A-Za-z0-9._-]{0,99}");
    private static final Pattern SAFE_STAGING_BASE =
            Pattern.compile("[A-Za-z0-9][A-Za-z0-9._-]{0,199}");
    private static final Pattern WINDOWS_RESERVED_BASE =
            Pattern.compile("(?i)(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])");

    boolean acceptsExpectedName(String name) {
        if (!isBoundedText(name, 300)) {
            return false;
        }

        return switch (this) {
            case NASH -> hasSafeSimulationPrefix(name, ": Monte Carlo Sampling");
            case VIEWER_SAVE -> hasSafeFilename(name, "Saving hand to: ", ".hrcv");
            case EXPORT -> hasSafeFilename(name, "Exporting ranges to ", ".zip");
        };
    }

    private static boolean hasSafeSimulationPrefix(String value, String suffix) {
        if (!value.endsWith(suffix) || value.length() <= suffix.length()) {
            return false;
        }
        String base = value.substring(0, value.length() - suffix.length());
        return SAFE_SIMULATION_BASE.matcher(base).matches()
                && isSafeBase(base)
                && !hasKnownSuffix(base, ".hrcv", ".hrcz");
    }

    private static boolean hasSafeFilename(String value, String prefix, String extension) {
        if (!value.startsWith(prefix)) {
            return false;
        }
        String filename = value.substring(prefix.length());
        if (filename.length() <= extension.length() || !filename.endsWith(extension)) {
            return false;
        }
        String base = filename.substring(0, filename.length() - extension.length());
        return SAFE_STAGING_BASE.matcher(base).matches()
                && isSafeBase(base)
                && !hasKnownSuffix(base, ".hrcv", ".hrcz", ".zip", ".json");
    }

    private static boolean isSafeBase(String base) {
        if (base.endsWith(".")) {
            return false;
        }
        int firstDot = base.indexOf('.');
        String deviceBase = firstDot < 0 ? base : base.substring(0, firstDot);
        return !WINDOWS_RESERVED_BASE.matcher(deviceBase).matches();
    }

    private static boolean hasKnownSuffix(String base, String... suffixes) {
        String lower = base.toLowerCase(java.util.Locale.ROOT);
        for (String suffix : suffixes) {
            if (lower.endsWith(suffix)) {
                return true;
            }
        }
        return false;
    }

    private static boolean isBoundedText(String value, int maximumLength) {
        return value != null
                && !value.isBlank()
                && value.length() <= maximumLength
                && value.chars().noneMatch(character -> character < 0x20 || character == 0x7f);
    }
}
