package com.ndi.feature.themeeditor

import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.NdiThemeMode
import com.ndi.feature.themeeditor.domain.model.ThemeAccentPalette
import com.ndi.feature.themeeditor.domain.model.ThemePreference
import com.ndi.feature.themeeditor.domain.repository.ThemeEditorRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class ThemeEditorPersistenceTest {

    private lateinit var repository: FakeThemeEditorRepository

    @Before
    fun setUp() {
        repository = FakeThemeEditorRepository()
        ThemeEditorDependencies.themeEditorRepositoryProvider = { repository }
    }

    @Test
    fun relaunchRestoresPersistedThemeState() {
        repository.saveBlocking(
            ThemePreference(
                themeMode = NdiThemeMode.DARK,
                accentColorId = ThemeAccentPalette.ACCENT_ORANGE,
                updatedAtEpochMillis = 42L,
            ),
        )

        val firstLaunch = launchFragmentInContainer<ThemeEditorFragment>()
        firstLaunch.close()

        val secondLaunch = launchFragmentInContainer<ThemeEditorFragment>()
        secondLaunch.onFragment {
            assertEquals(NdiThemeMode.DARK, repository.state.value.themeMode)
            assertEquals(ThemeAccentPalette.ACCENT_ORANGE, repository.state.value.accentColorId)
        }
    }

    private class FakeThemeEditorRepository : ThemeEditorRepository {
        val state = MutableStateFlow(
            ThemePreference(
                themeMode = NdiThemeMode.SYSTEM,
                accentColorId = ThemeAccentPalette.defaultAccentColorId,
                updatedAtEpochMillis = 0L,
            ),
        )

        override suspend fun getThemePreference(): ThemePreference = state.value

        override suspend fun saveThemePreference(preference: ThemePreference) {
            state.value = preference
        }

        override fun observeThemePreference(): Flow<ThemePreference> = state

        fun saveBlocking(preference: ThemePreference) {
            state.value = preference
        }
    }
}
