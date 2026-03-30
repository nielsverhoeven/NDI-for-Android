package com.ndi.feature.ndibrowser.output

import androidx.core.view.isVisible
import com.ndi.core.model.OutputState
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentOutputControlBinding
import com.ndi.feature.ndibrowser.settings.DeveloperOverlayRenderer

class OutputControlScreen(
    private val binding: FragmentOutputControlBinding,
    onStartPressed: () -> Unit,
    onStopPressed: () -> Unit,
    onRetryPressed: () -> Unit,
) {
    init {
        binding.startButton.setOnClickListener { onStartPressed() }
        binding.stopButton.setOnClickListener { onStopPressed() }
        binding.retryButton.setOnClickListener { onRetryPressed() }
    }

    fun currentStreamName(): String {
        return binding.streamNameInput.text?.toString().orEmpty()
    }

    fun render(state: OutputControlUiState) {
        val sourceTitle = state.sourceId.ifBlank {
            binding.root.context.getString(R.string.ndi_output_default_stream_name)
        }
        binding.outputTitle.text = binding.root.context.getString(R.string.ndi_output_title, sourceTitle)
        val statusText = when (state.outputState) {
            OutputState.ACTIVE -> binding.root.context.getString(R.string.ndi_output_status_active)
            OutputState.STARTING -> binding.root.context.getString(R.string.ndi_output_status_starting)
            OutputState.STOPPING -> binding.root.context.getString(R.string.ndi_output_status_stopping)
            OutputState.STOPPED -> binding.root.context.getString(R.string.ndi_output_status_stopped)
            OutputState.INTERRUPTED -> binding.root.context.getString(R.string.ndi_output_status_interrupted)
            OutputState.READY -> binding.root.context.getString(R.string.ndi_output_status_ready)
        }
        binding.outputState.text = binding.root.context.getString(R.string.ndi_output_status_label, statusText)
        binding.startButton.text = if (state.isLocalScreenSource) {
            binding.root.context.getString(R.string.ndi_output_share_screen)
        } else {
            binding.root.context.getString(R.string.ndi_output_start)
        }
        binding.startButton.isEnabled = state.canStart
        binding.stopButton.isEnabled = state.canStop
        binding.retryButton.isVisible = state.showRecoveryActions
        binding.retryButton.isEnabled = state.canRetry && !state.recoveryInProgress
        binding.consentMessage.isVisible = state.isLocalScreenSource && state.consentRequired
        binding.streamNameInput.isEnabled = state.outputState != OutputState.STOPPING && !state.recoveryInProgress
        binding.errorMessage.isVisible = !state.errorMessage.isNullOrBlank()
        binding.errorMessage.text = state.errorMessage.orEmpty()

        val currentText = binding.streamNameInput.text?.toString().orEmpty()
        if (state.streamName != currentText) {
            binding.streamNameInput.setText(state.streamName)
            binding.streamNameInput.setSelection(binding.streamNameInput.text?.length ?: 0)
        }

        if (state.outputState == OutputState.INTERRUPTED) {
            val errorText = if (state.errorMessage?.contains("discovery server", ignoreCase = true) == true) {
                binding.root.context.getString(R.string.ndi_output_discovery_unreachable_help)
            } else if (state.errorMessage?.contains("consent", ignoreCase = true) == true ||
                state.errorMessage?.contains("denied", ignoreCase = true) == true
            ) {
                binding.root.context.getString(R.string.ndi_output_consent_denied_help)
            } else {
                state.errorMessage
            }
            binding.streamNameInputLayout.error = errorText
        } else {
            binding.streamNameInputLayout.error = null
        }

        DeveloperOverlayRenderer.render(
            container = binding.developerOverlay.developerOverlayContainer,
            streamStatusView = binding.developerOverlay.overlayStreamStatus,
            sessionIdView = binding.developerOverlay.overlaySessionId,
            recentLogsView = binding.developerOverlay.overlayRecentLogs,
            overlayDisplayState = state.overlayDisplayState,
        )
    }
}
