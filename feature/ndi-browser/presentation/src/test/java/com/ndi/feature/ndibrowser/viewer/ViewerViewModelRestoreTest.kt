package com.ndi.feature.ndibrowser.viewer

import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.core.model.ViewerSession
import com.ndi.feature.ndibrowser.domain.repository.ConnectionHistoryState
import com.ndi.feature.ndibrowser.domain.repository.LastViewedContext
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.feature.ndibrowser.domain.repository.ViewerContinuityRepository
import com.ndi.feature.ndibrowser.testutil.MainDispatcherRule
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import java.util.UUID

@OptIn(ExperimentalCoroutinesApi::class)
class ViewerViewModelRestoreTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun onViewerOpened_withoutRouteSource_restoresUnavailableContextWithoutAutoplay() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val viewerRepository = RestoreFakeViewerRepository()
        val continuityRepository = RestoreFakeContinuityRepository(
            context = LastViewedContext(
                sourceId = "camera-offline",
                lastFrameImagePath = "C:/tmp/preview-camera-offline.png",
                lastFrameCapturedAtEpochMillis = 1234L,
            ),
        )
        val previousProvider = ViewerDependencies.viewerContinuityRepositoryProvider
        ViewerDependencies.viewerContinuityRepositoryProvider = { continuityRepository }

        try {
            val viewModel = ViewerViewModel(
                viewerRepository = viewerRepository,
                userSelectionRepository = RestoreSelectionRepository(),
                telemetryEmitter = ViewerTelemetryEmitter {},
            )

            viewModel.onViewerOpened("")
            advanceUntilIdle()

            assertEquals(0, viewerRepository.connectCalls)
            assertEquals("camera-offline", viewModel.uiState.value.sourceId)
            assertEquals(PlaybackState.IDLE, viewModel.uiState.value.playbackState)
            assertTrue(viewModel.uiState.value.isUnavailableRestore)
            assertEquals("C:/tmp/preview-camera-offline.png", viewModel.uiState.value.restoredPreviewPath)
        } finally {
            ViewerDependencies.viewerContinuityRepositoryProvider = previousProvider
        }
    }
}

private class RestoreFakeViewerRepository : NdiViewerRepository {
    private val sessions = MutableStateFlow(
        ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = "",
            playbackState = PlaybackState.IDLE,
            startedAtEpochMillis = 0L,
        ),
    )

    var connectCalls: Int = 0

    override suspend fun connectToSource(sourceId: String): ViewerSession {
        connectCalls += 1
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

    override fun getLatestVideoFrame(): ViewerVideoFrame? = null

    override suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int): ViewerSession {
        return sessions.value
    }

    override suspend fun stopViewing() {
        sessions.value = sessions.value.copy(playbackState = PlaybackState.STOPPED)
    }
}

private class RestoreSelectionRepository : UserSelectionRepository {
    override suspend fun saveLastSelectedSource(sourceId: String) = Unit

    override suspend fun getLastSelectedSource(): String? = null
}

private class RestoreFakeContinuityRepository(
    private var context: LastViewedContext?,
) : ViewerContinuityRepository {

    override fun observeLastViewedContext(): Flow<LastViewedContext?> = flowOf(context)

    override suspend fun getLastViewedContext(): LastViewedContext? = context

    override suspend fun saveLastViewedContext(context: LastViewedContext) {
        this.context = context
    }

    override suspend fun clearLastViewedContext() {
        context = null
    }

    override fun observeConnectionHistory(): Flow<List<ConnectionHistoryState>> = flowOf(emptyList())

    override fun observePreviouslyConnectedSourceIds(): Flow<Set<String>> = flowOf(emptySet())

    override suspend fun markSuccessfulFrame(sourceId: String, frameCapturedAtEpochMillis: Long) = Unit

    override suspend fun getConnectionHistory(sourceId: String): ConnectionHistoryState? = null

    override suspend fun hasPreviouslyConnected(sourceId: String): Boolean = false

    override suspend fun clearConnectionHistory() = Unit
}
