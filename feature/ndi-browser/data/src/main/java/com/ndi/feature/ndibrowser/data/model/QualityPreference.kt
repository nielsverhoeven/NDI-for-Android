package com.ndi.feature.ndibrowser.data.model

import com.ndi.feature.ndibrowser.domain.repository.QualityProfile
import com.ndi.feature.ndibrowser.domain.repository.QualityPreference as DomainQualityPreference

/**
 * Persistence model for quality preference storage.
 */
data class QualityPreference(
    val profileId: String = QualityProfile.default().id,
    val sourceId: String? = null,
    val timestampEpochMillis: Long = System.currentTimeMillis(),
) {
    fun toDomain(): DomainQualityPreference {
        return DomainQualityPreference(
            profileId = profileId,
            sourceId = sourceId,
            timestampEpochMillis = timestampEpochMillis,
        )
    }

    companion object {
        fun fromDomain(preference: DomainQualityPreference): QualityPreference {
            return QualityPreference(
                sourceId = preference.sourceId,
                profileId = preference.profileId,
                timestampEpochMillis = preference.timestampEpochMillis,
            )
        }
    }
}

typealias StoredQualityPreference = QualityPreference
