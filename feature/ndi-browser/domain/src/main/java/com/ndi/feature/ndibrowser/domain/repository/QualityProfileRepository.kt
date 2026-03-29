package com.ndi.feature.ndibrowser.domain.repository

import kotlinx.coroutines.flow.Flow

/**
 * Canonical playback quality profiles used by the Viewer experience.
 *
 * Presets are intentionally fixed to provide predictable behavior across devices and
 * network conditions.
 */
sealed class QualityProfile(
    val id: String,
    val displayName: String,
    val description: String,
    val baseWidth: Int,
    val baseHeight: Int,
    val maxFrameRate: Int,
    val frameDropThresholdPercent: Int,
    val priority: Int,
) {
    data object Smooth : QualityProfile(
        id = "smooth",
        displayName = "Smooth",
        description = "Best for slow networks",
        baseWidth = 854,
        baseHeight = 480,
        maxFrameRate = 30,
        frameDropThresholdPercent = 5,
        priority = 1,
    )

    data object Balanced : QualityProfile(
        id = "balanced",
        displayName = "Balanced",
        description = "Adaptive quality",
        baseWidth = 1280,
        baseHeight = 720,
        maxFrameRate = 30,
        frameDropThresholdPercent = 10,
        priority = 2,
    )

    data object HighQuality : QualityProfile(
        id = "high_quality",
        displayName = "High Quality",
        description = "Maximize detail",
        baseWidth = 1920,
        baseHeight = 1080,
        maxFrameRate = 60,
        frameDropThresholdPercent = 15,
        priority = 3,
    )

    companion object {
        val SMOOTH: QualityProfile = Smooth
        val BALANCED: QualityProfile = Balanced
        val HIGH_QUALITY: QualityProfile = HighQuality

        fun all(): List<QualityProfile> = listOf(Smooth, Balanced, HighQuality)

        fun fromId(id: String?): QualityProfile {
            return all().firstOrNull { it.id == id } ?: Smooth
        }

        fun default(): QualityProfile = Smooth
    }

    // Compatibility aliases used by existing viewer/data code.
    val profileId: String get() = id
    val maxWidth: Int get() = baseWidth
    val maxHeight: Int get() = baseHeight
    val targetFps: Int get() = maxFrameRate

    fun nextLowerProfile(): QualityProfile? {
        return when (this) {
            HighQuality -> Balanced
            Balanced -> Smooth
            Smooth -> null
        }
    }
}

/**
 * Persisted quality selection for a global default or source-specific override.
 *
 * @property profileId stable identifier of the selected [QualityProfile].
 * @property sourceId optional NDI source identifier; null means global preference.
 * @property timestampEpochMillis time of last update in epoch milliseconds.
 */
data class QualityPreference(
    val profileId: String = QualityProfile.default().id,
    val sourceId: String? = null,
    val timestampEpochMillis: Long = System.currentTimeMillis(),
)

/**
 * Repository contract for quality profile discovery and preference persistence.
 */
interface QualityProfileRepository {

    /**
     * Returns all supported quality presets ordered from lowest to highest quality.
     */
    suspend fun getAllProfiles(): List<QualityProfile>

    /**
     * Observes quality preference updates for the requested source scope.
     */
    fun observeQualityPreference(sourceId: String? = null): Flow<QualityPreference>

    /**
     * Gets the effective quality preference for a source, falling back to global default.
     */
    suspend fun getQualityPreference(sourceId: String? = null): QualityPreference

    /**
     * Persists a quality preference for either global or source-specific scope.
     */
    suspend fun setQualityPreference(preference: QualityPreference)

    /**
     * Removes all persisted quality preferences and restores defaults.
     */
    suspend fun clearPreferences()
}
