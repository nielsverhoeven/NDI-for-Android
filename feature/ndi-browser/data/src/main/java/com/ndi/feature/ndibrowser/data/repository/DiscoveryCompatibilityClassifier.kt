package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.DiscoveryCompatibilityStatus

class DiscoveryCompatibilityClassifier {

    fun classify(
        discoverySucceeded: Boolean,
        streamStartAttempted: Boolean,
        streamStartSucceeded: Boolean,
        blocked: Boolean,
    ): DiscoveryCompatibilityStatus {
        if (blocked) return DiscoveryCompatibilityStatus.BLOCKED
        if (discoverySucceeded && streamStartSucceeded) return DiscoveryCompatibilityStatus.COMPATIBLE
        if (discoverySucceeded && streamStartAttempted && !streamStartSucceeded) {
            return DiscoveryCompatibilityStatus.INCOMPATIBLE
        }
        if (discoverySucceeded) return DiscoveryCompatibilityStatus.LIMITED
        return DiscoveryCompatibilityStatus.PENDING
    }
}
