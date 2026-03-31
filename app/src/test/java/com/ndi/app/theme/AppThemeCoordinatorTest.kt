package com.ndi.app.theme

import com.ndi.core.model.NdiThemeMode
import com.ndi.feature.themeeditor.domain.model.ThemeAccentPalette
import com.ndi.feature.themeeditor.domain.model.ThemePreference
import com.ndi.feature.themeeditor.domain.repository.ThemeEditorRepository
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class AppThemeCoordinatorTest {

    @Test
    fun start_mapsLightToNightNo() = runTest {
        val repository = FakeThemeEditorRepository()
        repository.emitThemeMode(NdiThemeMode.LIGHT)
        val coordinator = AppThemeCoordinator(repository, this)

        coordinator.start()
        advanceUntilIdle()

        assertEquals(androidx.appcompat.app.AppCompatDelegate.MODE_NIGHT_NO, coordinator.lastAppliedNightMode)
        coordinator.stop()
    }

    @Test
    fun start_mapsDarkToNightYes() = runTest {
        val repository = FakeThemeEditorRepository()
        repository.emitThemeMode(NdiThemeMode.DARK)
        val coordinator = AppThemeCoordinator(repository, this)

        coordinator.start()
        advanceUntilIdle()

        assertEquals(androidx.appcompat.app.AppCompatDelegate.MODE_NIGHT_YES, coordinator.lastAppliedNightMode)
        coordinator.stop()
    }

    @Test
    fun start_mapsSystemToFollowSystem() = runTest {
        val repository = FakeThemeEditorRepository()
        repository.emitThemeMode(NdiThemeMode.SYSTEM)
        val coordinator = AppThemeCoordinator(repository, this)

        coordinator.start()
        advanceUntilIdle()

        assertEquals(androidx.appcompat.app.AppCompatDelegate.MODE_NIGHT_FOLLOW_SYSTEM, coordinator.lastAppliedNightMode)
        coordinator.stop()
    }

    @Test
    fun start_updatesActiveAccentFlow() = runTest {
        val repository = FakeThemeEditorRepository()
        repository.emitAccent(ThemeAccentPalette.ACCENT_ORANGE)
        val coordinator = AppThemeCoordinator(repository, this)

        coordinator.start()
        advanceUntilIdle()

        assertEquals(ThemeAccentPalette.ACCENT_ORANGE, coordinator.activeAccentColorId.value)
        coordinator.stop()
    }

    @Test
    fun start_reactsToThemeModeUpdatesAfterStart() = runTest {
        val repository = FakeThemeEditorRepository()
        repository.emitThemeMode(NdiThemeMode.LIGHT)
        val coordinator = AppThemeCoordinator(repository, this)

        coordinator.start()
        advanceUntilIdle()

        repository.emitThemeMode(NdiThemeMode.DARK)
        advanceUntilIdle()

        assertEquals(androidx.appcompat.app.AppCompatDelegate.MODE_NIGHT_YES, coordinator.lastAppliedNightMode)
        coordinator.stop()
    }

    @Test
    fun applyNightMode_recordsLatencyMillis() = runTest {
        val repository = FakeThemeEditorRepository()
        val coordinator = AppThemeCoordinator(repository, this)

        coordinator.start()
        advanceUntilIdle()

        val latency = coordinator.lastApplyLatencyMillis
        checkNotNull(latency)
        assertTrue(latency >= 0)
        coordinator.stop()
    }

    private class FakeThemeEditorRepository : ThemeEditorRepository {
        private val state = MutableStateFlow(
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

        fun emitThemeMode(themeMode: NdiThemeMode) {
            state.value = state.value.copy(themeMode = themeMode)
        }

        fun emitAccent(accentColorId: String) {
            state.value = state.value.copy(accentColorId = accentColorId)
        }
    }
}
