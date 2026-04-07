package com.ndi.core.model

data class CompatibilityGuidance(
    val targetId: String,
    val status: DiscoveryCompatibilityStatus,
    val message: String,
    val recommendedNextStep: String,
    val evidenceRef: String? = null,
)

data class DeveloperDiscoveryDiagnostics(
    val developerModeEnabled: Boolean = false,
    val latestDiscoveryRefreshStatus: DiscoveryStatus? = null,
    val latestDiscoveryRefreshAtEpochMillis: Long? = null,
    val serverStatusRollup: List<DiscoveryServerCheckStatus> = emptyList(),
    val recentDiscoveryLogs: List<String> = emptyList(),
    val compatibilityGuidance: List<CompatibilityGuidance> = emptyList(),
)
