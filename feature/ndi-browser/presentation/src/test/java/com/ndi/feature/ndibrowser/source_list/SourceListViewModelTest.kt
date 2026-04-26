package com.ndi.feature.ndibrowser.source_list

import com.ndi.core.model.DiscoveryCompatibilityResult
import com.ndi.core.model.DiscoveryCompatibilitySnapshot
import com.ndi.core.model.DiscoveryCompatibilityStatus
import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.CachedSourceRecord
import com.ndi.core.model.CachedSourceValidationState
import com.ndi.core.model.NdiSource
import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.ndibrowser.domain.repository.CachedSourceRepository
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

    @Test
    fun compatibilitySnapshot_mixedOutcomes_setsPartialCompatibilityState() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        repository.emitCompatibility(
            DiscoveryCompatibilitySnapshot(
                recordedAtEpochMillis = 100L,
                results = listOf(
                    DiscoveryCompatibilityResult(
                        targetId = "reachable.local:5959",
                        status = DiscoveryCompatibilityStatus.LIMITED,
                        discoveredSourceCount = 1,
                        streamStartAttempted = false,
                        streamStartSucceeded = false,
                    ),
                    DiscoveryCompatibilityResult(
                        targetId = "unreachable.local:5959",
                        status = DiscoveryCompatibilityStatus.BLOCKED,
                        discoveredSourceCount = 0,
                        streamStartAttempted = false,
                        streamStartSucceeded = false,
                    ),
                ),
            ),
        )
        advanceUntilIdle()

        assertTrue(viewModel.uiState.value.hasPartialCompatibility)
        assertTrue(viewModel.uiState.value.compatibilityMessage?.contains("not compatible") == true)
    }

    @Test
    fun compatibilitySnapshot_noMixedOutcomes_clearsPartialCompatibilityState() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        repository.emitCompatibility(
            DiscoveryCompatibilitySnapshot(
                recordedAtEpochMillis = 100L,
                results = listOf(
                    DiscoveryCompatibilityResult(
                        targetId = "reachable.local:5959",
                        status = DiscoveryCompatibilityStatus.LIMITED,
                        discoveredSourceCount = 1,
                        streamStartAttempted = false,
                        streamStartSucceeded = false,
                    ),
                ),
            ),
        )
        advanceUntilIdle()

        assertFalse(viewModel.uiState.value.hasPartialCompatibility)
        assertNull(viewModel.uiState.value.compatibilityMessage)
    }

    // ---- T014: Multicast Fallback Discovery - Presentation Tests (US1 Phase 3) ----

    @Test
    fun onDiscoverySnapshot_surfacesMulticastResultsWithoutGating() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        // Simulate multicast discovery result (no enabled servers configured)
        repository.emit(
            snapshotWithSources(
                status = DiscoveryStatus.SUCCESS,
                sources = listOf(
                    NdiSource(sourceId = "device-screen:local", displayName = "This Device", lastSeenAtEpochMillis = 1L),
                    NdiSource(sourceId = "multicast-camera-1", displayName = "Multicast Camera 1", lastSeenAtEpochMillis = 2L),
                    NdiSource(sourceId = "multicast-camera-2", displayName = "Multicast Camera 2", lastSeenAtEpochMillis = 3L),
                ),
            ),
        )
        advanceUntilIdle()

        // Verify multicast results appear without gating
        assertEquals(2, viewModel.uiState.value.sources.size)
        assertEquals(
            listOf("multicast-camera-1", "multicast-camera-2"),
            viewModel.uiState.value.sources.map { it.sourceId },
        )
        assertEquals(DiscoveryStatus.SUCCESS, viewModel.uiState.value.discoveryStatus)
    }

    @Test
    fun emptyMulticastDiscovery_showsEmptyStateWithoutError() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

        // Simulate empty multicast discovery result
        repository.emit(
            snapshotWithSources(
                status = DiscoveryStatus.EMPTY,
                sources = listOf(NdiSource(sourceId = "device-screen:local", displayName = "This Device", lastSeenAtEpochMillis = 1L)),
            ),
        )
        advanceUntilIdle()

        assertEquals(0, viewModel.uiState.value.sources.size)
        assertEquals(DiscoveryStatus.EMPTY, viewModel.uiState.value.discoveryStatus)
        assertNull(viewModel.uiState.value.refreshErrorMessage)
    }

    // ---- T039: Cache Visibility on Relaunch - Presentation Tests (US3 Phase 5) ----

    @Test
    fun startup_emitsCachedSourcesBeforeLiveDiscoveryCompletes_FR008() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val cachedRepository = FakeCachedSourceRepository()
        SourceListDependencies.cachedSourceRepositoryProvider = { cachedRepository }

        try {
            repository.emit(snapshotWithSources(status = DiscoveryStatus.IN_PROGRESS, sources = emptyList()))
            val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

            cachedRepository.emit(
                listOf(
                    cachedRecord(
                        stableSourceId = "cached-camera-1",
                        displayName = "Cached Camera 1",
                        endpointHost = "10.3.0.1",
                        endpointPort = 5960,
                        lastDiscoveredAt = 1_500L,
                        lastValidatedAt = 1_500L,
                    ),
                ),
            )
            advanceUntilIdle()

            assertEquals(DiscoveryStatus.IN_PROGRESS, viewModel.uiState.value.discoveryStatus)
            assertEquals(listOf("cached-camera-1"), viewModel.uiState.value.sources.map { it.sourceId })

            repository.emit(
                snapshotWithSources(
                    status = DiscoveryStatus.SUCCESS,
                    sources = listOf(
                        NdiSource(sourceId = "live-camera-1", displayName = "Live Camera 1", lastSeenAtEpochMillis = 2_000L),
                    ),
                ),
            )
            advanceUntilIdle()

            assertEquals(DiscoveryStatus.SUCCESS, viewModel.uiState.value.discoveryStatus)
            assertTrue(viewModel.uiState.value.sources.any { it.sourceId == "live-camera-1" })
        } finally {
            SourceListDependencies.cachedSourceRepositoryProvider = null
        }
    }

    @Test
    fun cacheFlow_updatesWhileLiveDiscoveryStillInProgress_separateFromLiveFlow() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepository()
        val cachedRepository = FakeCachedSourceRepository()
        SourceListDependencies.cachedSourceRepositoryProvider = { cachedRepository }

        try {
            repository.emit(snapshotWithSources(status = DiscoveryStatus.IN_PROGRESS, sources = emptyList()))
            val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepository(), SourceListTelemetryEmitter {})

            cachedRepository.emit(
                listOf(
                    cachedRecord(
                        stableSourceId = "cached-camera-1",
                        displayName = "Cached Camera Old",
                        endpointHost = "10.4.0.1",
                        endpointPort = 5960,
                        lastDiscoveredAt = 1_000L,
                        lastValidatedAt = 1_000L,
                    ),
                ),
            )
            advanceUntilIdle()

            assertEquals(listOf("Cached Camera Old"), viewModel.uiState.value.sources.map { it.displayName })

            cachedRepository.emit(
                listOf(
                    cachedRecord(
                        stableSourceId = "cached-camera-1",
                        displayName = "Cached Camera New",
                        endpointHost = "10.4.0.9",
                        endpointPort = 5961,
                        lastDiscoveredAt = 1_500L,
                        lastValidatedAt = 1_500L,
                    ),
                    cachedRecord(
                        stableSourceId = "cached-camera-2",
                        displayName = "Cached Camera 2",
                        endpointHost = "10.4.0.2",
                        endpointPort = 5960,
                        lastDiscoveredAt = 1_500L,
                        lastValidatedAt = 1_500L,
                    ),
                ),
            )
            advanceUntilIdle()

            assertEquals(
                listOf("Cached Camera New", "Cached Camera 2"),
                viewModel.uiState.value.sources.map { it.displayName },
            )
        } finally {
            SourceListDependencies.cachedSourceRepositoryProvider = null
        }
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
    private val compatibilitySnapshots = MutableStateFlow(
        DiscoveryCompatibilitySnapshot(
            recordedAtEpochMillis = 0L,
            results = emptyList(),
        ),
    )

    val discoveryRequests = mutableListOf<DiscoveryTrigger>()
    var autoRefreshStarted = false

    override suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot {
        discoveryRequests += trigger
        return snapshots.value
    }

    override fun observeDiscoveryState(): Flow<DiscoverySnapshot> = snapshots

    override fun observeCompatibilitySnapshot(): Flow<DiscoveryCompatibilitySnapshot> = compatibilitySnapshots

    override fun startForegroundAutoRefresh(intervalSeconds: Int) {
        autoRefreshStarted = true
    }

    override fun stopForegroundAutoRefresh() = Unit

    fun emit(snapshot: DiscoverySnapshot) {
        snapshots.value = snapshot
    }

    fun emitCompatibility(snapshot: DiscoveryCompatibilitySnapshot) {
        compatibilitySnapshots.value = snapshot
    }
}

