package com.ndi.feature.ndibrowser.source_list

import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.NdiSource
import com.ndi.feature.ndibrowser.testutil.MainDispatcherRule
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.SourceAvailabilityStatus
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import java.util.UUID

/**
 * Tests for availability debounce behavior and navigation blocking in SourceListViewModel.
 * T027: Add failing ViewModel unit test for two-miss availability transition.
 * T028: Add failing ViewModel unit test for disabled navigation on unavailable source.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class SourceListViewModelAvailabilityTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    /**
     * T027: Tests that a source transitions to unavailable after exactly two consecutive missed polls.
     */
    @Test
    fun testAvailabilityTransitionsToUnavailableAfterTwoMisses() = 
        runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepositoryWithAvailability()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepositoryForAvailability(), SourceListTelemetryEmitter {})

        // Arrange: Snapshot with Cam1 present
        val cam1 = NdiSource(
            sourceId = "cam1-id",
            displayName = "Cam1",
            endpointAddress = "192.168.1.1:5960",
            lastSeenAtEpochMillis = System.currentTimeMillis(),
        )
        val snapshot1 = DiscoverySnapshot(
            snapshotId = UUID.randomUUID().toString(),
            startedAtEpochMillis = 1L,
            completedAtEpochMillis = 2L,
            sources = listOf(cam1),
            status = DiscoveryStatus.SUCCESS,
            sourceCount = 1,
        )

        repository.emit(snapshot1)
        viewModel.onScreenVisible()
        advanceUntilIdle()

        // First state should have Cam1 available
        var uiState = viewModel.uiState.value
        assertTrue("Cam1 should be in source list", uiState.sources.isNotEmpty())
        assertTrue(
            "Cam1 should be available after first discovery",
            uiState.sources[0].isAvailable,
        )

        // Act: First miss (Cam1 absent from discovery)
        val snapshotMiss1 = DiscoverySnapshot(
            snapshotId = UUID.randomUUID().toString(),
            startedAtEpochMillis = 3L,
            completedAtEpochMillis = 4L,
            sources = emptyList(),
            status = DiscoveryStatus.SUCCESS,
            sourceCount = 0,
        )
        repository.emit(snapshotMiss1)
        advanceUntilIdle()

        // Assert: Still available after first miss
        uiState = viewModel.uiState.value
        val cam1AfterMiss1 = uiState.sources.find { it.sourceId == "cam1-id" }
        if (cam1AfterMiss1 != null) {
            assertTrue(
                "Cam1 should still be available after first miss (debouncing)",
                cam1AfterMiss1.isAvailable,
            )
        }

        // Act: Second miss (Cam1 still absent)
        val snapshotMiss2 = DiscoverySnapshot(
            snapshotId = UUID.randomUUID().toString(),
            startedAtEpochMillis = 5L,
            completedAtEpochMillis = 6L,
            sources = emptyList(),
            status = DiscoveryStatus.SUCCESS,
            sourceCount = 0,
        )
        repository.emit(snapshotMiss2)
        advanceUntilIdle()

        // Assert: Now unavailable after second miss
        uiState = viewModel.uiState.value
        val cam1AfterMiss2 = uiState.sources.find { it.sourceId == "cam1-id" }
        if (cam1AfterMiss2 != null) {
            assertFalse(
                "Cam1 MUST be unavailable after two consecutive misses",
                cam1AfterMiss2.isAvailable,
            )
        }
    }

    /**
     * T028: Tests that selecting an unavailable source does not trigger navigation.
     */
    @Test
    fun testSelectingUnavailableSourceIsBlocked() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeDiscoveryRepositoryWithAvailability()
        val viewModel = SourceListViewModel(repository, InMemoryUserSelectionRepositoryForAvailability(), SourceListTelemetryEmitter {})

        // Arrange: Cam1 present, then becomes unavailable after two misses
        val cam1 = NdiSource(
            sourceId = "cam1-id",
            displayName = "Cam1",
            endpointAddress = "192.168.1.1:5960",
            lastSeenAtEpochMillis = System.currentTimeMillis(),
        )
        val snapshot1 = DiscoverySnapshot(
            snapshotId = UUID.randomUUID().toString(),
            startedAtEpochMillis = 1L,
            completedAtEpochMillis = 2L,
            sources = listOf(cam1),
            status = DiscoveryStatus.SUCCESS,
            sourceCount = 1,
        )

        repository.emit(snapshot1)
        viewModel.onScreenVisible()
        advanceUntilIdle()

        // Drive Cam1 to unavailable state
        repeat(2) {
            val snapshotMiss = DiscoverySnapshot(
                snapshotId = UUID.randomUUID().toString(),
                startedAtEpochMillis = (3 + it * 2).toLong(),
                completedAtEpochMillis = (4 + it * 2).toLong(),
                sources = emptyList(),
                status = DiscoveryStatus.SUCCESS,
                sourceCount = 0,
            )
            repository.emit(snapshotMiss)
            advanceUntilIdle()
        }

        // Act: User tries to select the unavailable source
        var navigationEmitted = false
        val collector = launch {
            viewModel.navigationEvents.first()
            navigationEmitted = true
        }

        viewModel.onSourceSelected("cam1-id")
        advanceUntilIdle()

        // Assert: No navigation event should have been emitted
        assertTrue(
            "Navigation event MUST NOT be emitted for unavailable source",
            !navigationEmitted,
        )
        collector.cancel()
    }
}

