package com.ndi.feature.ndibrowser.viewer

import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerSession
import com.ndi.feature.ndibrowser.testutil.MainDispatcherRule
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import java.util.UUID

@OptIn(ExperimentalCoroutinesApi::class)
class ViewerViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun onViewerOpened_movesToPlayingState() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeViewerRepository()
        val viewModel = ViewerViewModel(repository, InMemorySelectionRepository(), ViewerTelemetryEmitter {})

        viewModel.onViewerOpened("camera-3")
        advanceUntilIdle()

        assertEquals(PlaybackState.PLAYING, viewModel.uiState.value.playbackState)
        assertEquals("camera-3", viewModel.uiState.value.sourceId)
    }

    @Test
    fun onBackToListPressed_stopsPlayback() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeViewerRepository()
        val viewModel = ViewerViewModel(repository, InMemorySelectionRepository(), ViewerTelemetryEmitter {})

        viewModel.onViewerOpened("camera-3")
        advanceUntilIdle()
        viewModel.onBackToListPressed()
        advanceUntilIdle()

        assertEquals(PlaybackState.STOPPED, viewModel.uiState.value.playbackState)
    }
}

private class FakeViewerRepository : NdiViewerRepository {
    private val sessions = MutableStateFlow(
        ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = "",
            playbackState = PlaybackState.IDLE,
            startedAtEpochMillis = 0L,
        ),
    )

    override suspend fun connectToSource(sourceId: String): ViewerSession {
        val session = ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = sourceId,
            playbackState = PlaybackState.PLAYING,
            startedAtEpochMillis = System.currentTimeMillis(),
        )
        sessions.value = session
        return session
    }

    override fun observeViewerSession(): Flow<ViewerSession> = sessions

    override suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int): ViewerSession {
        return connectToSource(sourceId)
    }

    override suspend fun stopViewing() {
        sessions.value = sessions.value.copy(playbackState = PlaybackState.STOPPED)
    }
}

private class InMemorySelectionRepository : UserSelectionRepository {
    private var sourceId: String? = null

    override suspend fun saveLastSelectedSource(sourceId: String) {
        this.sourceId = sourceId
    }

    override suspend fun getLastSelectedSource(): String? = sourceId
}
