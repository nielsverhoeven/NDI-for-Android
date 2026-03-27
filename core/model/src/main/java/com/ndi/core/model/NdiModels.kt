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

data class ViewerVideoFrame(
    val width: Int,
    val height: Int,
    val argbPixels: IntArray,
    val capturedAtEpochMillis: Long = System.currentTimeMillis(),
)

data class UserSelectionState(
    val lastSelectedSourceId: String? = null,
    val lastSelectedAtEpochMillis: Long? = null,
    val shouldAutoplayOnLaunch: Boolean = false,
)

enum class OutputState {
    READY,
    STARTING,
    ACTIVE,
    STOPPING,
    STOPPED,
    INTERRUPTED,
}

enum class OutputInputKind {
    DISCOVERED_NDI,
    DEVICE_SCREEN,
}

enum class OutputConsentState {
    NOT_REQUIRED,
    PENDING,
    GRANTED,
    DENIED,
}

enum class OutputQualityLevel {
    HEALTHY,
    DEGRADED,
    FAILED,
}

data class OutputSession(
    val sessionId: String,
    val inputSourceId: String,
    val inputSourceKind: OutputInputKind = OutputInputKind.DISCOVERED_NDI,
    val outboundStreamName: String,
    val consentState: OutputConsentState = OutputConsentState.NOT_REQUIRED,
    val state: OutputState,
    val startedAtEpochMillis: Long,
    val stoppedAtEpochMillis: Long? = null,
    val interruptionReason: String? = null,
    val retryAttempts: Int = 0,
    val hostInstanceId: String = "local",
)

data class OutputConfiguration(
    val preferredStreamName: String,
    val lastSelectedInputSourceId: String? = null,
    val lastSelectedInputSourceKind: OutputInputKind? = null,
    val autoRetryEnabled: Boolean = true,
    val retryWindowSeconds: Int = 15,
)

data class OutputInputIdentity(
    val sourceId: String,
    val kind: OutputInputKind,
    val displayName: String,
    val hostInstanceId: String? = null,
    val requiresCaptureConsent: Boolean = kind == OutputInputKind.DEVICE_SCREEN,
)

data class OutputHealthSnapshot(
    val snapshotId: String,
    val sessionId: String,
    val capturedAtEpochMillis: Long,
    val networkReachable: Boolean,
    val inputReachable: Boolean,
    val qualityLevel: OutputQualityLevel,
    val messageCode: String? = null,
)