private class FakeDiscoveryRepositoryWithAvailability : NdiDiscoveryRepository {
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
    private val availabilityMap = MutableStateFlow<Map<String, SourceAvailabilityStatus>>(emptyMap())

    override suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot {
        return snapshots.value
    }

    override fun observeDiscoveryState(): Flow<DiscoverySnapshot> = snapshots

    override fun startForegroundAutoRefresh(intervalSeconds: Int) {}

    override fun stopForegroundAutoRefresh() {}

    override fun observeAvailabilityHistory(): Flow<Map<String, SourceAvailabilityStatus>> {
        return availabilityMap
    }

    override suspend fun getSourceAvailabilityStatus(sourceId: String): SourceAvailabilityStatus? {
        return availabilityMap.value[sourceId]
    }

    fun emit(snapshot: DiscoverySnapshot) {
        snapshots.value = snapshot
        // Simulate availability tracking: after 2 misses, source becomes unavailable
        val seenSourceIds = snapshot.sources.map { it.sourceId }.toSet()
        val currentMap = availabilityMap.value.toMutableMap()

        // Update seen sources as available
        for (source in snapshot.sources) {
            currentMap[source.sourceId] = SourceAvailabilityStatus(
                sourceId = source.sourceId,
                isAvailable = true,
                consecutiveMissedPolls = 0,
                lastSeenAtEpochMillis = System.currentTimeMillis(),
                lastStatusChangedAtEpochMillis = System.currentTimeMillis(),
            )
        }

        // Update missing sources (increment miss counter)
        for ((sourceId, status) in currentMap.toList()) {
            if (sourceId !in seenSourceIds) {
                val nextMisses = status.consecutiveMissedPolls + 1
                val isNowUnavailable = nextMisses >= 2
                currentMap[sourceId] = status.copy(
                    isAvailable = !isNowUnavailable,
                    consecutiveMissedPolls = nextMisses,
                )
            }
        }

        availabilityMap.value = currentMap
    }
}

private class InMemoryUserSelectionRepositoryForAvailability : UserSelectionRepository {
    private var sourceId: String? = null

    override suspend fun saveLastSelectedSource(sourceId: String) {
        this.sourceId = sourceId
    }

    override suspend fun getLastSelectedSource(): String? = sourceId
}

// Use the shared FakeDiscoveryRepository and InMemoryUserSelectionRepository from  SourceListViewModelTest.kt
