package com.ndi.feature.ndibrowser.viewer

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.PlaybackOptimizationState as OptimizationSample
import com.ndi.feature.ndibrowser.domain.repository.QualityProfile
import com.ndi.feature.ndibrowser.domain.repository.QualityProfileRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class ViewerUiState(
    val sourceId: String = "",
    val playbackState: PlaybackState = PlaybackState.IDLE,
    val activeQualityProfileId: String = QualityProfile.default().profileId,
    val droppedFramePercent: Int = 0,
    val layoutMode: ViewerLayoutMode = ViewerLayoutMode.PHONE,
    val interruptionMessage: String? = null,
    val recoveryActionsVisible: Boolean = false,
    val disconnectionDialogVisible: Boolean = false,
    val optimizationState: PlaybackOptimizationState = PlaybackOptimizationState.Smooth,
    val recoveryElapsedSeconds: Int = 0,
    val manualReconnectVisible: Boolean = false,
    val overlayDisplayState: com.ndi.feature.ndibrowser.settings.OverlayDisplayState? = null,
)

sealed class PlaybackOptimizationState {
    data object Smooth : PlaybackOptimizationState()

    data class Degraded(
        val profileId: String,
    ) : PlaybackOptimizationState()

    data class AttemptingRecovery(
        val elapsedSeconds: Int,
    ) : PlaybackOptimizationState()

    data object Disconnected : PlaybackOptimizationState()
}

sealed class QualityAdjustmentAction {
    data object Degrade : QualityAdjustmentAction()
    data object Upgrade : QualityAdjustmentAction()
    data object Hold : QualityAdjustmentAction()
}

