package com.ndi.feature.ndibrowser.settings

import androidx.lifecycle.viewModelScope
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
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
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
    fun onDiscoveryServerChanged_emptyInput_isValid() = runTest(scheduler) {
        val viewModel = SettingsViewModel(FakeSettingsRepository())

        viewModel.onDiscoveryServerChanged("")
        advanceUntilIdle()

        assertNull(viewModel.uiState.value.validationError)
    }

    @Test
    fun onDiscoveryServerChanged_validHost_isValid() = runTest(scheduler) {
        val viewModel = SettingsViewModel(FakeSettingsRepository())

        viewModel.onDiscoveryServerChanged("valid-host")
        advanceUntilIdle()

        assertNull(viewModel.uiState.value.validationError)
    }

    @Test
    fun onDiscoveryServerChanged_unbracketedIpv6_setsValidationError() = runTest(scheduler) {
        val viewModel = SettingsViewModel(FakeSettingsRepository())

        viewModel.onDiscoveryServerChanged("::1")
        advanceUntilIdle()

        assertTrue(viewModel.uiState.value.validationError != null)
    }

    @Test
    fun onDiscoveryServerChanged_portOutOfRange_setsValidationError() = runTest(scheduler) {
        val viewModel = SettingsViewModel(FakeSettingsRepository())

        viewModel.onDiscoveryServerChanged("host:99999")
        advanceUntilIdle()

        assertTrue(viewModel.uiState.value.validationError != null)
    }

    @Test
    fun onDiscoveryServerChanged_bracketedIpv6WithPort_isValid() = runTest(scheduler) {
        val viewModel = SettingsViewModel(FakeSettingsRepository())

        viewModel.onDiscoveryServerChanged("[::1]:5960")
        advanceUntilIdle()

        assertNull(viewModel.uiState.value.validationError)
    }

    @Test
    fun onSaveSettings_validState_callsRepositorySave() = runTest(scheduler) {
        val repository = FakeSettingsRepository()
        val viewModel = SettingsViewModel(repository)

        viewModel.onDiscoveryServerChanged("ndi-server.local")
        viewModel.onDeveloperModeToggled(true)
        viewModel.onSaveSettings()
        advanceUntilIdle()

        assertEquals(1, repository.savedSnapshots.size)
        assertEquals("ndi-server.local", repository.savedSnapshots.first().discoveryServerInput)
        assertTrue(repository.savedSnapshots.first().developerModeEnabled)
    }

    @Test
    fun onSaveSettings_invalidState_doesNotSaveAndKeepsValidationError() = runTest(scheduler) {
        val repository = FakeSettingsRepository()
        val viewModel = SettingsViewModel(repository)

        viewModel.onDiscoveryServerChanged("host:99999")
        viewModel.onSaveSettings()
        advanceUntilIdle()

        assertTrue(viewModel.uiState.value.validationError != null)
        assertTrue(repository.savedSnapshots.isEmpty())
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
}

private class FakeSettingsRepository : NdiSettingsRepository {
    private val flow = MutableStateFlow(
        NdiSettingsSnapshot(
            discoveryServerInput = null,
            developerModeEnabled = false,
            updatedAtEpochMillis = 0L,
        ),
    )

    val savedSnapshots = mutableListOf<NdiSettingsSnapshot>()

    override suspend fun getSettings(): NdiSettingsSnapshot = flow.value

    override suspend fun saveSettings(snapshot: NdiSettingsSnapshot) {
        savedSnapshots += snapshot
        flow.value = snapshot
    }

    override fun observeSettings(): Flow<NdiSettingsSnapshot> = flow
}