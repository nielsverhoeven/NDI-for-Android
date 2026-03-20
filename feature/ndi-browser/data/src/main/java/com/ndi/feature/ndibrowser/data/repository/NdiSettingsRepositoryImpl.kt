package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.SettingsPreferenceDao
import com.ndi.core.database.SettingsPreferenceEntity
import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.launch

class NdiSettingsRepositoryImpl(
    private val settingsDao: SettingsPreferenceDao,
) : NdiSettingsRepository {

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private val _settings = MutableStateFlow<NdiSettingsSnapshot?>(null)

    init {
        scope.launch {
            _settings.value = settingsDao.get()?.toSnapshot() ?: defaultSnapshot()
        }
    }

    override suspend fun getSettings(): NdiSettingsSnapshot {
        return settingsDao.get()?.toSnapshot() ?: defaultSnapshot()
    }

    override suspend fun saveSettings(snapshot: NdiSettingsSnapshot) {
        settingsDao.upsert(snapshot.toEntity())
        _settings.value = snapshot
    }

    override fun observeSettings(): Flow<NdiSettingsSnapshot> = _settings.filterNotNull()

    private fun defaultSnapshot() = NdiSettingsSnapshot(
        discoveryServerInput = null,
        developerModeEnabled = false,
        updatedAtEpochMillis = 0L,
    )
}

private fun SettingsPreferenceEntity.toSnapshot(): NdiSettingsSnapshot = NdiSettingsSnapshot(
    discoveryServerInput = discoveryServerInput,
    developerModeEnabled = developerModeEnabled,
    updatedAtEpochMillis = updatedAtEpochMillis,
)

private fun NdiSettingsSnapshot.toEntity(): SettingsPreferenceEntity = SettingsPreferenceEntity(
    id = 1,
    discoveryServerInput = discoveryServerInput,
    developerModeEnabled = developerModeEnabled,
    updatedAtEpochMillis = updatedAtEpochMillis,
)
