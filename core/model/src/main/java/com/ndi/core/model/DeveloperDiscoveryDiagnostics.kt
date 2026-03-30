package com.ndi.core.model

data class DeveloperDiscoveryDiagnostics(
    val developerModeEnabled: Boolean = false,
    val latestDiscoveryRefreshStatus: DiscoveryStatus? = null,
    val latestDiscoveryRefreshAtEpochMillis: Long? = null,
    val serverStatusRollup: List<DiscoveryServerCheckStatus> = emptyList(),
    val recentDiscoveryLogs: List<String> = emptyList(),
)
