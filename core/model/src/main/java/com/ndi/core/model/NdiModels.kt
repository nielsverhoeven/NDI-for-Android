package com.ndi.core.model

enum class DiscoveryStatus {
    IN_PROGRESS,
    SUCCESS,
    EMPTY,
    FAILURE,
}

enum class DiscoveryTrigger {
    MANUAL,
    FOREGROUND_TICK,
}

enum class PlaybackState {
    IDLE,
    CONNECTING,
    PLAYING,
    INTERRUPTED,
    STOPPED,
}

data class NdiSource(
    val sourceId: String,
    val displayName: String,
    val endpointAddress: String? = null,
    val isReachable: Boolean = true,
    val lastSeenAtEpochMillis: Long,
)

data class DiscoverySnapshot(
    val snapshotId: String,
    val startedAtEpochMillis: Long,
    val completedAtEpochMillis: Long,
    val status: DiscoveryStatus,
    val sourceCount: Int,
    val sources: List<NdiSource>,
    val errorCode: String? = null,
    val errorMessage: String? = null,
)

data class ViewerSession(
    val sessionId: String,
    val selectedSourceId: String,
    val playbackState: PlaybackState,
    val interruptionReason: String? = null,
    val retryWindowSeconds: Int = 15,
    val retryAttempts: Int = 0,
    val startedAtEpochMillis: Long,
    val endedAtEpochMillis: Long? = null,
)

data class UserSelectionState(
    val lastSelectedSourceId: String? = null,
    val lastSelectedAtEpochMillis: Long? = null,
    val shouldAutoplayOnLaunch: Boolean = false,
)
