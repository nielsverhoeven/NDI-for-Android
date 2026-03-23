package com.ndi.feature.ndibrowser.output

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.OutputState
import com.ndi.core.model.navigation.BackgroundContinuationReason
import com.ndi.core.model.navigation.StreamContinuityState
import com.ndi.core.model.navigation.TopLevelDestination
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.OutputConfigurationRepository
import com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentRepository
import com.ndi.feature.ndibrowser.domain.repository.StreamContinuityRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class OutputControlUiState(
    val sourceId: String = "",
    val streamName: String = "",
    val isLocalScreenSource: Boolean = false,
    val consentRequired: Boolean = false,
    val outputState: OutputState = OutputState.READY,
    val canStart: Boolean = true,
    val canStop: Boolean = false,
    val canRetry: Boolean = false,
    val showRecoveryActions: Boolean = false,
    val recoveryInProgress: Boolean = false,
    val errorMessage: String? = null,
    val topLevelDestination: TopLevelDestination = TopLevelDestination.STREAM,
)

private class NoOpStreamContinuityRepository : StreamContinuityRepository {
    private val state = MutableStateFlow(
        StreamContinuityState(
            hasActiveOutput = false,
            outputState = OutputState.READY,
        ),
    )

    override fun observeContinuityState(): StateFlow<StreamContinuityState> = state.asStateFlow()

    override suspend fun captureLastKnownState() = Unit

    override suspend fun markAppBackgrounded(reason: BackgroundContinuationReason) = Unit

    override suspend fun markAppForegrounded() = Unit

    override suspend fun clearTransientStateOnExplicitStop() = Unit
}