private class InMemoryUserSelectionRepository : UserSelectionRepository {
    private var sourceId: String? = null

    override suspend fun saveLastSelectedSource(sourceId: String) {
        this.sourceId = sourceId
    }

    override suspend fun getLastSelectedSource(): String? = sourceId
}

private class FakeCachedSourceRepository : CachedSourceRepository {
    private val records = MutableStateFlow<List<CachedSourceRecord>>(emptyList())

    override fun observeCachedSources(): Flow<List<CachedSourceRecord>> = records

    override suspend fun getCachedSource(cacheKey: String): CachedSourceRecord? {
        return records.value.firstOrNull { it.cacheKey == cacheKey }
    }

    override suspend fun upsertCachedSource(record: CachedSourceRecord) {
        records.value = records.value.filterNot { it.cacheKey == record.cacheKey } + record
    }

    override suspend fun upsertFromDiscovery(record: CachedSourceRecord) {
        upsertCachedSource(record)
    }

    override suspend fun upsertDiscoveryAssociation(
        cacheKey: String,
        discoveryServerId: String,
        observedAtEpochMillis: Long,
    ) = Unit

    override suspend fun markValidationState(
        cacheKey: String,
        state: CachedSourceValidationState,
        validationStartedAtEpochMillis: Long?,
        validatedAtEpochMillis: Long?,
        availableAtEpochMillis: Long?,
    ) = Unit

    fun emit(rows: List<CachedSourceRecord>) {
        records.value = rows
    }
}

private fun cachedRecord(
    stableSourceId: String,
    displayName: String,
    endpointHost: String,
    endpointPort: Int,
    lastDiscoveredAt: Long,
    lastValidatedAt: Long?,
): CachedSourceRecord {
    return CachedSourceRecord(
        cacheKey = "$stableSourceId@$endpointHost:$endpointPort",
        stableSourceId = stableSourceId,
        lastObservedSourceId = stableSourceId,
        displayName = displayName,
        endpointHost = endpointHost,
        endpointPort = endpointPort,
        endpointKey = "$endpointHost:$endpointPort",
        validationState = CachedSourceValidationState.UNAVAILABLE,
        lastValidatedAtEpochMillis = lastValidatedAt,
        firstCachedAtEpochMillis = lastDiscoveredAt,
        lastDiscoveredAtEpochMillis = lastDiscoveredAt,
        updatedAtEpochMillis = lastDiscoveredAt,
    )
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
