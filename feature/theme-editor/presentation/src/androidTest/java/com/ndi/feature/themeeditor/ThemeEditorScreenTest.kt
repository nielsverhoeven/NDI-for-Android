package com.ndi.feature.themeeditor

import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.feature.themeeditor.domain.model.ThemeAccentPalette
import com.ndi.feature.themeeditor.domain.model.ThemePreference
import com.ndi.feature.themeeditor.domain.repository.ThemeEditorRepository
import com.ndi.feature.themeeditor.presentation.R
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.Assert.assertNotNull
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class ThemeEditorScreenTest {

    @Before
    fun setUp() {
        ThemeEditorDependencies.themeEditorRepositoryProvider = { FakeThemeEditorRepository() }
    }

    @Test
    fun themeModeControls_areRendered() {
        val scenario = launchFragmentInContainer<ThemeEditorFragment>()
        scenario.onFragment { fragment ->
            assertNotNull(fragment.view?.findViewById<android.view.View>(R.id.themeModeLight))
            assertNotNull(fragment.view?.findViewById<android.view.View>(R.id.themeModeDark))
            assertNotNull(fragment.view?.findViewById<android.view.View>(R.id.themeModeSystem))
        }
    }

    @Test
    fun accentPaletteControls_areRendered() {
        val scenario = launchFragmentInContainer<ThemeEditorFragment>()
        scenario.onFragment { fragment ->
            assertNotNull(fragment.view?.findViewById<android.view.View>(R.id.accentBlue))
            assertNotNull(fragment.view?.findViewById<android.view.View>(R.id.accentTeal))
            assertNotNull(fragment.view?.findViewById<android.view.View>(R.id.accentGreen))
            assertNotNull(fragment.view?.findViewById<android.view.View>(R.id.accentOrange))
            assertNotNull(fragment.view?.findViewById<android.view.View>(R.id.accentRed))
            assertNotNull(fragment.view?.findViewById<android.view.View>(R.id.accentPink))
        }
    }

    @Test
    fun persistedThemeValues_areRestoredOnLaunch() {
        val repository = FakeThemeEditorRepository()
        repository.saveBlocking(
            ThemePreference(
                themeMode = com.ndi.core.model.NdiThemeMode.DARK,
                accentColorId = ThemeAccentPalette.ACCENT_RED,
                updatedAtEpochMillis = 10L,
            ),
        )
        ThemeEditorDependencies.themeEditorRepositoryProvider = { repository }

        val scenario = launchFragmentInContainer<ThemeEditorFragment>()
        scenario.onFragment { fragment ->
            assertNotNull(fragment.view?.findViewById<android.view.View>(R.id.themeModeDark))
            assertNotNull(fragment.view?.findViewById<android.view.View>(R.id.accentRed))
        }
    }

    private class FakeThemeEditorRepository : ThemeEditorRepository {
        private val state = MutableStateFlow(
            ThemePreference(
                themeMode = com.ndi.core.model.NdiThemeMode.SYSTEM,
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
