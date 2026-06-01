package com.ndi.feature.ndibrowser.viewer

import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerSession

data class ViewerRecoveryUiState(
    val interruptionMessage: String? = null,
    val recoveryActionsVisible: Boolean = false,
)

fun ViewerSession.toViewerRecoveryUiState(): ViewerRecoveryUiState {
    return when (playbackState) {
        PlaybackState.INTERRUPTED -> ViewerRecoveryUiState(
            interruptionMessage = interruptionReason ?: "Playback interrupted",
            recoveryActionsVisible = false,
        )
        PlaybackState.STOPPED -> ViewerRecoveryUiState(
            interruptionMessage = interruptionReason ?: "Playback interrupted",
            recoveryActionsVisible = selectedSourceId.isNotBlank(),
        )
        else -> ViewerRecoveryUiState()
    }
}
