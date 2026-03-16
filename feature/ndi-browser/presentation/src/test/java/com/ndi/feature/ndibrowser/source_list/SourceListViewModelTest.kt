package com.ndi.feature.ndibrowser.source_list

import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.NdiSource
import com.ndi.feature.ndibrowser.testutil.MainDispatcherRule
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
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
    fun onOutputRequested_emitsOutputNavigation() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        var emitted: String? = null
        val collector = launch {
            emitted = viewModel.outputNavigationEvents.first()
        }

        viewModel.onOutputRequested("camera-output")
        advanceUntilIdle()

        assertEquals("camera-output", emitted)
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
