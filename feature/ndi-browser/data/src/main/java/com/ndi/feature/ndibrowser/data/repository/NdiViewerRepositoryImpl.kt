package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.ViewerSessionDao
import com.ndi.core.database.ViewerSessionEntity
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.core.model.ViewerSession
import com.ndi.feature.ndibrowser.data.ViewerReconnectCoordinator
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.sdkbridge.NdiViewerBridge
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withTimeoutOrNull
import kotlinx.coroutines.withContext
import java.util.UUID

class NdiViewerRepositoryImpl(
    private val bridge: NdiViewerBridge,
    private val viewerSessionDao: ViewerSessionDao,
    private val reconnectCoordinator: ViewerReconnectCoordinator = ViewerReconnectCoordinator(),
) : NdiViewerRepository {

    private val operationMutex = Mutex()

    private val viewerSessionState = MutableStateFlow(
        ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = "",
            playbackState = PlaybackState.IDLE,
            startedAtEpochMillis = 0L,
        ),
    )

    override suspend fun connectToSource(sourceId: String): ViewerSession {
        return operationMutex.withLock {
            // Ensure any stale receiver state is cleared before opening a new stream,
            // especially when users reopen the same source immediately after backing out.
            withContext(Dispatchers.IO) {
                withTimeoutOrNull(1_500L) {
                    bridge.stopReceiver()
                }
            }

            val connectingSession = ViewerSession(
                sessionId = UUID.randomUUID().toString(),
                selectedSourceId = sourceId,
                playbackState = PlaybackState.CONNECTING,
                startedAtEpochMillis = System.currentTimeMillis(),
            )
            viewerSessionState.value = connectingSession

            runCatching {
                withContext(Dispatchers.IO) {
                    bridge.startReceiver(sourceId)
                    waitForFirstFrame(sourceId)
                }
                val playingSession = connectingSession.copy(playbackState = PlaybackState.PLAYING)
                viewerSessionState.value = playingSession
                withContext(Dispatchers.IO) {
                    viewerSessionDao.upsert(playingSession.toEntity())
                }
                playingSession
            }.getOrElse { error ->
                if (error is UnsupportedOperationException) {
                    val interruptedSession = ViewerSession(
                        sessionId = UUID.randomUUID().toString(),
                        selectedSourceId = sourceId,
                        playbackState = PlaybackState.INTERRUPTED,
                        interruptionReason = error.message,
                        retryWindowSeconds = 15,
                        startedAtEpochMillis = System.currentTimeMillis(),
                    )
                    viewerSessionState.value = interruptedSession
                    withContext(Dispatchers.IO) {
                        viewerSessionDao.upsert(interruptedSession.toEntity())
                    }
                    return@withLock interruptedSession
                }
                retryInternal(sourceId, 15, error.message ?: "Playback interrupted")
            }
        }
    }

    override fun observeViewerSession(): Flow<ViewerSession> = viewerSessionState.asStateFlow()

    override fun getLatestVideoFrame(): ViewerVideoFrame? = bridge.getLatestReceiverFrame()

    override suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int): ViewerSession {
        return operationMutex.withLock {
            retryInternal(sourceId, windowSeconds, "Playback interrupted")
        }
    }

    override suspend fun stopViewing() {
        operationMutex.withLock {
            val stoppedSession = withContext(Dispatchers.IO) {
                // Native stop can occasionally stall; bound it so UI flows do not ANR.
                withTimeoutOrNull(1_500L) {
                    bridge.stopReceiver()
                }
                viewerSessionState.value.copy(
                    playbackState = PlaybackState.STOPPED,
                    endedAtEpochMillis = System.currentTimeMillis(),
                )
            }
            viewerSessionState.value = stoppedSession
            withContext(Dispatchers.IO) {
                viewerSessionDao.upsert(stoppedSession.toEntity())
            }
        }
    }

    private fun ViewerSession.toEntity(): ViewerSessionEntity {
        return ViewerSessionEntity(
            sessionId = sessionId,
            selectedSourceId = selectedSourceId,
            playbackState = playbackState.name,
            interruptionReason = interruptionReason,
            retryWindowSeconds = retryWindowSeconds,
            retryAttempts = retryAttempts,
            startedAtEpochMillis = startedAtEpochMillis,
            endedAtEpochMillis = endedAtEpochMillis,
        )
    }

    private suspend fun retryInternal(sourceId: String, windowSeconds: Int, interruptionReason: String): ViewerSession {
        val interruptedSession = ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = sourceId,
            playbackState = PlaybackState.INTERRUPTED,
            interruptionReason = interruptionReason,
            retryWindowSeconds = windowSeconds,
            startedAtEpochMillis = System.currentTimeMillis(),
        )
        viewerSessionState.value = interruptedSession
        withContext(Dispatchers.IO) {
            viewerSessionDao.upsert(interruptedSession.toEntity())
        }

        val retryResult = reconnectCoordinator.retryWithinWindow(windowSeconds) {
            runCatching {
                withContext(Dispatchers.IO) {
                    bridge.startReceiver(sourceId)
                    waitForFirstFrame(sourceId)
                }
                true
            }.getOrDefault(false)
        }

        return if (retryResult.recovered) {
            val recoveredSession = interruptedSession.copy(
                playbackState = PlaybackState.PLAYING,
                retryAttempts = retryResult.attempts,
            )
            viewerSessionState.value = recoveredSession
            withContext(Dispatchers.IO) {
                viewerSessionDao.upsert(recoveredSession.toEntity())
            }
            recoveredSession
        } else {
            val stoppedSession = interruptedSession.copy(
                playbackState = PlaybackState.STOPPED,
                retryAttempts = retryResult.attempts,
                endedAtEpochMillis = System.currentTimeMillis(),
            )
            viewerSessionState.value = stoppedSession
            withContext(Dispatchers.IO) {
                viewerSessionDao.upsert(stoppedSession.toEntity())
            }
            stoppedSession
        }
    }

    private suspend fun waitForFirstFrame(sourceId: String, timeoutMillis: Long = 5_000L) {
        if (sourceId.startsWith("relay-screen:")) {
            return
        }
        val deadline = System.currentTimeMillis() + timeoutMillis
        while (System.currentTimeMillis() < deadline) {
            val frame = bridge.getLatestReceiverFrame()
            if (frame != null && frame.width > 0 && frame.height > 0 && frame.argbPixels.isNotEmpty()) {
                return
            }
            delay(100)
        }
        throw IllegalStateException("No video frames received from selected NDI source")
    }
}
