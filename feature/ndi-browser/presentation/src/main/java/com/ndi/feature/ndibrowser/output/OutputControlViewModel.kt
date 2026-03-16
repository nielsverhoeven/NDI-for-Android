package com.ndi.feature.ndibrowser.output

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.OutputState
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.OutputConfigurationRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class OutputControlUiState(
    val sourceId: String = "",
    val streamName: String = "",
    val outputState: OutputState = OutputState.READY,
    val canStart: Boolean = true,
    val canStop: Boolean = false,
    val canRetry: Boolean = false,
    val showRecoveryActions: Boolean = false,
    val recoveryInProgress: Boolean = false,
    val errorMessage: String? = null,
)

class OutputControlViewModel(
    private val outputRepository: NdiOutputRepository,
    private val outputConfigurationRepository: OutputConfigurationRepository,
    private val telemetryEmitter: OutputTelemetryEmitter = OutputDependencies.telemetryEmitter,
) : ViewModel() {

    private val retryWindowSeconds = 15
    private var lastObservedState: OutputState = OutputState.READY

    private val _uiState = MutableStateFlow(OutputControlUiState())
    val uiState: StateFlow<OutputControlUiState> = _uiState.asStateFlow()

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
            _uiState.update {
                it.copy(
                    sourceId = sourceId,
                    streamName = config.preferredStreamName,
                )
            }
            outputConfigurationRepository.saveLastSelectedInputSource(sourceId)
        }
    }

    fun onStreamNameChanged(value: String) {
        _uiState.update { it.copy(streamName = value) }
    }

    fun onStartOutputPressed() {
        val snapshot = _uiState.value
        if (!snapshot.canStart || snapshot.sourceId.isBlank()) return

        viewModelScope.launch {
            runCatching {
                telemetryEmitter.emit(OutputTelemetry.outputStartRequested(snapshot.sourceId))
                outputRepository.startOutput(snapshot.sourceId, snapshot.streamName)
                outputConfigurationRepository.savePreferredStreamName(snapshot.streamName)
            }.onFailure { error ->
                _uiState.update {
                    it.copy(
                        outputState = OutputState.INTERRUPTED,
                        canStart = true,
                        canStop = false,
                        errorMessage = error.message ?: "Unable to start output",
                    )
                }
            }
        }
    }

    fun onStopOutputPressed() {
        val snapshot = _uiState.value
        if (!snapshot.canStop || snapshot.sourceId.isBlank()) return

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
        private val telemetryEmitter: OutputTelemetryEmitter = OutputDependencies.telemetryEmitter,
    ) : ViewModelProvider.Factory {
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(OutputControlViewModel::class.java))
            return OutputControlViewModel(outputRepository, outputConfigurationRepository, telemetryEmitter) as T
        }
    }
}
