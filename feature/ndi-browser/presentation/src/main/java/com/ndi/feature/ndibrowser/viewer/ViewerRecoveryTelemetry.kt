package com.ndi.feature.ndibrowser.viewer

import com.ndi.core.model.TelemetryEvent

object ViewerRecoveryTelemetry {

    fun retryRequested(sourceId: String): TelemetryEvent {
        return TelemetryEvent(
            name = "recovery_action_taken",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "action" to "retry",
            ),
        )
    }

    fun returnToListRequested(sourceId: String): TelemetryEvent {
        return TelemetryEvent(
            name = "recovery_action_taken",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "action" to "return_to_list",
            ),
        )
    }
}
