package com.ndi.feature.ndibrowser.settings

object OverlayLogRedactor {
    private const val REDACTED_IP = "[redacted-ip]"
    private val ipv6BracketedPattern = Regex("""\[(?:[0-9A-Fa-f]{0,4}:){2,}[0-9A-Fa-f:.%]+]""")
    private val ipv6UnbracketedPattern = Regex("""(?<![\w\[:])(?:[0-9A-Fa-f]{0,4}:){2,7}[0-9A-Fa-f]{0,4}(?![\w:])""")
    private val ipv4Pattern = Regex("""(?<!\d)(?:\d{1,3}\.){3}\d{1,3}(?!\d)""")

    fun redact(logLine: String): String {
        if (logLine.isEmpty()) return logLine
        return logLine
            .replace(ipv6BracketedPattern, REDACTED_IP)
            .replace(ipv6UnbracketedPattern, REDACTED_IP)
            .replace(ipv4Pattern, REDACTED_IP)
    }

    fun redactSessionId(sessionId: String?): String? {
        val normalized = sessionId?.trim().orEmpty()
        if (normalized.isBlank()) return null
        return if (normalized.length <= 4) {
            "****"
        } else {
            "****${normalized.takeLast(4)}"
        }
    }
}