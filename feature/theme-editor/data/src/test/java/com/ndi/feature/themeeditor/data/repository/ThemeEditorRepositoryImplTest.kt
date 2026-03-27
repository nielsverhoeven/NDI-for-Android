package com.ndi.feature.themeeditor.data.repository

import com.ndi.core.database.SettingsPreferenceDao
import com.ndi.core.database.SettingsPreferenceEntity
import com.ndi.core.model.NdiThemeMode
import com.ndi.feature.themeeditor.domain.model.ThemeAccentPalette
import kotlinx.coroutines.ExperimentalCoroutinesApi
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

    private class FakeSettingsPreferenceDao(
        private var entity: SettingsPreferenceEntity?,
    ) : SettingsPreferenceDao {
        override suspend fun get(): SettingsPreferenceEntity? = entity

        override suspend fun upsert(entity: SettingsPreferenceEntity) {
            this.entity = entity
        }
    }
}
