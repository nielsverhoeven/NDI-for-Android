package com.ndi.feature.ndibrowser.data.model

import com.ndi.feature.ndibrowser.domain.repository.QualityProfile

/**
 * Real-time optimization metrics captured while streaming.
 */
data class PlaybackOptimizationState(
    val sourceId: String,
    val selectedProfileId: String,
    val smoothPlaybackCount: Int = 0,
    val droppedFrameCount: Int = 0,
    val droppedFramePercent: Int = 0,
    val lastFrameTime: Long = 0L,
    val averageFrameRate: Double = 0.0,
    val currentFrameRate: Double = 0.0,
    val reconnectAttemptCount: Int = 0,
    val autoDegradeCount: Int = 0,
    val updatedAtEpochMillis: Long = System.currentTimeMillis(),
)

@Deprecated(
    message = "Use PlaybackOptimizationState for richer optimization telemetry.",
    replaceWith = ReplaceWith("PlaybackOptimizationState"),
)
data class PlaybackOptimization(
    val sourceId: String,
    val selectedProfile: QualityProfile,
    val frameRateFps: Int,
    val droppedFramePercent: Int,
    val autoDowngraded: Boolean,
    val timestampEpochMillis: Long = System.currentTimeMillis(),
) {
    fun shouldDowngrade(): Boolean {
        return droppedFramePercent >= selectedProfile.frameDropThresholdPercent
    }
}
