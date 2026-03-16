package com.ndi.feature.ndibrowser.output

import androidx.core.view.isVisible
import com.ndi.core.model.OutputState
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentOutputControlBinding

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
        binding.outputState.text = when (state.outputState) {
            OutputState.ACTIVE -> "ACTIVE"
            OutputState.STARTING -> "STARTING"
            OutputState.STOPPING -> "STOPPING"
            OutputState.STOPPED -> "STOPPED"
            OutputState.INTERRUPTED -> "INTERRUPTED"
            OutputState.READY -> "READY"
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
            binding.streamNameInputLayout.error = state.errorMessage
        } else {
            binding.streamNameInputLayout.error = null
        }
    }
}
