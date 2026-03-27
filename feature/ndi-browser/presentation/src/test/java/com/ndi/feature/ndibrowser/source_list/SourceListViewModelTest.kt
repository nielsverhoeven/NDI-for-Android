package com.ndi.feature.ndibrowser.source_list

import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.NdiSource
import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.ndibrowser.testutil.MainDispatcherRule
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertFalse
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import java.util.UUID

@OptIn(ExperimentalCoroutinesApi::class)
class SourceListViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun onScreenVisible_startsRefreshAndPublishesRepositoryState() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        repository.emit(successSnapshot())
        viewModel.onScreenVisible()
        advanceUntilIdle()

        assertTrue(repository.autoRefreshStarted)
        assertEquals(DiscoveryStatus.SUCCESS, viewModel.uiState.value.discoveryStatus)
        assertEquals(1, viewModel.uiState.value.sources.size)
    }

    @Test
    fun onManualRefresh_requestsManualDiscovery() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        viewModel.onManualRefresh()
        advanceUntilIdle()

        assertEquals(listOf(DiscoveryTrigger.MANUAL), repository.discoveryRequests)
    }

    @Test
    fun onDiscoverySnapshot_filtersOutCurrentDeviceSource() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        repository.emit(
            snapshotWithSources(
                status = DiscoveryStatus.SUCCESS,
                sources = listOf(
                    NdiSource(sourceId = "device-screen:local", displayName = "This Device", lastSeenAtEpochMillis = 1L),
                    NdiSource(sourceId = "camera-1", displayName = "Camera 1", lastSeenAtEpochMillis = 2L),
                ),
            ),
        )
        advanceUntilIdle()

        assertEquals(listOf("camera-1"), viewModel.uiState.value.sources.map { it.sourceId })
    }

    @Test
    fun inProgressRefresh_preservesPreviouslyVisibleList_andSetsRefreshing() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        repository.emit(
            snapshotWithSources(
                status = DiscoveryStatus.SUCCESS,
                sources = listOf(NdiSource(sourceId = "camera-1", displayName = "Camera 1", lastSeenAtEpochMillis = 2L)),
            ),
        )
        advanceUntilIdle()

        repository.emit(
            snapshotWithSources(
                status = DiscoveryStatus.IN_PROGRESS,
                sources = emptyList(),
            ),
        )
        advanceUntilIdle()

        assertTrue(viewModel.uiState.value.isRefreshing)
        assertEquals(listOf("camera-1"), viewModel.uiState.value.sources.map { it.sourceId })
        assertNull(viewModel.uiState.value.refreshErrorMessage)
    }

    @Test
    fun onSourceSelected_supportsReservedDeviceScreenIdentity() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val selectionRepository = InMemoryUserSelectionRepository()
        val viewModel = SourceListViewModel(repository, selectionRepository, SourceListTelemetryEmitter {})

        viewModel.onSourceSelected("device-screen:local")
        advanceUntilIdle()

        assertEquals("device-screen:local", viewModel.uiState.value.highlightedSourceId)
        assertEquals("device-screen:local", selectionRepository.getLastSelectedSource())
    }

    @Test
    fun onSourceSelected_persistsDiscoverableSourceIdentity() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val selectionRepository = InMemoryUserSelectionRepository()
        val viewModel = SourceListViewModel(repository, selectionRepository, SourceListTelemetryEmitter {})

        viewModel.onSourceSelected("camera-discovered")
        advanceUntilIdle()

        assertEquals("camera-discovered", viewModel.uiState.value.highlightedSourceId)
        assertEquals("camera-discovered", selectionRepository.getLastSelectedSource())
    }

    @Test
    fun onSourceSelected_emitsViewerNavigationEvent() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        var emitted: String? = null
        val collector = launch {
            emitted = viewModel.navigationEvents.first()
        }

        viewModel.onSourceSelected("camera-42")
        advanceUntilIdle()

        assertEquals("camera-42", emitted)
        collector.cancel()
    }

    @Test
    fun onSourceSelected_emitsViewSelectionOpenedViewerTelemetry() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val emitted = mutableListOf<TelemetryEvent>()
        val viewModel = SourceListViewModel(
            repository,
            InMemoryUserSelectionRepository(),
            SourceListTelemetryEmitter { event -> emitted += event },
        )

        viewModel.onSourceSelected("camera-telemetry")
        advanceUntilIdle()

        assertTrue(
            emitted.any { event ->
                event.name == TelemetryEvent.VIEW_SELECTION_OPENED_VIEWER &&
                    event.attributes["sourceId"] == "camera-telemetry"
            },
        )
    }

    @Test
    fun refreshFailure_preservesExistingList_andSetsInlineError() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        repository.emit(
            snapshotWithSources(
                status = DiscoveryStatus.SUCCESS,
                sources = listOf(NdiSource(sourceId = "camera-1", displayName = "Camera 1", lastSeenAtEpochMillis = 2L)),
            ),
        )
        advanceUntilIdle()

        repository.emit(
            snapshotWithSources(
                status = DiscoveryStatus.FAILURE,
                sources = emptyList(),
                errorMessage = "Network unavailable",
            ),
        )
        advanceUntilIdle()

        assertFalse(viewModel.uiState.value.isRefreshing)
        assertEquals(listOf("camera-1"), viewModel.uiState.value.sources.map { it.sourceId })
        assertEquals("Network unavailable", viewModel.uiState.value.refreshErrorMessage)
    }

    @Test
    fun refreshSuccess_clearsInlineError_andReenablesRefresh() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        repository.emit(
            snapshotWithSources(
                status = DiscoveryStatus.FAILURE,
                sources = emptyList(),
                errorMessage = "Transient failure",
            ),
        )
        advanceUntilIdle()

        repository.emit(
            snapshotWithSources(
                status = DiscoveryStatus.SUCCESS,
                sources = listOf(NdiSource(sourceId = "camera-2", displayName = "Camera 2", lastSeenAtEpochMillis = 3L)),
            ),
        )
        advanceUntilIdle()

        assertFalse(viewModel.uiState.value.isRefreshing)
        assertNull(viewModel.uiState.value.refreshErrorMessage)
        assertEquals(listOf("camera-2"), viewModel.uiState.value.sources.map { it.sourceId })
    }

    @Test
    fun onSettingsTogglePressed_emitsOnceUntilSettled() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        var emissionCount = 0
        val collector = launch(start = CoroutineStart.UNDISPATCHED) {
            viewModel.settingsToggleEvents.collect {
                emissionCount += 1
            }
        }

        viewModel.onSettingsTogglePressed()
        viewModel.onSettingsTogglePressed()
        advanceUntilIdle()

        assertEquals(1, emissionCount)

        viewModel.onSettingsToggleSettled()
        viewModel.onSettingsTogglePressed()
        advanceUntilIdle()

        assertEquals(2, emissionCount)
        collector.cancel()
    }
}

