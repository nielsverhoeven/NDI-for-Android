package com.ndi.feature.ndibrowser.viewer

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class ViewerUiState(
    val sourceId: String = "",
    val playbackState: PlaybackState = PlaybackState.IDLE,
    val layoutMode: ViewerLayoutMode = ViewerLayoutMode.PHONE,
    val interruptionMessage: String? = null,
    val recoveryActionsVisible: Boolean = false,
    val overlayDisplayState: com.ndi.feature.ndibrowser.settings.OverlayDisplayState? = null,
)

class ViewerViewModel(
    private val viewerRepository: NdiViewerRepository,
    private val userSelectionRepository: UserSelectionRepository,
    private val telemetryEmitter: ViewerTelemetryEmitter = ViewerDependencies.telemetryEmitter,
) : ViewModel() {

    private val _uiState = MutableStateFlow(ViewerUiState())
    val uiState: StateFlow<ViewerUiState> = _uiState.asStateFlow()

    private val _settingsToggleEvents = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val settingsToggleEvents: SharedFlow<Unit> = _settingsToggleEvents.asSharedFlow()
    private var settingsToggleInFlight: Boolean = false

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
            }
        }
    }

    fun onViewerOpened(sourceId: String) {
        if (sourceId.isBlank()) return
        viewModelScope.launch {
            userSelectionRepository.saveLastSelectedSource(sourceId)
            viewerRepository.connectToSource(sourceId)
            telemetryEmitter.emit(ViewerTelemetry.playbackStarted(sourceId))
        }
    }

    fun onBackToListPressed() {
        viewModelScope.launch {
            val sourceId = uiState.value.sourceId
            viewerRepository.stopViewing()
            if (sourceId.isNotBlank()) {
                telemetryEmitter.emit(ViewerRecoveryTelemetry.returnToListRequested(sourceId))
                telemetryEmitter.emit(ViewerTelemetry.playbackStopped(sourceId))
            }
        }
    }

    fun onRetryPressed() {
        viewModelScope.launch {
            val sourceId = uiState.value.sourceId
            if (sourceId.isBlank()) return@launch
            telemetryEmitter.emit(ViewerRecoveryTelemetry.retryRequested(sourceId))
            viewerRepository.retryReconnectWithinWindow(sourceId, windowSeconds = 15)
        }
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

    class Factory(
        private val viewerRepository: NdiViewerRepository,
        private val userSelectionRepository: UserSelectionRepository,
        private val telemetryEmitter: ViewerTelemetryEmitter = ViewerDependencies.telemetryEmitter,
    ) : ViewModelProvider.Factory {
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(ViewerViewModel::class.java))
            return ViewerViewModel(viewerRepository, userSelectionRepository, telemetryEmitter) as T
        }
    }
}
