package com.ndi.core.model

data class NdiSettingsSnapshot(
    val discoveryServerInput: String?,
    val developerModeEnabled: Boolean,
    val updatedAtEpochMillis: Long,
)

data class NdiDiscoveryEndpoint(
    val host: String,
    val port: Int?,
    val resolvedPort: Int,
    val usesDefaultPort: Boolean,
) {
    companion object {
        const val DEFAULT_NDI_DISCOVERY_PORT = 5960

        /**
         * Parse formats: hostname, hostname:port, IPv4, IPv4:port, [IPv6], [IPv6]:port.
         * Trims the input before parsing. Rejects unbracketed IPv6.
         */
        fun parse(raw: String?): NdiDiscoveryEndpoint? {
            val trimmed = raw?.trim() ?: return null
            if (trimmed.isBlank()) return null

            val host: String
            val port: Int?

            if (trimmed.startsWith("[")) {
                // IPv6 bracketed form: [::1] or [::1]:5960
                val closeBracket = trimmed.indexOf(']')
                if (closeBracket < 0) return null
                host = trimmed.substring(1, closeBracket)
                val rest = trimmed.substring(closeBracket + 1)
                port = when {
                    rest.isEmpty() -> null
                    rest.startsWith(":") -> {
                        val portStr = rest.substring(1)
                        portStr.toIntOrNull()?.takeIf { isValidPort(it) } ?: return null
                    }
                    else -> return null
                }
            } else {
                val colonCount = trimmed.count { it == ':' }
                when {
                    colonCount == 0 -> {
                        host = trimmed
                        port = null
                    }
                    colonCount == 1 -> {
                        val idx = trimmed.indexOf(':')
                        host = trimmed.substring(0, idx)
                        val portStr = trimmed.substring(idx + 1)
                        port = portStr.toIntOrNull()?.takeIf { isValidPort(it) } ?: return null
                    }
                    else -> {
                        // Multiple colons — unbracketed IPv6: reject
                        return null
                    }
                }
            }

            if (!isValidHost(host)) return null

            val resolvedPort = port ?: DEFAULT_NDI_DISCOVERY_PORT
            return NdiDiscoveryEndpoint(
                host = host,
                port = port,
                resolvedPort = resolvedPort,
                usesDefaultPort = port == null,
            )
        }

        fun isValidHost(host: String): Boolean =
            host.isNotBlank() && host.length <= 253 && !host.startsWith(".") && !host.endsWith(".")

        fun isValidPort(port: Int): Boolean = port in 1..65535
    }
}

data class NdiDiscoveryApplyResult(
    val applyId: String,
    val endpoint: NdiDiscoveryEndpoint?,
    val interruptedActiveStream: Boolean,
    val fallbackTriggered: Boolean,
    val appliedAtEpochMillis: Long,
)

enum class NdiOverlayMode { DISABLED, IDLE, ACTIVE }

enum class NdiStreamDirection { NONE, INCOMING, OUTGOING }

enum class NdiStreamStatus { IDLE, CONNECTING, ACTIVE, INTERRUPTED, ERROR }

enum class NdiLogLevel { DEBUG, INFO, WARN, ERROR }

enum class NdiLogCategory { DISCOVERY, VIEWER, OUTPUT, SYSTEM }

data class NdiDeveloperOverlayState(
    val visible: Boolean,
    val mode: NdiOverlayMode,
    val streamDirectionLabel: String,
    val streamStatusLabel: String,
    val streamSourceLabel: String?,
    val warningMessage: String?,
    val recentLogs: List<NdiRedactedLogEntry>,
    val updatedAtEpochMillis: Long,
)

data class NdiRedactedLogEntry(
    val timestampEpochMillis: Long,
    val level: NdiLogLevel,
    val category: NdiLogCategory,
    val messageRedacted: String,
    val redactionApplied: Boolean,
)