private class FakeDiscoveryRepository : NdiDiscoveryRepository {
    private val snapshots = MutableStateFlow(
        DiscoverySnapshot(
            snapshotId = UUID.randomUUID().toString(),
            startedAtEpochMillis = 0L,
            completedAtEpochMillis = 0L,
            status = DiscoveryStatus.EMPTY,
            sourceCount = 0,
            sources = emptyList(),
        ),
    )

    val discoveryRequests = mutableListOf<DiscoveryTrigger>()
    var autoRefreshStarted = false

    override suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot {
        discoveryRequests += trigger
        return snapshots.value
    }

    override fun observeDiscoveryState(): Flow<DiscoverySnapshot> = snapshots

    override fun startForegroundAutoRefresh(intervalSeconds: Int) {
        autoRefreshStarted = true
    }

    override fun stopForegroundAutoRefresh() = Unit

    fun emit(snapshot: DiscoverySnapshot) {
        snapshots.value = snapshot
    }
}

private class InMemoryUserSelectionRepository : UserSelectionRepository {
    private var sourceId: String? = null

    override suspend fun saveLastSelectedSource(sourceId: String) {
        this.sourceId = sourceId
    }

    override suspend fun getLastSelectedSource(): String? = sourceId
}

private fun successSnapshot(): DiscoverySnapshot {
    return DiscoverySnapshot(
        snapshotId = UUID.randomUUID().toString(),
        startedAtEpochMillis = 1L,
        completedAtEpochMillis = 2L,
        status = DiscoveryStatus.SUCCESS,
        sourceCount = 1,
        sources = listOf(NdiSource("camera-1", "Camera 1", lastSeenAtEpochMillis = 2L)),
    )
}

private fun snapshotWithSources(
    status: DiscoveryStatus,
    sources: List<NdiSource>,
    errorMessage: String? = null,
): DiscoverySnapshot {
    return DiscoverySnapshot(
        snapshotId = UUID.randomUUID().toString(),
        startedAtEpochMillis = 1L,
        completedAtEpochMillis = 2L,
        status = status,
        sourceCount = sources.size,
        sources = sources,
        errorMessage = errorMessage,
    )
}
