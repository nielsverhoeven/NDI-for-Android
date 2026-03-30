package com.ndi.feature.ndibrowser.data.util

object LogRedactor {
    private const val REDACTED = "[REDACTED]"

    // Redact IPv4 addresses
    private val ipv4Pattern = Regex("""(?<!\d)(?:\d{1,3}\.){3}\d{1,3}(?!\d)""")

    // Redact host= values (hostname.tld or simple words followed by port pattern)
    private val hostValuePattern = Regex("""(?<=\bhost=)[^\s,]+""")

    // Redact port= values
    private val portValuePattern = Regex("""(?<=\bport=)\d+""")

    fun redact(logLine: String): String {
        if (logLine.isEmpty()) return logLine
        return logLine
            .replace(ipv4Pattern, REDACTED)
            .replace(hostValuePattern, REDACTED)
            .replace(portValuePattern, REDACTED)
    }
}
