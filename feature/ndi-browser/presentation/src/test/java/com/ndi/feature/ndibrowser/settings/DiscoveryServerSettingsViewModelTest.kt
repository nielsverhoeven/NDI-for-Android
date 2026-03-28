package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.DEFAULT_DISCOVERY_SERVER_PORT
import com.ndi.core.model.DiscoveryServerEntry
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
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
}
