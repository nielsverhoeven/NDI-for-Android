package com.ndi.feature.ndibrowser.viewer

import com.ndi.core.model.TelemetryEvent
import java.util.concurrent.atomic.AtomicLong

object ViewerRecoveryTelemetry {

    const val PROFILE_SELECTED = "viewer_profile_selected"
    const val QUALITY_DOWNGRADED = "viewer_quality_downgraded"
    const val QUALITY_RECOVERED = "viewer_quality_recovered"
    const val RECOVERY_DIALOG_SHOWN = "viewer_recovery_dialog_shown"
    const val RECOVERY_SUCCESS = "viewer_recovery_success"
    const val RECOVERY_FAILURE = "viewer_recovery_failure"

    private val smoothPlaySecondsCounter = AtomicLong(0)
    private val degradedSegmentsCounter = AtomicLong(0)
    private val recoveryAttemptsCounter = AtomicLong(0)
    private val recoverySuccessesCounter = AtomicLong(0)
    private val recoveryFailuresCounter = AtomicLong(0)

    const val EVENT_QUALITY_CHANGED = "viewer_quality_changed"
    const val EVENT_QUALITY_AUTO_DEGRADED = "viewer_quality_auto_degraded"
    const val EVENT_RECOVERY_SUCCEEDED = "viewer_recovery_succeeded"

    fun profileSelected(sourceId: String, profileId: String): TelemetryEvent {
        return TelemetryEvent(
            name = PROFILE_SELECTED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "profileId" to profileId,
            ),
        )
    }

    fun qualityDowngraded(sourceId: String, fromProfileId: String, toProfileId: String): TelemetryEvent {
        degradedSegmentsCounter.incrementAndGet()
        return TelemetryEvent(
            name = QUALITY_DOWNGRADED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "fromProfileId" to fromProfileId,
                "toProfileId" to toProfileId,
                "degraded_segments" to degradedSegmentsCounter.get().toString(),
            ),
        )
    }

    fun qualityRecovered(sourceId: String, profileId: String): TelemetryEvent {
        return TelemetryEvent(
            name = QUALITY_RECOVERED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "profileId" to profileId,
            ),
        )
    }

    fun recoveryDialogShown(sourceId: String): TelemetryEvent {
        return TelemetryEvent(
            name = RECOVERY_DIALOG_SHOWN,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
            ),
        )
    }

    fun recoveryAttempted(sourceId: String, attempt: Int): TelemetryEvent {
        recoveryAttemptsCounter.incrementAndGet()
        return TelemetryEvent(
            name = "viewer_recovery_attempt",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "attempt" to attempt.toString(),
                "recovery_attempts" to recoveryAttemptsCounter.get().toString(),
            ),
        )
    }

    fun recoveryResult(sourceId: String, success: Boolean): TelemetryEvent {
        if (success) {
            recoverySuccessesCounter.incrementAndGet()
        } else {
            recoveryFailuresCounter.incrementAndGet()
        }
        return TelemetryEvent(
            name = if (success) RECOVERY_SUCCESS else RECOVERY_FAILURE,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "recovery_successes" to recoverySuccessesCounter.get().toString(),
                "recovery_failures" to recoveryFailuresCounter.get().toString(),
                "smooth_play_seconds" to smoothPlaySecondsCounter.get().toString(),
            ),
        )
    }

    fun smoothPlaybackTick(sourceId: String): TelemetryEvent {
        smoothPlaySecondsCounter.incrementAndGet()
        return TelemetryEvent(
            name = "viewer_smooth_playback_tick",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "smooth_play_seconds" to smoothPlaySecondsCounter.get().toString(),
            ),
        )
    }

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

    fun qualityChanged(sourceId: String, profileId: String, automatic: Boolean): TelemetryEvent {
        return TelemetryEvent(
            name = if (automatic) EVENT_QUALITY_AUTO_DEGRADED else EVENT_QUALITY_CHANGED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "profileId" to profileId,
            ),
        )
    }

    fun recoverySucceeded(sourceId: String, attempts: Int): TelemetryEvent {
        return TelemetryEvent(
            name = EVENT_RECOVERY_SUCCEEDED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "attempts" to attempts.toString(),
            ),
        )
    }
}
