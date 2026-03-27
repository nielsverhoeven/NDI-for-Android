package com.ndi.feature.themeeditor

import com.ndi.core.model.NdiThemeMode
import com.ndi.feature.themeeditor.domain.model.ThemeAccentPalette
import com.ndi.feature.themeeditor.domain.model.ThemePreference
import com.ndi.feature.themeeditor.domain.repository.ThemeEditorRepository
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestCoroutineScheduler
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class ThemeEditorViewModelTest {

    private val scheduler = TestCoroutineScheduler()
    private val dispatcher = StandardTestDispatcher(scheduler)

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun onThemeModeSelected_savesSingleSelectedMode() = runTest(scheduler) {
        val repository = FakeThemeEditorRepository()
        val viewModel = ThemeEditorViewModel(repository)

        viewModel.onThemeModeSelected(NdiThemeMode.DARK)
        advanceUntilIdle()

        assertEquals(NdiThemeMode.DARK, repository.state.value.themeMode)
        assertEquals(NdiThemeMode.DARK, viewModel.uiState.value.selectedThemeMode)
    }

    @Test
    fun onThemeModeSelected_switchesBetweenModes() = runTest(scheduler) {
        val repository = FakeThemeEditorRepository()
        val viewModel = ThemeEditorViewModel(repository)

        viewModel.onThemeModeSelected(NdiThemeMode.LIGHT)
        viewModel.onThemeModeSelected(NdiThemeMode.SYSTEM)
        advanceUntilIdle()

        assertEquals(NdiThemeMode.SYSTEM, repository.state.value.themeMode)
        assertEquals(NdiThemeMode.SYSTEM, viewModel.uiState.value.selectedThemeMode)
    }

    @Test
    fun onAccentColorSelected_updatesSingleSelectedAccent() = runTest(scheduler) {
        val repository = FakeThemeEditorRepository()
        val viewModel = ThemeEditorViewModel(repository)

        viewModel.onAccentColorSelected(ThemeAccentPalette.ACCENT_RED)
        advanceUntilIdle()

        assertEquals(ThemeAccentPalette.ACCENT_RED, repository.state.value.accentColorId)
        assertEquals(ThemeAccentPalette.ACCENT_RED, viewModel.uiState.value.selectedAccentColorId)
    }

    @Test
    fun init_readsPersistedThemeValues() = runTest(scheduler) {
        val repository = FakeThemeEditorRepository(
            ThemePreference(
                themeMode = NdiThemeMode.DARK,
                accentColorId = ThemeAccentPalette.ACCENT_ORANGE,
                updatedAtEpochMillis = 10L,
            ),
        )

        val viewModel = ThemeEditorViewModel(repository)
        advanceUntilIdle()

        assertEquals(NdiThemeMode.DARK, viewModel.uiState.value.selectedThemeMode)
        assertEquals(ThemeAccentPalette.ACCENT_ORANGE, viewModel.uiState.value.selectedAccentColorId)
    }

    private class FakeThemeEditorRepository(
        initialPreference: ThemePreference = ThemePreference(
            themeMode = NdiThemeMode.SYSTEM,
            accentColorId = ThemeAccentPalette.defaultAccentColorId,
            updatedAtEpochMillis = 0L,
        ),
    ) : ThemeEditorRepository {
        val state = MutableStateFlow(initialPreference)

        override suspend fun getThemePreference(): ThemePreference = state.value

        override suspend fun saveThemePreference(preference: ThemePreference) {
            state.value = preference
        }

        override fun observeThemePreference(): Flow<ThemePreference> = state
    }
}
