package com.ndi.core.model

data class DiscoveryServerCheckStatus(
    val serverId: String,
    val checkType: DiscoveryCheckType,
    val outcome: DiscoveryCheckOutcome,
    val checkedAtEpochMillis: Long,
    val failureCategory: DiscoveryFailureCategory,
    val failureMessage: String?,
    val correlationId: String,
)
