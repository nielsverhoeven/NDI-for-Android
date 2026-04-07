package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.NdiOverlayMode
import com.ndi.core.model.DeveloperDiscoveryDiagnostics

data class OverlayDisplayState(
    val mode: NdiOverlayMode,
    val streamStatus: String?,
    val sessionId: String?,
    val recentLogs: List<String>,
    val configuredAddresses: List<String> = emptyList(),
    val discoveryDiagnostics: DeveloperDiscoveryDiagnostics? = null,
)

object DeveloperOverlayStateMapper {

    fun map(
        developerModeEnabled: Boolean,
        streamStatus: String?,
        sessionId: String?,
        recentLogs: List<String>,
        configuredAddresses: List<String> = emptyList(),
        discoveryDiagnostics: DeveloperDiscoveryDiagnostics? = null,
    ): OverlayDisplayState {
        val mode = when {
            !developerModeEnabled -> NdiOverlayMode.DISABLED
            streamStatus != null -> NdiOverlayMode.ACTIVE
            else -> NdiOverlayMode.IDLE
        }
        return OverlayDisplayState(
            mode = mode,
            streamStatus = if (mode == NdiOverlayMode.DISABLED) null else streamStatus,
            sessionId = if (mode == NdiOverlayMode.DISABLED) null else sessionId,
            recentLogs = if (mode == NdiOverlayMode.DISABLED) emptyList() else recentLogs,
            configuredAddresses = if (mode == NdiOverlayMode.DISABLED) emptyList() else configuredAddresses,
            discoveryDiagnostics = if (mode == NdiOverlayMode.DISABLED) null else discoveryDiagnostics,
        )
    }
}