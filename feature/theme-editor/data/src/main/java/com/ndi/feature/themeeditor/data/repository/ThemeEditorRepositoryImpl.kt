package com.ndi.feature.themeeditor.data.repository

import com.ndi.core.database.SettingsPreferenceDao
import com.ndi.core.database.SettingsPreferenceEntity
import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.feature.themeeditor.domain.model.ThemeAccentPalette
import com.ndi.feature.themeeditor.domain.model.ThemePreference
import com.ndi.feature.themeeditor.domain.repository.ThemeEditorRepository
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.launch

class ThemeEditorRepositoryImpl(
    private val settingsDao: SettingsPreferenceDao,
) : ThemeEditorRepository {

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private val state = MutableStateFlow<ThemePreference?>(null)

    init {
        scope.launch {
            val snapshot = settingsDao.get()?.toSnapshot() ?: defaultSettingsSnapshot()
            state.value = ThemePreferenceMapper.fromSettings(snapshot)
        }
    }

    override suspend fun getThemePreference(): ThemePreference {
        val snapshot = settingsDao.get()?.toSnapshot() ?: defaultSettingsSnapshot()
        return ThemePreferenceMapper.fromSettings(snapshot)
    }

    override suspend fun saveThemePreference(preference: ThemePreference) {
        val currentSnapshot = settingsDao.get()?.toSnapshot() ?: defaultSettingsSnapshot()
        val merged = ThemePreferenceMapper.toSettings(currentSnapshot, preference)
        settingsDao.upsert(merged.toEntity())
        state.value = ThemePreferenceMapper.fromSettings(merged)
    }

    override fun observeThemePreference(): Flow<ThemePreference> = state.filterNotNull()

    private fun defaultSettingsSnapshot(): NdiSettingsSnapshot = NdiSettingsSnapshot(
        discoveryServerInput = null,
        developerModeEnabled = false,
        themeMode = ThemePreferenceMapper.normalizeThemeMode(null),
        accentColorId = ThemeAccentPalette.defaultAccentColorId,
        updatedAtEpochMillis = 0L,
    )
}

private fun SettingsPreferenceEntity.toSnapshot(): NdiSettingsSnapshot = NdiSettingsSnapshot(
    discoveryServerInput = discoveryServerInput,
    developerModeEnabled = developerModeEnabled,
    themeMode = ThemePreferenceMapper.normalizeThemeMode(themeMode),
    accentColorId = ThemePreferenceMapper.normalizeAccent(accentColorId),
    updatedAtEpochMillis = updatedAtEpochMillis,
)

private fun NdiSettingsSnapshot.toEntity(): SettingsPreferenceEntity = SettingsPreferenceEntity(
    id = 1,
    discoveryServerInput = discoveryServerInput,
    developerModeEnabled = developerModeEnabled,
    themeMode = themeMode.name,
    accentColorId = ThemePreferenceMapper.normalizeAccent(accentColorId),
    updatedAtEpochMillis = updatedAtEpochMillis,
)
