package com.ndi.feature.ndibrowser.data.repository

import com.ndi.feature.ndibrowser.data.local.SharedPreferencesQualityStore
import com.ndi.feature.ndibrowser.data.model.StoredQualityPreference
import com.ndi.feature.ndibrowser.domain.repository.QualityPreference
import com.ndi.feature.ndibrowser.domain.repository.QualityProfile
import com.ndi.feature.ndibrowser.domain.repository.QualityProfileRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

class QualityProfileRepositoryImpl(
    private val store: SharedPreferencesQualityStore,
) : QualityProfileRepository {

    private val mutex = Mutex()
    private val globalPreference = MutableStateFlow(store.loadQualityPreference(sourceId = null).toDomain())
    private val profiles = listOf(
        QualityProfile.Smooth,
        QualityProfile.Balanced,
        QualityProfile.HighQuality,
    )

    override suspend fun getAllProfiles(): List<QualityProfile> {
        return profiles
    }

    override fun observeQualityPreference(sourceId: String?): Flow<QualityPreference> {
        return globalPreference.asStateFlow()
    }

    override suspend fun getQualityPreference(sourceId: String?): QualityPreference {
        return mutex.withLock {
            store.loadQualityPreference(sourceId).toDomain()
        }
    }

    override suspend fun setQualityPreference(preference: QualityPreference) {
        mutex.withLock {
            store.saveQualityPreference(StoredQualityPreference.fromDomain(preference))
            if (preference.sourceId.isNullOrBlank()) {
                globalPreference.value = preference
            }
        }
    }

    override suspend fun clearPreferences() {
        mutex.withLock {
            store.clearPreferences()
            globalPreference.value = store.loadQualityPreference(sourceId = null).toDomain()
        }
    }
}
