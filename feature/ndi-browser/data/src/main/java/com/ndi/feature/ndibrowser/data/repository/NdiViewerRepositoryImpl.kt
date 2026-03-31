package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.ViewerSessionDao
import com.ndi.core.database.ViewerSessionEntity
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.core.model.ViewerSession
import com.ndi.feature.ndibrowser.data.ViewerReconnectCoordinator
import com.ndi.feature.ndibrowser.domain.repository.LastViewedContext
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.PlaybackOptimizationState
import com.ndi.feature.ndibrowser.domain.repository.QualityProfileApplyResult
import com.ndi.feature.ndibrowser.domain.repository.QualityProfile
import com.ndi.feature.ndibrowser.domain.repository.ViewerContinuityRepository
import com.ndi.feature.ndibrowser.domain.repository.PerSourceFrameRepository
import com.ndi.sdkbridge.NdiViewerBridge
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withTimeoutOrNull
import kotlinx.coroutines.withContext
import java.util.ArrayDeque
import java.util.UUID

class NdiViewerRepositoryImpl(
    private val bridge: NdiViewerBridge,
    private val viewerSessionDao: ViewerSessionDao,
    private val reconnectCoordinator: ViewerReconnectCoordinator = ViewerReconnectCoordinator(),
    private val viewerContinuityRepository: ViewerContinuityRepository? = null,
    private val perSourceFrameRepository: PerSourceFrameRepository? = null,
) : NdiViewerRepository {

    private val operationMutex = Mutex()
    private val qualityBySource = mutableMapOf<String, QualityProfile>()
    private val codecHintBySource = mutableMapOf<String, String>()

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
                val firstFrame = withContext(Dispatchers.IO) {
                    bridge.startReceiver(sourceId)
                    waitForFirstFrame(sourceId)
                }
                persistViewerContinuity(sourceId = sourceId, firstFrame = firstFrame)
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
            val activeSourceId = viewerSessionState.value.selectedSourceId
            val latestFrameBeforeStop = if (activeSourceId.isNotBlank()) {
                withContext(Dispatchers.IO) {
                    bridge.getLatestReceiverFrame()
                }
            } else {
                null
            }

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

            if (activeSourceId.isNotBlank()) {
                persistViewerContinuity(sourceId = activeSourceId, firstFrame = latestFrameBeforeStop)
                // Save frame for per-source retention after continuity is persisted
                perSourceFrameRepository?.saveFrameForSource(activeSourceId, latestFrameBeforeStop)
            }
        }
    }

    override suspend fun applyQualityProfile(sourceId: String, profile: QualityProfile) {
        operationMutex.withLock {
            qualityBySource[sourceId] = profile
            codecHintBySource[sourceId] = "adaptive"
            withContext(Dispatchers.IO) {
                bridge.applyReceiverQualityProfile(
                    profileId = profile.profileId,
                    maxWidth = profile.maxWidth,
                    maxHeight = profile.maxHeight,
                    targetFps = profile.targetFps,
                )
            }
        }
    }

    override suspend fun applyQualityProfile(profile: QualityProfile): QualityProfileApplyResult {
        val sourceId = viewerSessionState.value.selectedSourceId
        if (sourceId.isBlank()) {
            return QualityProfileApplyResult.NOT_SUPPORTED
        }

        return operationMutex.withLock {
            qualityBySource[sourceId] = profile
            codecHintBySource[sourceId] = "adaptive"
            withContext(Dispatchers.IO) {
                bridge.applyReceiverQualityProfile(
                    profileId = profile.profileId,
                    maxWidth = profile.maxWidth,
                    maxHeight = profile.maxHeight,
                    targetFps = profile.targetFps,
                )
                val frameRateApplied = bridge.setFrameRatePolicy(profile.targetFps)
                val resolutionApplied = bridge.setResolutionPolicy(profile.maxWidth, profile.maxHeight)
                when {
                    frameRateApplied && resolutionApplied -> QualityProfileApplyResult.APPLIED
                    frameRateApplied || resolutionApplied -> QualityProfileApplyResult.FALLBACK
                    else -> QualityProfileApplyResult.NOT_SUPPORTED
                }
            }
        }
    }

    override fun getOptimizationStats(): Flow<PlaybackOptimizationState> {
        return flow {
            val recentFpsSamples = ArrayDeque<Pair<Long, Double>>()
            var smoothCount = 0
            while (true) {
                val now = System.currentTimeMillis()
                val session = viewerSessionState.value
                val sourceId = session.selectedSourceId
                val activeProfile = qualityBySource[sourceId] ?: QualityProfile.default()

                if (sourceId.isBlank() || session.playbackState != PlaybackState.PLAYING) {
                    recentFpsSamples.clear()
                    smoothCount = 0
                    emit(
                        PlaybackOptimizationState(
                            sourceId = sourceId,
                            selectedProfileId = activeProfile.id,
                            lastFrameTimeEpochMillis = now,
                        ),
                    )
                    delay(500)
                    continue
                }

                val currentFps = withContext(Dispatchers.IO) { bridge.getMeasuredReceiverFps() }
                    .toDouble()
                    .coerceAtLeast(0.0)
                recentFpsSamples.addLast(now to currentFps)
                while (recentFpsSamples.isNotEmpty() && now - recentFpsSamples.first().first > 1_000L) {
                    recentFpsSamples.removeFirst()
                }

                val averageFps = if (recentFpsSamples.isEmpty()) {
                    currentFps
                } else {
                    recentFpsSamples.sumOf { it.second } / recentFpsSamples.size.toDouble()
                }

                val droppedPercent = withContext(Dispatchers.IO) {
                    bridge.getReceiverDroppedFramePercent()
                }.coerceIn(0f, 100f)
                val (actualWidth, actualHeight) = withContext(Dispatchers.IO) { bridge.getActualResolution() }
                if (droppedPercent < activeProfile.frameDropThresholdPercent) {
                    smoothCount += 1
                }

                emit(
                    PlaybackOptimizationState(
                        sourceId = sourceId,
                        droppedFramePercent = droppedPercent.toInt(),
                        selectedProfileId = activeProfile.id,
                        currentFrameRate = currentFps,
                        averageFrameRate = averageFps,
                        lastFrameTimeEpochMillis = now,
                        smoothPlaybackCount = smoothCount,
                        droppedFrameCount = droppedPercent.toInt(),
                        actualWidth = actualWidth,
                        actualHeight = actualHeight,
                        detectedCodecPreference = codecHintBySource[sourceId] ?: "adaptive",
                        autoDegradeCount = 0,
                        updatedAtEpochMillis = now,
                    ),
                )

                // Keep additional per-sample hints in source maps for telemetry and downstream policy.
                qualityBySource[sourceId] = activeProfile
                codecHintBySource[sourceId] = "adaptive-${actualWidth}x${actualHeight}"
                delay(500)
            }
        }
    }

    override suspend fun getActiveQualityProfile(sourceId: String): QualityProfile {
        return operationMutex.withLock {
            qualityBySource[sourceId] ?: QualityProfile.default()
        }
    }

    override fun observeDroppedFramePercent(sourceId: String): Flow<Int> {
        return viewerSessionState.asStateFlow().map {
            if (it.selectedSourceId == sourceId && it.playbackState == PlaybackState.PLAYING) {
                bridge.getReceiverDroppedFramePercent().toInt().coerceIn(0, 100)
            } else {
                0
            }
        }
    }

    override suspend fun degradeQualityIfNeeded(sourceId: String, droppedFramePercent: Int): QualityProfile {
        val current = getActiveQualityProfile(sourceId)
        if (droppedFramePercent < current.frameDropThresholdPercent) {
            return current
        }
        val next = current.nextLowerProfile() ?: current
        applyQualityProfile(sourceId, next)
        return next
    }

    override suspend fun handleStreamDisconnection(sourceId: String, maxRetries: Int): Boolean {
        val activeProfile = getActiveQualityProfile(sourceId)
        val session = retryReconnectWithinWindow(sourceId, windowSeconds = 15)
        val recovered = session.playbackState == PlaybackState.PLAYING
        if (recovered) {
            applyQualityProfile(sourceId, activeProfile)
        }
        return recovered
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

    private suspend fun waitForFirstFrame(sourceId: String, timeoutMillis: Long = 5_000L): ViewerVideoFrame? {
        if (sourceId.startsWith("relay-screen:")) {
            return null
        }
        val deadline = System.currentTimeMillis() + timeoutMillis
        while (System.currentTimeMillis() < deadline) {
            val frame = bridge.getLatestReceiverFrame()
            if (frame != null && frame.width > 0 && frame.height > 0 && frame.argbPixels.isNotEmpty()) {
                return frame
            }
            delay(100)
        }
        throw IllegalStateException("No video frames received from selected NDI source")
    }

    private suspend fun persistViewerContinuity(sourceId: String, firstFrame: ViewerVideoFrame?) {
        val continuityRepository = viewerContinuityRepository ?: return
        if (sourceId.isBlank()) return

        val capturedAt = System.currentTimeMillis()
        if (firstFrame != null) {
            continuityRepository.markSuccessfulFrame(sourceId, capturedAt)
        }
        val previewPath = if (firstFrame != null) {
            continuityRepository.captureAndSavePreviewFrame(sourceId, firstFrame, capturedAt)
        } else {
            continuityRepository.getLastViewedContext()?.lastFrameImagePath
        }
        continuityRepository.saveLastViewedContext(
            LastViewedContext(
                sourceId = sourceId,
                lastFrameImagePath = previewPath,
                lastFrameCapturedAtEpochMillis = capturedAt,
            ),
        )
    }
}
