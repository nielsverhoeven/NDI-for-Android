package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.NdiOverlayMode
import com.ndi.core.model.TelemetryEvent

object SettingsTelemetry {

    fun discoveryServerSaved(hasEndpoint: Boolean): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.DISCOVERY_SERVER_SAVED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("hasEndpoint" to hasEndpoint.toString()),
        )
    }

    fun discoveryApplyImmediate(endpointHost: String?): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.DISCOVERY_SERVER_APPLY_IMMEDIATE,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("endpointHost" to (endpointHost ?: "default")),
        )
    }

    fun discoveryFallbackToDefault(reason: String): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.DISCOVERY_SERVER_FALLBACK_TO_DEFAULT,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("reason" to reason),
        )
    }

    fun activeStreamInterruptedForDiscovery(sessionId: String?): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.ACTIVE_STREAM_INTERRUPTED_FOR_DISCOVERY,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("sessionId" to (sessionId ?: "none")),
        )
    }

    fun developerModeToggled(enabled: Boolean): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.DEVELOPER_MODE_TOGGLED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("enabled" to enabled.toString()),
        )
    }

    fun developerOverlayStateChanged(from: NdiOverlayMode, to: NdiOverlayMode): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.DEVELOPER_OVERLAY_STATE_CHANGED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "from" to from.name,
                "to" to to.name,
            ),
        )
    }

    fun overlayLogRedactionApplied(linesRedacted: Int): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.OVERLAY_LOG_REDACTION_APPLIED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("linesRedacted" to linesRedacted.toString()),
        )
    }
}