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
    val isAvailable: Boolean = true,
    val previouslyConnected: Boolean = false,
    val lastFramePreviewPath: String? = null,
)

enum class CachedSourceValidationState {
    NOT_YET_VALIDATED,
    VALIDATING,
    AVAILABLE,
    UNAVAILABLE,
}

// ---- T007: Discovery Routing Models ----

enum class DiscoveryMode {
    MULTICAST,
    DISCOVERY_SERVER,
}

data class DiscoveryModeSnapshot(
    val runId: String,
    val startedAtEpochMillis: Long,
    val enabledServerCount: Int,
    val mode: DiscoveryMode,
    val modeSelectionReason: String,
)

data class DiscoveredSourceEndpoint(
    val canonicalSourceId: String,
    val displayName: String,
    val endpointHost: String,
    val endpointPort: Int,
    val discoveredAtEpochMillis: Long,
    val originMode: DiscoveryMode,
    val originServerId: String? = null,
)

enum class DiscoveryRunStatus {
    SUCCESS,
    TIMEOUT,
    FAILURE,
}

data class DiscoveryRunResult(
    val runId: String,
    val mode: DiscoveryMode,
    val durationMillis: Long,
    val status: DiscoveryRunStatus,
    val sourceCount: Int,
    val diagnosticCode: String? = null,
    val diagnosticMessage: String? = null,
)

data class CachedSourceRecord(
    val cacheKey: String,
    val stableSourceId: String? = null,
    val lastObservedSourceId: String? = null,
    val displayName: String,
    val endpointHost: String,
    val endpointPort: Int,
    val endpointKey: String,
    val validationState: CachedSourceValidationState = CachedSourceValidationState.NOT_YET_VALIDATED,
    val lastAvailableAtEpochMillis: Long? = null,
    val lastValidatedAtEpochMillis: Long? = null,
    val lastValidationStartedAtEpochMillis: Long? = null,
    val firstCachedAtEpochMillis: Long,
    val lastDiscoveredAtEpochMillis: Long,
    val retainedPreviewImagePath: String? = null,
    val lastPreviewCapturedAtEpochMillis: Long? = null,
    val updatedAtEpochMillis: Long,
    val discoveryServerIds: List<String> = emptyList(),
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
