package com.ndi.feature.ndibrowser.data

import com.ndi.core.model.OutputHealthSnapshot
import com.ndi.core.model.OutputQualityLevel
import com.ndi.core.model.OutputSession
import com.ndi.core.model.OutputState
import java.util.UUID

class OutputSessionCoordinator(
    private val nowProvider: () -> Long = System::currentTimeMillis,
    private val snapshotIdProvider: () -> String = { UUID.randomUUID().toString() },
) {

    fun nextOnStopRequested(current: OutputSession): OutputSession {
        if (current.state == OutputState.STOPPING || current.state == OutputState.STOPPED) return current
        return current.copy(state = OutputState.STOPPING)
    }

    fun nextOnStopped(stopping: OutputSession): OutputSession {
        return stopping.copy(
            state = OutputState.STOPPED,
            stoppedAtEpochMillis = nowProvider(),
            interruptionReason = null,
        )
    }

    fun nextHealthForState(session: OutputSession): OutputHealthSnapshot {
        val quality = when (session.state) {
            OutputState.ACTIVE -> OutputQualityLevel.HEALTHY
            OutputState.INTERRUPTED -> OutputQualityLevel.FAILED
            OutputState.STOPPED,
            OutputState.STOPPING,
            OutputState.READY,
            OutputState.STARTING,
            -> OutputQualityLevel.DEGRADED
        }
        val messageCode = when (session.state) {
            OutputState.STOPPED -> "output_stopped"
            OutputState.STOPPING -> "output_stopping"
            OutputState.INTERRUPTED -> "output_interrupted"
            else -> null
        }

        return OutputHealthSnapshot(
            snapshotId = snapshotIdProvider(),
            sessionId = session.sessionId,
            capturedAtEpochMillis = nowProvider(),
            networkReachable = session.state != OutputState.INTERRUPTED,
            inputReachable = session.state != OutputState.INTERRUPTED,
            qualityLevel = quality,
            messageCode = messageCode,
        )
    }
}