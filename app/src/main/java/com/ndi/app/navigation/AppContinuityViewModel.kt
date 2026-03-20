package com.ndi.app.navigation

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.navigation.BackgroundContinuationReason
import com.ndi.feature.ndibrowser.domain.repository.StreamContinuityRepository
import com.ndi.feature.ndibrowser.output.OutputDependencies
import com.ndi.feature.ndibrowser.output.OutputTelemetry
import com.ndi.feature.ndibrowser.output.OutputTelemetryEmitter
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch

/**
 * App-level continuity mediator that translates process/activity lifecycle events into
 * stream continuity repository transitions.
 */
class AppContinuityViewModel(
    private val streamContinuityRepository: StreamContinuityRepository,
    private val telemetryEmitter: OutputTelemetryEmitter = OutputDependencies.telemetryEmitter,
) : ViewModel() {

    private var wasBackgrounded: Boolean = false

    fun onAppForegrounded() {
        viewModelScope.launch {
            if (!wasBackgrounded) return@launch
            streamContinuityRepository.captureLastKnownState()
            val continuity = streamContinuityRepository.observeContinuityState().first()
            if (!continuity.hasActiveOutput) return@launch

            streamContinuityRepository.markAppForegrounded()
            wasBackgrounded = false
            val sourceId = continuity.lastKnownOutputSourceId ?: return@launch
            telemetryEmitter.emit(OutputTelemetry.outputContinuityForegrounded(sourceId))
        }
    }

    fun onAppBackgrounded(isConfigurationChange: Boolean) {
        if (isConfigurationChange) return

        viewModelScope.launch {
            streamContinuityRepository.captureLastKnownState()
            val continuity = streamContinuityRepository.observeContinuityState().first()
            if (!continuity.hasActiveOutput) return@launch

            streamContinuityRepository.markAppBackgrounded(BackgroundContinuationReason.APP_BACKGROUND)
            wasBackgrounded = true
            val sourceId = continuity.lastKnownOutputSourceId ?: return@launch
            telemetryEmitter.emit(
                OutputTelemetry.outputContinuityBackgrounded(sourceId, BackgroundContinuationReason.APP_BACKGROUND),
            )
        }
    }

    class Factory(
        private val streamContinuityRepository: StreamContinuityRepository,
        private val telemetryEmitter: OutputTelemetryEmitter = OutputDependencies.telemetryEmitter,
    ) : ViewModelProvider.Factory {
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(AppContinuityViewModel::class.java))
            @Suppress("UNCHECKED_CAST")
            return AppContinuityViewModel(streamContinuityRepository, telemetryEmitter) as T
        }
    }
}
