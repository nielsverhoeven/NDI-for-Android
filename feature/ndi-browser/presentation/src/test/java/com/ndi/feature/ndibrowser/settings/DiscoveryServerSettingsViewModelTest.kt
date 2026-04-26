package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.DEFAULT_DISCOVERY_SERVER_PORT
import com.ndi.core.model.CompatibilityGuidance
import com.ndi.core.model.DiscoveryCompatibilityStatus
import com.ndi.core.model.DeveloperDiscoveryDiagnostics
import com.ndi.core.model.DiscoveryCheckOutcome
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.feature.ndibrowser.domain.repository.DeveloperDiagnosticsRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import com.ndi.core.model.DiscoveryServerEntry
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class DiscoveryServerSettingsViewModelTest {

    private val testDispatcher = StandardTestDispatcher()
    private lateinit var fakeRepository: FakeDiscoveryServerRepository
    private lateinit var viewModel: DiscoveryServerSettingsViewModel

    @Before
    fun setUp() {
        Dispatchers.setMain(testDispatcher)
        fakeRepository = FakeDiscoveryServerRepository()
        viewModel = DiscoveryServerSettingsViewModel(fakeRepository)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    // ---- US1: Required-host validation ----

    @Test
    fun `onHostInputChanged with blank host sets validation error`() = runTest {
        viewModel.onHostInputChanged("")
        val state = viewModel.uiState.value
        assertNotNull("Expected validation error for blank host", state.validationError)
        assertFalse("Save should be disabled when host is blank", state.isSaveEnabled)
    }

    @Test
    fun `onHostInputChanged with valid host clears validation error`() = runTest {
        viewModel.onHostInputChanged("valid-host.local")
        val state = viewModel.uiState.value
        assertNull("Expected no validation error for valid host", state.validationError)
        assertTrue("Save should be enabled when host is valid", state.isSaveEnabled)
    }

    // ---- US1: Blank-port defaulting ----

    @Test
    fun `onAddServerClicked with blank port saves server with default port 5959`() = runTest {
        viewModel.onHostInputChanged("ndi-server.local")
        viewModel.onPortInputChanged("")
        viewModel.onAddServerClicked()
        testDispatcher.scheduler.advanceUntilIdle()

        val servers = viewModel.uiState.value.servers
        assertEquals(1, servers.size)
        assertEquals(DEFAULT_DISCOVERY_SERVER_PORT, servers.first().port)
    }

    @Test
    fun `onAddServerClicked with explicit port saves server with provided port`() = runTest {
        viewModel.onHostInputChanged("ndi-server.local")
        viewModel.onPortInputChanged("5961")
        viewModel.onAddServerClicked()
        testDispatcher.scheduler.advanceUntilIdle()

        val servers = viewModel.uiState.value.servers
        assertEquals(1, servers.size)
        assertEquals(5961, servers.first().port)
    }

    @Test
    fun `onAddServerClicked with invalid port sets validation error and blocks save`() = runTest {
        viewModel.onHostInputChanged("ndi-server.local")
        viewModel.onPortInputChanged("notaport")
        val state = viewModel.uiState.value
        assertNotNull("Expected validation error for invalid port", state.validationError)
        assertFalse("Save should be disabled with invalid port", state.isSaveEnabled)
    }

    @Test
    fun `onAddServerClicked without host shows validation error and does not save`() = runTest {
        viewModel.onHostInputChanged("")
        viewModel.onAddServerClicked()
        testDispatcher.scheduler.advanceUntilIdle()

        val servers = viewModel.uiState.value.servers
        assertEquals(0, servers.size)
        assertNotNull(viewModel.uiState.value.validationError)
    }

    // ---- US3: Per-server toggle (failing tests — implementation comes in Phase 5) ----

    @Test
    fun `onToggleServerClicked persists enabled state`() = runTest {
        fakeRepository.seedServer("toggle-me.local", DEFAULT_DISCOVERY_SERVER_PORT)
        viewModel.onScreenVisible()
        testDispatcher.scheduler.advanceUntilIdle()

        val serverId = viewModel.uiState.value.servers.first().id
        viewModel.onToggleServerClicked(serverId, false)
        testDispatcher.scheduler.advanceUntilIdle()

        assertFalse(viewModel.uiState.value.servers.first().enabled)
    }

    @Test
    fun `noEnabledServersWarning is shown when all servers are disabled`() = runTest {
        fakeRepository.seedServer("solo.local", DEFAULT_DISCOVERY_SERVER_PORT)
        viewModel.onScreenVisible()
        testDispatcher.scheduler.advanceUntilIdle()

        val serverId = viewModel.uiState.value.servers.first().id
        viewModel.onToggleServerClicked(serverId, false)
        testDispatcher.scheduler.advanceUntilIdle()

        assertNotNull(viewModel.uiState.value.noEnabledServersWarning)
    }

    // ---- US4: Edit validation (failing tests — implementation comes in Phase 4a) ----

    @Test
    fun `onSaveEditClicked with duplicate host+port shows validation error`() = runTest {
        fakeRepository.seedServer("existing.local", DEFAULT_DISCOVERY_SERVER_PORT)
        fakeRepository.seedServer("other.local", 5960)
        viewModel.onScreenVisible()
        testDispatcher.scheduler.advanceUntilIdle()

        val otherId = viewModel.uiState.value.servers.last().id
        viewModel.onEditServerClicked(otherId)
        viewModel.onHostInputChanged("existing.local")
        viewModel.onPortInputChanged("$DEFAULT_DISCOVERY_SERVER_PORT")
        viewModel.onSaveEditClicked()
        testDispatcher.scheduler.advanceUntilIdle()

        assertNotNull(viewModel.uiState.value.validationError)
    }

    @Test
    fun `onDeleteServerClicked removes server from state`() = runTest {
        fakeRepository.seedServer("delete-me.local", DEFAULT_DISCOVERY_SERVER_PORT)
        viewModel.onScreenVisible()
        testDispatcher.scheduler.advanceUntilIdle()

        val serverId = viewModel.uiState.value.servers.first().id
        viewModel.onDeleteServerClicked(serverId)
        testDispatcher.scheduler.advanceUntilIdle()

        assertEquals(0, viewModel.uiState.value.servers.size)
    }

    // ---- Spec 022 T014: lastCheckResult ----

    @Test
    fun `addServer success emits checkResult with SUCCESS outcome in uiState`() = runTest {
        viewModel.onHostInputChanged("server.local")
        viewModel.onAddServerClicked()
        testDispatcher.scheduler.advanceUntilIdle()

        val state = viewModel.uiState.value
        assertNotNull("uiState must have a lastCheckResult after adding a server", state.lastCheckResult)
        assertEquals(DiscoveryCheckOutcome.SUCCESS, state.lastCheckResult?.outcome)
    }

    @Test
    fun `uiState does not expose lastCheckResult before first add`() = runTest {
        val state = viewModel.uiState.value
        assertNull(state.lastCheckResult)
    }

    @Test
    fun `developer mode ON exposes developer discovery diagnostics in ui state`() = runTest {
        val settingsRepository = FakeSettingsRepository(
            initial = NdiSettingsSnapshot(
                discoveryServerInput = null,
                developerModeEnabled = true,
                updatedAtEpochMillis = 0L,
            ),
        )
        val diagnosticsRepository = FakeDeveloperDiagnosticsRepository(
            diagnostics = DeveloperDiscoveryDiagnostics(
                developerModeEnabled = true,
                latestDiscoveryRefreshStatus = DiscoveryStatus.SUCCESS,
                latestDiscoveryRefreshAtEpochMillis = 1234L,
                serverStatusRollup = emptyList(),
                recentDiscoveryLogs = listOf("log"),
                compatibilityGuidance = listOf(
                    CompatibilityGuidance(
                        targetId = "venue",
                        status = DiscoveryCompatibilityStatus.BLOCKED,
                        message = "blocked",
                        recommendedNextStep = "verify endpoint",
                    ),
                ),
            ),
        )
        val diagnosticsViewModel = DiscoveryServerSettingsViewModel(
            repository = fakeRepository,
            settingsRepository = settingsRepository,
            developerDiagnosticsRepository = diagnosticsRepository,
        )

        testDispatcher.scheduler.advanceUntilIdle()

        assertNotNull(diagnosticsViewModel.uiState.value.developerDiscoveryDiagnostics)
        assertEquals(
            1,
            diagnosticsViewModel.uiState.value.developerDiscoveryDiagnostics?.compatibilityGuidance?.size,
        )
    }

    @Test
    fun `developer mode OFF clears developer discovery diagnostics from ui state`() = runTest {
        val settingsRepository = FakeSettingsRepository(
            initial = NdiSettingsSnapshot(
                discoveryServerInput = null,
                developerModeEnabled = false,
                updatedAtEpochMillis = 0L,
            ),
        )
        val diagnosticsRepository = FakeDeveloperDiagnosticsRepository(
            diagnostics = DeveloperDiscoveryDiagnostics(
                developerModeEnabled = true,
                latestDiscoveryRefreshStatus = DiscoveryStatus.SUCCESS,
                latestDiscoveryRefreshAtEpochMillis = 1234L,
                serverStatusRollup = emptyList(),
                recentDiscoveryLogs = listOf("log"),
                compatibilityGuidance = listOf(
                    CompatibilityGuidance(
                        targetId = "venue",
                        status = DiscoveryCompatibilityStatus.BLOCKED,
                        message = "blocked",
                        recommendedNextStep = "verify endpoint",
                    ),
                ),
            ),
        )
        val diagnosticsViewModel = DiscoveryServerSettingsViewModel(
            repository = fakeRepository,
            settingsRepository = settingsRepository,
            developerDiagnosticsRepository = diagnosticsRepository,
        )

        testDispatcher.scheduler.advanceUntilIdle()

        assertNull(diagnosticsViewModel.uiState.value.developerDiscoveryDiagnostics)
    }

    private class FakeSettingsRepository(initial: NdiSettingsSnapshot) : NdiSettingsRepository {
        private val state = MutableStateFlow(initial)

        override suspend fun getSettings(): NdiSettingsSnapshot = state.value

        override suspend fun saveSettings(snapshot: NdiSettingsSnapshot) {
            state.value = snapshot
        }

        override fun observeSettings(): Flow<NdiSettingsSnapshot> = state
    }

    private class FakeDeveloperDiagnosticsRepository(
        diagnostics: DeveloperDiscoveryDiagnostics,
    ) : DeveloperDiagnosticsRepository {
        private val diagnosticsState = MutableStateFlow(diagnostics)

        override fun observeOverlayState() = MutableStateFlow(
            com.ndi.core.model.NdiDeveloperOverlayState(
                visible = false,
                mode = com.ndi.core.model.NdiOverlayMode.DISABLED,
                streamDirectionLabel = "",
                streamStatusLabel = "",
                sessionId = null,
                streamSourceLabel = null,
                warningMessage = null,
                recentLogs = emptyList(),
                updatedAtEpochMillis = 0L,
            ),
        )

        override fun observeRecentLogs() = MutableStateFlow(emptyList<com.ndi.core.model.NdiRedactedLogEntry>())

        override fun observeDiscoveryDiagnostics(): Flow<DeveloperDiscoveryDiagnostics> = diagnosticsState
    }
}
