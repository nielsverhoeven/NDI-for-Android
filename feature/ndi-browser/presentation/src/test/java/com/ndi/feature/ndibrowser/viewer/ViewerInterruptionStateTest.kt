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
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import java.util.UUID

@OptIn(ExperimentalCoroutinesApi::class)
class ViewerInterruptionStateTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun interruptedThenStopped_showsRecoveryActions() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = RecoveringStateViewerRepository()
        val viewModel = ViewerViewModel(repository, NoOpSelectionRepository(), ViewerTelemetryEmitter {})

        viewModel.onViewerOpened("camera-4")
        advanceUntilIdle()

        assertEquals(PlaybackState.STOPPED, viewModel.uiState.value.playbackState)
        assertTrue(viewModel.uiState.value.recoveryActionsVisible)
        assertEquals("Playback interrupted", viewModel.uiState.value.interruptionMessage)
    }
}

private class RecoveringStateViewerRepository : NdiViewerRepository {
    private val sessions = MutableStateFlow(
        ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = "",
            playbackState = PlaybackState.IDLE,
            startedAtEpochMillis = 0L,
        ),
    )

    override suspend fun connectToSource(sourceId: String): ViewerSession {
        sessions.value = ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = sourceId,
            playbackState = PlaybackState.INTERRUPTED,
            interruptionReason = "Playback interrupted",
            startedAtEpochMillis = 1L,
        )
        sessions.value = sessions.value.copy(playbackState = PlaybackState.STOPPED, endedAtEpochMillis = 2L)
        return sessions.value
    }

    override fun observeViewerSession(): Flow<ViewerSession> = sessions

    override suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int): ViewerSession = sessions.value

    override suspend fun stopViewing() = Unit
}

private class NoOpSelectionRepository : UserSelectionRepository {
    override suspend fun saveLastSelectedSource(sourceId: String) = Unit

    override suspend fun getLastSelectedSource(): String? = null
}
