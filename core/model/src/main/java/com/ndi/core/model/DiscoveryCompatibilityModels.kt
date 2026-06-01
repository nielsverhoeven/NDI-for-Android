package com.ndi.core.model

enum class DiscoveryCompatibilityStatus {
    PENDING,
    COMPATIBLE,
    LIMITED,
    INCOMPATIBLE,
    BLOCKED,
}

data class DiscoveryCompatibilityResult(
    val targetId: String,
    val status: DiscoveryCompatibilityStatus,
    val discoveredSourceCount: Int,
    val streamStartAttempted: Boolean,
    val streamStartSucceeded: Boolean,
    val temporaryUnknownObserved: Boolean = false,
    val notes: String? = null,
)

data class DiscoveryCompatibilitySnapshot(
    val recordedAtEpochMillis: Long,
    val results: List<DiscoveryCompatibilityResult>,
)

// ---- T007: Discovery Server Diagnostics ----

enum class DiscoveryServerAttemptStatus {
    SUCCESS,
    TIMEOUT,
    UNREACHABLE,
    ERROR,
}

data class DiscoveryServerDiagnosticRecord(
    val runId: String,
    val serverId: String,
    val endpoint: String,
    val attemptStartedAtEpochMillis: Long,
    val durationMillis: Long,
    val status: DiscoveryServerAttemptStatus,
    val errorDetail: String? = null,
)
