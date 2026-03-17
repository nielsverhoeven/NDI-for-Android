package com.ndi.feature.ndibrowser.viewer

import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerSession
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.feature.ndibrowser.testutil.MainDispatcherRule
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import java.util.UUID

/**
 * Validates View stop-only (no pause fallback) and no-autoplay restore behavior
 * on top-level navigation and relaunch.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class ViewerViewModelTopLevelNavTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun leavingView_stopsPlayback() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val viewerRepo = NavViewerRepository()
        val vm = ViewerViewModel(viewerRepo, NavUserSelectionRepository(), ViewerTelemetryEmitter {})

        vm.onViewerOpened("camera-1")
        advanceUntilIdle()

        // Simulate leaving View via top-level navigation (back to list / nav away)
        vm.onBackToListPressed()
        advanceUntilIdle()

        assertEquals(PlaybackState.STOPPED, vm.uiState.value.playbackState)
        assertEquals(1, viewerRepo.stopCalls)
    }

    @Test
    fun noAutoplay_onRelaunch() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val viewerRepo = NavViewerRepository()
        ViewerViewModel(viewerRepo, NavUserSelectionRepository(lastSourceId = "camera-1"), ViewerTelemetryEmitter {})
        advanceUntilIdle()

        // Source highlighted on return — NO auto-connect call
        assertEquals(0, viewerRepo.connectCalls)
    }

    @Test
    fun onBackToListPressed_doesNotPause_onlyStops() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val viewerRepo = NavViewerRepository()
        val vm = ViewerViewModel(viewerRepo, NavUserSelectionRepository(), ViewerTelemetryEmitter {})

        vm.onViewerOpened("camera-2")
        advanceUntilIdle()
        vm.onBackToListPressed()
        advanceUntilIdle()

        // Only stopViewing should be called, not any pause API
        assertEquals(1, viewerRepo.stopCalls)
    }
}

private class NavViewerRepository : NdiViewerRepository {
    private val session = MutableStateFlow(
        ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = "",
            playbackState = PlaybackState.STOPPED,
            startedAtEpochMillis = 0L,
        ),
    )
    var connectCalls = 0
    var stopCalls = 0

    override suspend fun connectToSource(sourceId: String): ViewerSession {
        connectCalls++
        val playing = session.value.copy(selectedSourceId = sourceId, playbackState = PlaybackState.PLAYING)
        session.value = playing
        return playing
    }

    override fun observeViewerSession(): Flow<ViewerSession> = session

    override suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int) = session.value

    override suspend fun stopViewing() {
        stopCalls++
        session.value = session.value.copy(playbackState = PlaybackState.STOPPED)
    }
}

private class NavUserSelectionRepository(private val lastSourceId: String? = null) : UserSelectionRepository {
    override suspend fun saveLastSelectedSource(sourceId: String) = Unit
    override suspend fun getLastSelectedSource(): String? = lastSourceId
}




