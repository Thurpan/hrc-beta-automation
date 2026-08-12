package net.hrcautomation.jobobserver;

record StatusSnapshot(
        int severity,
        boolean ok,
        int code,
        String plugin,
        boolean pluginOmitted) {

    static final int OK = 0;
    static final int INFO = 1;
    static final int WARNING = 2;
    static final int ERROR = 4;
    static final int CANCEL = 8;

    StatusSnapshot {
        if (plugin == null || plugin.isBlank() || plugin.length() > 160
                || !isPluginToken(plugin)) {
            plugin = "";
            pluginOmitted = true;
        }
    }

    static StatusSnapshot capture(int severity, boolean ok, int code, String plugin) {
        return new StatusSnapshot(severity, ok, code, plugin, false);
    }

    TerminalResult terminalResult() {
        if (severity == OK && ok) {
            return TerminalResult.OK;
        }
        if (severity == CANCEL && !ok) {
            return TerminalResult.CANCEL;
        }
        if (severity == ERROR && !ok) {
            return TerminalResult.ERROR;
        }
        return TerminalResult.UNKNOWN;
    }

    private static boolean isPluginToken(String value) {
        return value.chars().allMatch(character ->
                Character.isLetterOrDigit(character)
                        || character == '.'
                        || character == '_'
                        || character == '-');
    }
}
