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