class OutputControlViewModel(
    private val outputRepository: NdiOutputRepository,
    private val outputConfigurationRepository: OutputConfigurationRepository,
    private val screenCaptureConsentRepository: ScreenCaptureConsentRepository = OutputDependencies.requireScreenCaptureConsentRepository(),
    private val telemetryEmitter: OutputTelemetryEmitter = OutputDependencies.telemetryEmitter,
    private val streamContinuityRepository: StreamContinuityRepository =
        OutputDependencies.streamContinuityRepositoryProvider?.invoke() ?: NoOpStreamContinuityRepository(),
) : ViewModel() {

    private val retryWindowSeconds = 15
    private var lastObservedState: OutputState = OutputState.READY

    private val _uiState = MutableStateFlow(OutputControlUiState())
    val uiState: StateFlow<OutputControlUiState> = _uiState.asStateFlow()

    private val _consentPromptEvents = MutableSharedFlow<String>(extraBufferCapacity = 1)
    val consentPromptEvents: SharedFlow<String> = _consentPromptEvents.asSharedFlow()

    init {
        viewModelScope.launch {
            outputRepository.observeOutputSession().collect { session ->
                _uiState.update {
                    it.copy(
                        sourceId = session.inputSourceId,
                        streamName = session.outboundStreamName,
                        outputState = session.state,
                        canStart = session.state == OutputState.READY || session.state == OutputState.STOPPED,
                        canStop = session.state == OutputState.ACTIVE || session.state == OutputState.STARTING || session.state == OutputState.INTERRUPTED,
                        canRetry = session.state == OutputState.INTERRUPTED,
                        showRecoveryActions = session.state == OutputState.INTERRUPTED,
                        recoveryInProgress = session.state == OutputState.STARTING && session.retryAttempts > 0,
                        errorMessage = session.interruptionReason,
                    )
                }
                if (session.state == OutputState.INTERRUPTED && lastObservedState != OutputState.INTERRUPTED) {
                    telemetryEmitter.emit(OutputTelemetry.outputInterrupted(session.inputSourceId, session.interruptionReason))
                }
                lastObservedState = session.state
            }
        }
    }

    fun onOutputScreenVisible(sourceId: String) {
        if (sourceId.isBlank()) return
        viewModelScope.launch {
            val config = outputConfigurationRepository.getConfiguration()
            val snapshot = _uiState.value
            _uiState.update {
                it.copy(
                    sourceId = sourceId,
                    streamName = if (snapshot.outputState == OutputState.ACTIVE && snapshot.sourceId == sourceId) {
                        snapshot.streamName
                    } else {
                        config.preferredStreamName
                    },
                    isLocalScreenSource = sourceId.startsWith("device-screen:"),
                )
            }
            outputConfigurationRepository.saveLastSelectedInputSource(sourceId)
            streamContinuityRepository.captureLastKnownState()
        }
    }

    fun onStreamNameChanged(value: String) {
        _uiState.update { it.copy(streamName = value) }
    }

    fun onStartOutputPressed() {
        val snapshot = _uiState.value
        if (snapshot.sourceId.isBlank()) return
        if (!snapshot.canStart) {
            telemetryEmitter.emit(OutputTelemetry.outputStartIgnoredDuplicate(snapshot.sourceId))
            return
        }

        viewModelScope.launch {
            if (snapshot.sourceId.startsWith("device-screen:")) {
                val consent = screenCaptureConsentRepository.getConsentState(snapshot.sourceId)
                if (consent?.granted != true) {
                    screenCaptureConsentRepository.beginConsentRequest(snapshot.sourceId)
                    telemetryEmitter.emit(OutputTelemetry.screenShareConsentRequested(snapshot.sourceId))
                    _consentPromptEvents.emit(snapshot.sourceId)
                    _uiState.update { it.copy(consentRequired = true) }
                    return@launch
                }
            }

            runCatching {
                telemetryEmitter.emit(OutputTelemetry.outputStartRequested(snapshot.sourceId))
                outputRepository.startOutput(snapshot.sourceId, snapshot.streamName)
                outputConfigurationRepository.savePreferredStreamName(snapshot.streamName)
                telemetryEmitter.emit(OutputTelemetry.outputStarted(snapshot.sourceId))
                _uiState.update { it.copy(consentRequired = false) }
            }.onFailure { error ->
                _uiState.update {
                    it.copy(
                        outputState = OutputState.INTERRUPTED,
                        canStart = true,
                        canStop = false,
                        consentRequired = snapshot.sourceId.startsWith("device-screen:"),
                        errorMessage = error.message ?: "Unable to start output",
                    )
                }
            }
        }
    }

    fun onScreenCaptureConsentResult(granted: Boolean, tokenRef: String? = null) {
        val snapshot = _uiState.value
        if (!snapshot.sourceId.startsWith("device-screen:")) return

        viewModelScope.launch {
            screenCaptureConsentRepository.registerConsentResult(snapshot.sourceId, granted, tokenRef)
            if (granted) {
                telemetryEmitter.emit(OutputTelemetry.screenShareConsentGranted(snapshot.sourceId))
                _uiState.update { it.copy(consentRequired = false) }
                onStartOutputPressed()
            } else {
                telemetryEmitter.emit(OutputTelemetry.screenShareConsentDenied(snapshot.sourceId))
                _uiState.update {
                    it.copy(
                        outputState = OutputState.INTERRUPTED,
                        canStart = true,
                        canStop = false,
                        consentRequired = true,
                        errorMessage = "Screen capture consent denied",
                    )
                }
            }
        }
    }

    fun onStopOutputPressed() {
        val snapshot = _uiState.value
        if (snapshot.sourceId.isBlank()) return
        if (!snapshot.canStop) {
            telemetryEmitter.emit(OutputTelemetry.outputStopIgnoredDuplicate(snapshot.sourceId))
            return
        }

        _uiState.update {
            it.copy(
                outputState = OutputState.STOPPING,
                canStart = false,
                canStop = false,
                canRetry = false,
                showRecoveryActions = false,
                recoveryInProgress = false,
                errorMessage = null,
            )
        }

        viewModelScope.launch {
            runCatching {
                telemetryEmitter.emit(OutputTelemetry.outputStopRequested(snapshot.sourceId))
                outputRepository.stopOutput()
                streamContinuityRepository.clearTransientStateOnExplicitStop()
                telemetryEmitter.emit(OutputTelemetry.outputStopped(snapshot.sourceId))
            }.onFailure { error ->
                _uiState.update {
                    it.copy(
                        outputState = OutputState.INTERRUPTED,
                        canStart = true,
                        canStop = false,
                        canRetry = true,
                        showRecoveryActions = true,
                        recoveryInProgress = false,
                        errorMessage = error.message ?: "Unable to stop output",
                    )
                }
            }
        }
    }

    fun onRetryOutputPressed() {
        val snapshot = _uiState.value
        if (!snapshot.canRetry || snapshot.sourceId.isBlank()) return

        _uiState.update {
            it.copy(
                outputState = OutputState.STARTING,
                canStart = false,
                canStop = false,
                canRetry = false,
                showRecoveryActions = true,
                recoveryInProgress = true,
            )
        }

        viewModelScope.launch {
            runCatching {
                telemetryEmitter.emit(OutputTelemetry.outputRetryRequested(snapshot.sourceId, retryWindowSeconds))
                val result = outputRepository.retryInterruptedOutputWithinWindow(retryWindowSeconds)
                if (result.state == OutputState.ACTIVE) {
                    telemetryEmitter.emit(OutputTelemetry.outputRetrySucceeded(snapshot.sourceId, result.retryAttempts))
                } else {
                    telemetryEmitter.emit(OutputTelemetry.outputRetryFailed(snapshot.sourceId, result.retryAttempts))
                }
            }.onFailure { error ->
                telemetryEmitter.emit(OutputTelemetry.outputRetryFailed(snapshot.sourceId, 1))
                _uiState.update {
                    it.copy(
                        outputState = OutputState.INTERRUPTED,
                        canStart = true,
                        canStop = true,
                        canRetry = true,
                        showRecoveryActions = true,
                        recoveryInProgress = false,
                        errorMessage = error.message ?: "Recovery failed",
                    )
                }
            }
        }
    }

    class Factory(
        private val outputRepository: NdiOutputRepository,
        private val outputConfigurationRepository: OutputConfigurationRepository,
        private val screenCaptureConsentRepository: ScreenCaptureConsentRepository = OutputDependencies.requireScreenCaptureConsentRepository(),
        private val telemetryEmitter: OutputTelemetryEmitter = OutputDependencies.telemetryEmitter,
        private val streamContinuityRepository: StreamContinuityRepository =
            OutputDependencies.streamContinuityRepositoryProvider?.invoke() ?: NoOpStreamContinuityRepository(),
    ) : ViewModelProvider.Factory {
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(OutputControlViewModel::class.java))
            return OutputControlViewModel(
                outputRepository,
                outputConfigurationRepository,
                screenCaptureConsentRepository,
                telemetryEmitter,
                streamContinuityRepository,
            ) as T
        }
    }
}
