package com.ndi.feature.ndibrowser.home

import com.ndi.core.model.OutputState
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.navigation.HomeDashboardSnapshot
import com.ndi.feature.ndibrowser.domain.repository.HomeDashboardRepository
import com.ndi.feature.ndibrowser.testutil.MainDispatcherRule
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class HomeViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun onHomeVisible_refreshesDashboard() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repo = FakeHomeDashboardRepository()
        val vm = HomeViewModel(repo)

        vm.onHomeVisible()
        advanceUntilIdle()

        assertNotNull(vm.uiState.value.snapshot)
        assertEquals(1, repo.refreshCount)
    }

    @Test
    fun onOpenStreamActionPressed_emitsOpenStreamEvent() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val repo = FakeHomeDashboardRepository()
            val vm = HomeViewModel(repo)
            val events = mutableListOf<HomeNavigationEvent>()
            val job = launch { vm.navigationEvents.collect { events.add(it) } }

            vm.onOpenStreamActionPressed()
            advanceUntilIdle()

            assertTrue(events.any { it is HomeNavigationEvent.OpenStream })
            job.cancel()
        }

    @Test
    fun onOpenViewActionPressed_emitsOpenViewEvent() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val repo = FakeHomeDashboardRepository()
            val vm = HomeViewModel(repo)
            val events = mutableListOf<HomeNavigationEvent>()
            val job = launch { vm.navigationEvents.collect { events.add(it) } }

            vm.onOpenViewActionPressed()
            advanceUntilIdle()

            assertTrue(events.any { it is HomeNavigationEvent.OpenView })
            job.cancel()
        }

    @Test
    fun snapshotStream_updatesUiState() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repo = FakeHomeDashboardRepository()
        val vm = HomeViewModel(repo)
        advanceUntilIdle()

        val newSnapshot = HomeDashboardSnapshot(
            generatedAtEpochMillis = System.currentTimeMillis(),
            streamStatus = OutputState.ACTIVE,
            viewPlaybackStatus = PlaybackState.PLAYING,
        )
        repo.emit(newSnapshot)
        advanceUntilIdle()

        assertEquals(OutputState.ACTIVE, vm.uiState.value.snapshot?.streamStatus)
    }
}

private class FakeHomeDashboardRepository : HomeDashboardRepository {
    private val _snapshots = MutableStateFlow(
        HomeDashboardSnapshot(
            generatedAtEpochMillis = 0L,
            streamStatus = OutputState.READY,
            viewPlaybackStatus = PlaybackState.STOPPED,
        ),
    )
    var refreshCount = 0

    override fun observeDashboardSnapshot(): Flow<HomeDashboardSnapshot> = _snapshots

    override suspend fun refreshDashboardSnapshot(): HomeDashboardSnapshot {
        refreshCount++
        return _snapshots.value
    }

    fun emit(snapshot: HomeDashboardSnapshot) {
        _snapshots.value = snapshot
    }
}

@OptIn(ExperimentalCoroutinesApi::class)
class HomeViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun onHomeVisible_refreshesDashboard() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repo = FakeHomeDashboardRepository()
        val vm = HomeViewModel(repo, HomeTelemetryEmitter {})

        vm.onHomeVisible()
        advanceUntilIdle()

        assertNotNull(vm.uiState.value.snapshot)
        assertEquals(1, repo.refreshCount)
    }

    @Test
    fun onOpenStreamActionPressed_emitsOpenStreamEvent() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val repo = FakeHomeDashboardRepository()
            val vm = HomeViewModel(repo, HomeTelemetryEmitter {})
            val events = mutableListOf<HomeNavigationEvent>()
            val job = kotlinx.coroutines.launch { vm.navigationEvents.collect { events.add(it) } }

            vm.onOpenStreamActionPressed()
            advanceUntilIdle()

            assertTrue(events.any { it is HomeNavigationEvent.OpenStream })
            job.cancel()
        }

    @Test
    fun onOpenViewActionPressed_emitsOpenViewEvent() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val repo = FakeHomeDashboardRepository()
            val vm = HomeViewModel(repo, HomeTelemetryEmitter {})
            val events = mutableListOf<HomeNavigationEvent>()
            val job = kotlinx.coroutines.launch { vm.navigationEvents.collect { events.add(it) } }

            vm.onOpenViewActionPressed()
            advanceUntilIdle()

            assertTrue(events.any { it is HomeNavigationEvent.OpenView })
            job.cancel()
        }

    @Test
    fun snapshotStream_updatesUiState() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repo = FakeHomeDashboardRepository()
        val vm = HomeViewModel(repo, HomeTelemetryEmitter {})
        advanceUntilIdle()

        val newSnapshot = HomeDashboardSnapshot(
            generatedAtEpochMillis = System.currentTimeMillis(),
            streamStatus = OutputState.ACTIVE,
            viewPlaybackStatus = PlaybackState.PLAYING,
        )
        repo.emit(newSnapshot)
        advanceUntilIdle()

        assertEquals(OutputState.ACTIVE, vm.uiState.value.snapshot?.streamStatus)
    }
}

private class FakeHomeDashboardRepository : HomeDashboardRepository {
    private val _snapshots = MutableStateFlow(
        HomeDashboardSnapshot(
            generatedAtEpochMillis = 0L,
            streamStatus = OutputState.READY,
            viewPlaybackStatus = PlaybackState.STOPPED,
        ),
    )
    var refreshCount = 0

    override fun observeDashboardSnapshot(): Flow<HomeDashboardSnapshot> = _snapshots

    override suspend fun refreshDashboardSnapshot(): HomeDashboardSnapshot {
        refreshCount++
        return _snapshots.value
    }

    fun emit(snapshot: HomeDashboardSnapshot) {
        _snapshots.value = snapshot
    }
}


