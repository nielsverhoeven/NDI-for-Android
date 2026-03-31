package com.ndi.feature.themeeditor.data.repository

import com.ndi.core.database.SettingsPreferenceDao
import com.ndi.core.database.SettingsPreferenceEntity
import com.ndi.core.model.NdiThemeMode
import com.ndi.feature.themeeditor.domain.model.ThemeAccentPalette
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.launch
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.take
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class ThemeEditorRepositoryImplTest {

    @Test
    fun getThemePreference_usesDefaults_whenNoStoredEntity() = runTest {
        val dao = FakeSettingsPreferenceDao(null)
        val repository = ThemeEditorRepositoryImpl(dao)

        val preference = repository.getThemePreference()

        assertEquals(NdiThemeMode.SYSTEM, preference.themeMode)
        assertEquals(ThemeAccentPalette.defaultAccentColorId, preference.accentColorId)
    }

    @Test
    fun saveThemePreference_normalizesUnknownAccent() = runTest {
        val dao = FakeSettingsPreferenceDao(
            SettingsPreferenceEntity(
                id = 1,
                discoveryServerInput = null,
                developerModeEnabled = false,
                themeMode = NdiThemeMode.SYSTEM.name,
                accentColorId = ThemeAccentPalette.defaultAccentColorId,
                updatedAtEpochMillis = 1L,
            ),
        )
        val repository = ThemeEditorRepositoryImpl(dao)

        repository.saveThemePreference(
            com.ndi.feature.themeeditor.domain.model.ThemePreference(
                themeMode = NdiThemeMode.DARK,
                accentColorId = "accent_unknown",
                updatedAtEpochMillis = 2L,
            ),
        )

        val saved = dao.get() ?: error("Expected stored entity")
        assertEquals(NdiThemeMode.DARK.name, saved.themeMode)
        assertEquals(ThemeAccentPalette.defaultAccentColorId, saved.accentColorId)
    }

    @Test
    fun observeThemePreference_emitsWhenExternalSettingsWriteOccurs() = runTest {
        val dao = FakeSettingsPreferenceDao(
            SettingsPreferenceEntity(
                id = 1,
                discoveryServerInput = null,
                developerModeEnabled = false,
                themeMode = NdiThemeMode.SYSTEM.name,
                accentColorId = ThemeAccentPalette.defaultAccentColorId,
                updatedAtEpochMillis = 1L,
            ),
        )
        val repository = ThemeEditorRepositoryImpl(dao)
        val collected = mutableListOf<com.ndi.feature.themeeditor.domain.model.ThemePreference>()
        val collectionJob = launch {
            repository.observeThemePreference().take(2).toList(collected)
        }
        advanceUntilIdle()

        dao.upsert(
            SettingsPreferenceEntity(
                id = 1,
                discoveryServerInput = null,
                developerModeEnabled = false,
                themeMode = NdiThemeMode.DARK.name,
                accentColorId = ThemeAccentPalette.defaultAccentColorId,
                updatedAtEpochMillis = 2L,
            ),
        )
        advanceUntilIdle()

        collectionJob.join()
        assertEquals(listOf(NdiThemeMode.SYSTEM, NdiThemeMode.DARK), collected.map { it.themeMode })
    }

    @Test
    fun saveThemePreference_preservesDiscoveryAndDeveloperFields() = runTest {
        val dao = FakeSettingsPreferenceDao(
            SettingsPreferenceEntity(
                id = 1,
                discoveryServerInput = "10.0.0.10:5960",
                developerModeEnabled = true,
                themeMode = NdiThemeMode.SYSTEM.name,
                accentColorId = ThemeAccentPalette.defaultAccentColorId,
                updatedAtEpochMillis = 1L,
            ),
        )
        val repository = ThemeEditorRepositoryImpl(dao)

        repository.saveThemePreference(
            com.ndi.feature.themeeditor.domain.model.ThemePreference(
                themeMode = NdiThemeMode.DARK,
                accentColorId = ThemeAccentPalette.ACCENT_RED,
                updatedAtEpochMillis = 2L,
            ),
        )

        val saved = dao.get() ?: error("Expected stored entity")
        assertEquals("10.0.0.10:5960", saved.discoveryServerInput)
        assertEquals(true, saved.developerModeEnabled)
        assertEquals(NdiThemeMode.DARK.name, saved.themeMode)
        assertEquals(ThemeAccentPalette.ACCENT_RED, saved.accentColorId)
    }

    private class FakeSettingsPreferenceDao(
        private var entity: SettingsPreferenceEntity?,
    ) : SettingsPreferenceDao {
        private val state = MutableStateFlow(entity)

        override suspend fun get(): SettingsPreferenceEntity? = entity

        override fun observe(): Flow<SettingsPreferenceEntity?> = state

        override suspend fun upsert(entity: SettingsPreferenceEntity) {
            this.entity = entity
            state.value = entity
        }
    }
}