class ViewerViewModel(
    private val viewerRepository: NdiViewerRepository,
    private val userSelectionRepository: UserSelectionRepository,
    private val telemetryEmitter: ViewerTelemetryEmitter = ViewerDependencies.telemetryEmitter,
    private val qualityProfileRepository: QualityProfileRepository? = ViewerDependencies.qualityProfileRepositoryOrNull(),
) : ViewModel() {

    private val _uiState = MutableStateFlow(ViewerUiState())
    val uiState: StateFlow<ViewerUiState> = _uiState.asStateFlow()

    private val _settingsToggleEvents = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val settingsToggleEvents: SharedFlow<Unit> = _settingsToggleEvents.asSharedFlow()

    private var settingsToggleInFlight: Boolean = false
    private var connectInFlight: Boolean = false
    private var optimizationCollectorJob: Job? = null
    private var playbackOptimizationManagerJob: Job? = null
    private var recoveryJob: Job? = null
    private var latestOptimizationSample: OptimizationSample? = null
    private var lowFpsSinceEpochMillis: Long? = null
    private var recoveredFpsSinceEpochMillis: Long? = null

    init {
        viewModelScope.launch {
            viewerRepository.observeViewerSession().collect { session ->
                val recoveryState = session.toViewerRecoveryUiState()
                _uiState.update {
                    it.copy(
                        sourceId = session.selectedSourceId,
                        playbackState = session.playbackState,
                        interruptionMessage = recoveryState.interruptionMessage,
                        recoveryActionsVisible = recoveryState.recoveryActionsVisible,
                    )
                }
                if (session.playbackState == PlaybackState.INTERRUPTED || session.playbackState == PlaybackState.STOPPED) {
                    beginRecovery(session.selectedSourceId)
                } else if (session.playbackState == PlaybackState.PLAYING) {
                    completeRecovery(success = true)
                }
            }
        }
    }

    fun onViewerOpened(sourceId: String) {
        if (sourceId.isBlank() || connectInFlight) return
        connectInFlight = true
        viewModelScope.launch {
            runCatching {
                userSelectionRepository.saveLastSelectedSource(sourceId)
                viewerRepository.connectToSource(sourceId)

                val profile = qualityProfileRepository
                    ?.getQualityPreference(sourceId)
                    ?.profileId
                    ?.let(QualityProfile::fromId)
                    ?: QualityProfile.default()
                viewerRepository.applyQualityProfile(sourceId, profile)
                _uiState.update { it.copy(activeQualityProfileId = profile.profileId) }
                telemetryEmitter.emit(ViewerTelemetry.playbackStarted(sourceId))
                telemetryEmitter.emit(ViewerRecoveryTelemetry.profileSelected(sourceId, profile.profileId))
                onStart()
            }
            connectInFlight = false
        }
    }

    fun onStart() {
        val sourceId = _uiState.value.sourceId
        if (sourceId.isBlank()) return
        startPlaybackOptimization(sourceId)
    }

    fun onStop() {
        optimizationCollectorJob?.cancel()
        optimizationCollectorJob = null
        playbackOptimizationManagerJob?.cancel()
        playbackOptimizationManagerJob = null
        lowFpsSinceEpochMillis = null
        recoveredFpsSinceEpochMillis = null
    }

    fun onBackToListPressed() {
        viewModelScope.launch {
            stopViewingForBackNavigation()
        }
    }

    suspend fun stopViewingForBackNavigation() {
        onStop()
        recoveryJob?.cancel()
        recoveryJob = null
        val sourceId = uiState.value.sourceId
        viewerRepository.stopViewing()
        if (sourceId.isNotBlank()) {
            telemetryEmitter.emit(ViewerRecoveryTelemetry.returnToListRequested(sourceId))
            telemetryEmitter.emit(ViewerTelemetry.playbackStopped(sourceId))
        }
    }

    fun onRetryPressed() {
        viewModelScope.launch {
            val sourceId = uiState.value.sourceId
            if (sourceId.isBlank()) return@launch
            telemetryEmitter.emit(ViewerRecoveryTelemetry.retryRequested(sourceId))
            recoveryJob?.cancel()
            recoveryJob = null
            val recovered = viewerRepository.handleStreamDisconnection(sourceId, maxRetries = 5)
            if (recovered) {
                completeRecovery(success = true)
            } else {
                completeRecovery(success = false)
            }
        }
    }

    fun onDisconnectionDialogDismissed() {
        _uiState.update { it.copy(disconnectionDialogVisible = false) }
    }

    fun onLayoutMeasured(widthDp: Int) {
        _uiState.update { current -> current.copy(layoutMode = ViewerAdaptiveLayout.resolve(widthDp)) }
    }

    fun getLatestVideoFrame(): ViewerVideoFrame? = viewerRepository.getLatestVideoFrame()

    fun onSettingsTogglePressed() {
        if (settingsToggleInFlight) return
        settingsToggleInFlight = true
        _settingsToggleEvents.tryEmit(Unit)
    }

    fun onSettingsToggleSettled() {
        settingsToggleInFlight = false
    }

    fun onQualityProfileSelected(profileId: String) {
        val sourceId = uiState.value.sourceId
        if (sourceId.isBlank()) return
        val profile = QualityProfile.fromId(profileId)
        viewModelScope.launch {
            viewerRepository.applyQualityProfile(sourceId, profile)
            qualityProfileRepository?.setQualityPreference(
                com.ndi.feature.ndibrowser.domain.repository.QualityPreference(
                    profileId = profile.id,
                    sourceId = sourceId,
                ),
            )
            _uiState.update { it.copy(activeQualityProfileId = profile.profileId) }
            telemetryEmitter.emit(ViewerRecoveryTelemetry.profileSelected(sourceId, profile.profileId))
        }
    }

    private fun startPlaybackOptimization(sourceId: String) {
        optimizationCollectorJob?.cancel()
        playbackOptimizationManagerJob?.cancel()

        optimizationCollectorJob = viewModelScope.launch {
            viewerRepository.getOptimizationStats(sourceId).collect { sample ->
                latestOptimizationSample = sample
                _uiState.update { current -> current.copy(droppedFramePercent = sample.droppedFramePercent.coerceIn(0, 100)) }
            }
        }

        playbackOptimizationManagerJob = viewModelScope.launch {
            while (true) {
                val sample = latestOptimizationSample
                if (sample != null) {
                    val now = System.currentTimeMillis()
                    val action = decideAdjustment(sample.averageFrameRate)
                    when (action) {
                        QualityAdjustmentAction.Degrade -> {
                            if (lowFpsSinceEpochMillis == null) {
                                lowFpsSinceEpochMillis = now
                            } else if (now - (lowFpsSinceEpochMillis ?: now) >= 5_000L) {
                                lowFpsSinceEpochMillis = null
                                val current = QualityProfile.fromId(uiState.value.activeQualityProfileId)
                                val next = current.nextLowerProfile()
                                if (next != null && next.profileId != current.profileId) {
                                    viewerRepository.applyQualityProfile(sourceId, next)
                                    _uiState.update {
                                        it.copy(
                                            activeQualityProfileId = next.profileId,
                                            optimizationState = PlaybackOptimizationState.Degraded(next.profileId),
                                        )
                                    }
                                    telemetryEmitter.emit(
                                        ViewerRecoveryTelemetry.qualityDowngraded(
                                            sourceId = sourceId,
                                            fromProfileId = current.profileId,
                                            toProfileId = next.profileId,
                                        ),
                                    )
                                }
                            }
                            recoveredFpsSinceEpochMillis = null
                        }

                        QualityAdjustmentAction.Upgrade -> {
                            if (recoveredFpsSinceEpochMillis == null) {
                                recoveredFpsSinceEpochMillis = now
                            } else if (now - (recoveredFpsSinceEpochMillis ?: now) >= 3_000L) {
                                recoveredFpsSinceEpochMillis = null
                                val current = QualityProfile.fromId(uiState.value.activeQualityProfileId)
                                val upgraded = current.nextHigherProfile()
                                if (upgraded != null && upgraded.profileId != current.profileId) {
                                    viewerRepository.applyQualityProfile(sourceId, upgraded)
                                    _uiState.update {
                                        it.copy(
                                            activeQualityProfileId = upgraded.profileId,
                                            optimizationState = PlaybackOptimizationState.Smooth,
                                        )
                                    }
                                    telemetryEmitter.emit(
                                        ViewerRecoveryTelemetry.qualityRecovered(
                                            sourceId = sourceId,
                                            profileId = upgraded.profileId,
                                        ),
                                    )
                                }
                            }
                            lowFpsSinceEpochMillis = null
                        }

                        QualityAdjustmentAction.Hold -> {
                            lowFpsSinceEpochMillis = null
                            recoveredFpsSinceEpochMillis = null
                            if (sample.averageFrameRate >= 24.0) {
                                _uiState.update { it.copy(optimizationState = PlaybackOptimizationState.Smooth) }
                            }
                        }
                    }
                }
                delay(250)
            }
        }
    }

    private fun decideAdjustment(averageFps: Double): QualityAdjustmentAction {
        return when {
            averageFps < 20.0 -> QualityAdjustmentAction.Degrade
            averageFps > 25.0 -> QualityAdjustmentAction.Upgrade
            else -> QualityAdjustmentAction.Hold
        }
    }

    private fun beginRecovery(sourceId: String) {
        if (sourceId.isBlank() || recoveryJob?.isActive == true) return
        recoveryJob = viewModelScope.launch {
            telemetryEmitter.emit(ViewerRecoveryTelemetry.recoveryDialogShown(sourceId))
            val retryDelays = listOf(500L, 1_000L, 2_000L, 4_000L)
            var elapsedMillis = 0L
            var attempt = 0

            while (elapsedMillis < 15_000L) {
                _uiState.update {
                    it.copy(
                        optimizationState = PlaybackOptimizationState.AttemptingRecovery((elapsedMillis / 1_000L).toInt()),
                        disconnectionDialogVisible = true,
                        recoveryElapsedSeconds = (elapsedMillis / 1_000L).toInt(),
                        manualReconnectVisible = false,
                    )
                }

                val recovered = viewerRepository.retryReconnectWithinWindow(sourceId, windowSeconds = 1)
                    .playbackState == PlaybackState.PLAYING
                if (recovered) {
                    completeRecovery(success = true)
                    return@launch
                }

                val delayMillis = retryDelays[minOf(attempt, retryDelays.lastIndex)]
                telemetryEmitter.emit(ViewerRecoveryTelemetry.recoveryAttempted(sourceId, attempt + 1))
                delay(delayMillis)
                elapsedMillis += delayMillis
                attempt += 1
            }

            _uiState.update {
                it.copy(
                    optimizationState = PlaybackOptimizationState.Disconnected,
                    disconnectionDialogVisible = true,
                    recoveryElapsedSeconds = 15,
                    manualReconnectVisible = true,
                )
            }
            completeRecovery(success = false)
        }
    }

    private fun completeRecovery(success: Boolean) {
        val sourceId = _uiState.value.sourceId
        if (sourceId.isNotBlank()) {
            telemetryEmitter.emit(ViewerRecoveryTelemetry.recoveryResult(sourceId, success))
        }
        if (success) {
            _uiState.update {
                it.copy(
                    optimizationState = PlaybackOptimizationState.Smooth,
                    disconnectionDialogVisible = false,
                    interruptionMessage = null,
                    recoveryElapsedSeconds = 0,
                    manualReconnectVisible = false,
                )
            }
        }
    }

    private fun QualityProfile.nextHigherProfile(): QualityProfile? {
        return when (this) {
            QualityProfile.Smooth -> QualityProfile.Balanced
            QualityProfile.Balanced -> QualityProfile.HighQuality
            QualityProfile.HighQuality -> null
        }
    }

    class Factory(
        private val viewerRepository: NdiViewerRepository,
        private val userSelectionRepository: UserSelectionRepository,
        private val telemetryEmitter: ViewerTelemetryEmitter = ViewerDependencies.telemetryEmitter,
        private val qualityProfileRepository: QualityProfileRepository? = ViewerDependencies.qualityProfileRepositoryOrNull(),
    ) : ViewModelProvider.Factory {
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(ViewerViewModel::class.java))
            return ViewerViewModel(
                viewerRepository = viewerRepository,
                userSelectionRepository = userSelectionRepository,
                telemetryEmitter = telemetryEmitter,
                qualityProfileRepository = qualityProfileRepository,
            ) as T
        }
    }
}
