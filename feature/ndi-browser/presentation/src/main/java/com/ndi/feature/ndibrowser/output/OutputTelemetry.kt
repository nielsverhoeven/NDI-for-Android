package com.ndi.feature.ndibrowser.output

import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.OutputConfigurationRepository

fun interface OutputTelemetryEmitter {
    fun emit(event: TelemetryEvent)
}

object OutputDependencies {
    var outputRepositoryProvider: (() -> NdiOutputRepository)? = null
    var outputConfigurationRepositoryProvider: (() -> OutputConfigurationRepository)? = null
    var telemetryEmitter: OutputTelemetryEmitter = OutputTelemetryEmitter {}

    fun requireOutputRepository(): NdiOutputRepository {
        return requireNotNull(outputRepositoryProvider) { "Output repository dependency is not configured." }.invoke()
    }

    fun requireOutputConfigurationRepository(): OutputConfigurationRepository {
        return requireNotNull(outputConfigurationRepositoryProvider) { "Output configuration dependency is not configured." }.invoke()
    }
}

object OutputTelemetry {
    fun outputStartRequested(sourceId: String): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.OUTPUT_START_REQUESTED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("sourceId" to sourceId),
        )
    }

    fun outputStopRequested(sourceId: String): TelemetryEvent {
        return TelemetryEvent(
            name = "output_stop_requested",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("sourceId" to sourceId),
        )
    }

    fun outputStopped(sourceId: String): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.OUTPUT_STOPPED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("sourceId" to sourceId),
        )
    }

    fun outputInterrupted(sourceId: String, reason: String?): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.OUTPUT_INTERRUPTED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "reason" to (reason ?: "unknown"),
            ),
        )
    }

    fun outputRetryRequested(sourceId: String, windowSeconds: Int): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.OUTPUT_RETRY_REQUESTED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "windowSeconds" to windowSeconds.toString(),
            ),
        )
    }

    fun outputRetrySucceeded(sourceId: String, attempts: Int): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.OUTPUT_RETRY_SUCCEEDED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "attempts" to attempts.toString(),
            ),
        )
    }

    fun outputRetryFailed(sourceId: String, attempts: Int): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.OUTPUT_RETRY_FAILED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "attempts" to attempts.toString(),
            ),
        )
    }
}
