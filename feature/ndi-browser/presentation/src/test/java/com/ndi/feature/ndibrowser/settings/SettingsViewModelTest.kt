package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestCoroutineScheduler
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.assertFalse
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.junit.runners.JUnit4

@OptIn(ExperimentalCoroutinesApi::class)
@RunWith(JUnit4::class)
class SettingsViewModelTest {

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
    fun onSaveSettings_persistsDeveloperMode_and_clearsLegacyDiscoveryInput() = runTest(scheduler) {
        val repository = FakeSettingsRepository(
            NdiSettingsSnapshot(
                discoveryServerInput = "legacy-host:5960",
                developerModeEnabled = false,
                themeMode = com.ndi.core.model.NdiThemeMode.DARK,
                accentColorId = "accent_red",
                updatedAtEpochMillis = 0L,
            ),
        )
        val viewModel = SettingsViewModel(repository)
        advanceUntilIdle()

        viewModel.onDeveloperModeToggled(true)
        viewModel.onSaveSettings()
        advanceUntilIdle()

        assertEquals(1, repository.savedSnapshots.size)
        assertEquals(null, repository.savedSnapshots.first().discoveryServerInput)
        assertTrue(repository.savedSnapshots.first().developerModeEnabled)
        assertEquals(com.ndi.core.model.NdiThemeMode.DARK, repository.savedSnapshots.first().themeMode)
        assertEquals("accent_red", repository.savedSnapshots.first().accentColorId)
    }

    @Test
    fun onSaveSettings_withoutInputValidation_alwaysSaves() = runTest(scheduler) {
        val repository = FakeSettingsRepository()
        val viewModel = SettingsViewModel(repository)

        viewModel.onDeveloperModeToggled(false)
        viewModel.onSaveSettings()
        advanceUntilIdle()

        assertEquals(1, repository.savedSnapshots.size)
    }

    @Test
    fun onDeveloperModeToggled_updatesState() = runTest(scheduler) {
        val viewModel = SettingsViewModel(FakeSettingsRepository())
        advanceUntilIdle()  // Let init block run first

        viewModel.onDeveloperModeToggled(true)

        assertTrue(viewModel.uiState.value.developerModeEnabled)
    }

    @Test
    fun onSettingsTogglePressed_emitsOnceUntilSettled() = runTest(scheduler) {
        val viewModel = SettingsViewModel(FakeSettingsRepository())
        var emissionCount = 0
        val collector = launch(start = CoroutineStart.UNDISPATCHED) {
            viewModel.closeSettingsEvents.collect {
                emissionCount += 1
            }
        }

        viewModel.onSettingsTogglePressed()
        viewModel.onSettingsTogglePressed()
        advanceUntilIdle()

        assertEquals(1, emissionCount)

        viewModel.onCloseSettingsSettled()
        viewModel.onSettingsTogglePressed()
        advanceUntilIdle()

        assertEquals(2, emissionCount)
        collector.cancel()
    }

    @Test
    fun onThemeModeChanged_marksDirty_and_onSaveSettings_clearsDirty() = runTest(scheduler) {
        val repository = FakeSettingsRepository()
        val viewModel = SettingsViewModel(repository)
        advanceUntilIdle()

        viewModel.onThemeModeChanged(com.ndi.core.model.NdiThemeMode.DARK)
        assertTrue(viewModel.uiState.value.isDirty)

        viewModel.onSaveSettings()
        advanceUntilIdle()

        assertFalse(viewModel.uiState.value.isDirty)
        assertEquals(com.ndi.core.model.NdiThemeMode.DARK, repository.savedSnapshots.last().themeMode)
    }
}

private class FakeSettingsRepository(
    initialSnapshot: NdiSettingsSnapshot = NdiSettingsSnapshot(
        discoveryServerInput = null,
        developerModeEnabled = false,
        updatedAtEpochMillis = 0L,
    ),
) : NdiSettingsRepository {
    private val flow = MutableStateFlow(initialSnapshot)

    val savedSnapshots = mutableListOf<NdiSettingsSnapshot>()

    override suspend fun getSettings(): NdiSettingsSnapshot = flow.value

    override suspend fun saveSettings(snapshot: NdiSettingsSnapshot) {
        savedSnapshots += snapshot
        flow.value = snapshot
    }

    override fun observeSettings(): Flow<NdiSettingsSnapshot> = flow
}