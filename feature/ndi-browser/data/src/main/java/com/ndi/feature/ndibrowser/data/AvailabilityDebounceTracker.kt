package com.ndi.feature.ndibrowser.data

import com.ndi.feature.ndibrowser.domain.repository.SourceAvailabilityStatus

/**
 * Applies the two-miss debounce policy for source availability transitions.
 */
class AvailabilityDebounceTracker(
    private val unavailableMissThreshold: Int = 2,
) {

    fun update(
        previous: SourceAvailabilityStatus?,
        sourceId: String,
        seenInSnapshot: Boolean,
        nowEpochMillis: Long = System.currentTimeMillis(),
    ): SourceAvailabilityStatus {
        val normalizedPrevious = previous ?: SourceAvailabilityStatus(
            sourceId = sourceId,
            isAvailable = true,
            consecutiveMissedPolls = 0,
            lastSeenAtEpochMillis = null,
            lastStatusChangedAtEpochMillis = nowEpochMillis,
        )

        if (seenInSnapshot) {
            val statusChanged = !normalizedPrevious.isAvailable
            return normalizedPrevious.copy(
                sourceId = sourceId,
                isAvailable = true,
                consecutiveMissedPolls = 0,
                lastSeenAtEpochMillis = nowEpochMillis,
                lastStatusChangedAtEpochMillis = if (statusChanged) nowEpochMillis else normalizedPrevious.lastStatusChangedAtEpochMillis,
            )
        }

        val nextMisses = (normalizedPrevious.consecutiveMissedPolls + 1).coerceAtLeast(0)
        val nextAvailability = nextMisses < unavailableMissThreshold
        val statusChanged = normalizedPrevious.isAvailable != nextAvailability
        return normalizedPrevious.copy(
            sourceId = sourceId,
            isAvailable = nextAvailability,
            consecutiveMissedPolls = nextMisses,
            lastStatusChangedAtEpochMillis = if (statusChanged) nowEpochMillis else normalizedPrevious.lastStatusChangedAtEpochMillis,
        )
    }
}
