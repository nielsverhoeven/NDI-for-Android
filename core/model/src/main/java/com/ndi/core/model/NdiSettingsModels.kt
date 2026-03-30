package com.ndi.core.model

data class NdiSettingsSnapshot(
    val discoveryServerInput: String?,
    val developerModeEnabled: Boolean,
    val themeMode: NdiThemeMode = NdiThemeMode.SYSTEM,
    val accentColorId: String = "accent_teal",
    val updatedAtEpochMillis: Long,
)

enum class NdiThemeMode { LIGHT, DARK, SYSTEM }

enum class SettingsLayoutMode {
    WIDE,
    COMPACT,
}

enum class SettingsCategorySelectionSource {
    DEFAULT,
    USER_TAP,
    RESTORED,
}

data class SettingsLayoutContext(
    val mode: SettingsLayoutMode,
    val meetsWideLayoutCriteria: Boolean,
    val widthDp: Int,
    val isLandscape: Boolean,
    val lastTransitionEpochMillis: Long,
)

data class SettingsCategory(
    val id: String,
    val title: String,
    val subtitle: String? = null,
    val isSelected: Boolean,
    val hasAdjustableOptions: Boolean,
)

data class SettingsCategoryState(
    val categories: List<SettingsCategory>,
    val selectedCategoryId: String?,
    val selectionSource: SettingsCategorySelectionSource,
)

data class SettingsDetailGroup(
    val id: String,
    val title: String,
    val controls: List<String>,
)

data class SettingsDetailState(
    val selectedCategoryId: String?,
    val groups: List<SettingsDetailGroup>,
    val emptyStateMessage: String?,
    val isEditable: Boolean,
)

// ---- Spec 018: Discovery server collection model types ----

/**
 * Default NDI discovery port for the new multi-server feature.
 * NOTE: DEFAULT_NDI_DISCOVERY_PORT in NdiDiscoveryEndpoint matches this value (5959) —
 * the official NDI Discovery Server default port per NDI docs.
 */
const val DEFAULT_DISCOVERY_SERVER_PORT = 5959

/**
 * A single persisted discovery server entry managed by the discovery server settings submenu.
 */
data class DiscoveryServerEntry(
    val id: String,
    val hostOrIp: String,
    val port: Int,
    val enabled: Boolean,
    val orderIndex: Int,
    val createdAtEpochMillis: Long,
    val updatedAtEpochMillis: Long,
) {
    /** Display label shown in the settings list. */
    val displayLabel: String get() = "$hostOrIp:$port"
}

/**
 * Complete ordered collection of discovery server entries.
 */
data class DiscoveryServerCollection(
    val entries: List<DiscoveryServerEntry>,
) {
    val enabledEntries: List<DiscoveryServerEntry>
        get() = entries.filter { it.enabled }.sortedBy { it.orderIndex }
}

/** Modes for the add/edit form. */
enum class DiscoveryServerDraftMode { ADD, EDIT }

/**
 * In-progress add/edit form state for the discovery server settings UI.
 */
data class DiscoveryServerDraft(
    val hostInput: String = "",
    val portInput: String = "",
    val validationError: String? = null,
    val mode: DiscoveryServerDraftMode = DiscoveryServerDraftMode.ADD,
    val editingEntryId: String? = null,
) {
    /** Effective port: parsed from portInput or defaulted to 5959 when blank. */
    val resolvedPort: Int
        get() = if (portInput.isBlank()) {
            DEFAULT_DISCOVERY_SERVER_PORT
        } else {
            portInput.trim().toIntOrNull() ?: DEFAULT_DISCOVERY_SERVER_PORT
        }

    val isSaveEnabled: Boolean
        get() = hostInput.isNotBlank() && validationError == null
}

/** Selection outcome when querying for an active discovery target at runtime. */
enum class DiscoverySelectionOutcome {
    SUCCESS,
    ALL_ENABLED_UNREACHABLE,
    NO_ENABLED_SERVERS,
}

/**
 * Result object returned by DiscoveryServerRepository.resolveActiveDiscoveryTarget().
 */
data class DiscoverySelectionResult(
    val attemptedEntryIds: List<String>,
    val selectedEntryId: String?,
    val result: DiscoverySelectionOutcome,
    val errorMessage: String?,
)

data class NdiDiscoveryEndpoint(
    val host: String,
    val port: Int?,
    val resolvedPort: Int,
    val usesDefaultPort: Boolean,
) {
    companion object {
        const val DEFAULT_NDI_DISCOVERY_PORT = 5959

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
    val sessionId: String? = null,
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
