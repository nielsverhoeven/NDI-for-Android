package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.NdiSource

class CachedSourceIdentityResolver {
    fun buildCacheKey(source: NdiSource): String {
        val stableId = source.sourceId.trim().takeIf { it.isNotBlank() }
        val endpointKey = source.endpointAddress
            ?.trim()
            ?.lowercase()
            ?.takeIf { it.isNotBlank() }

        return stableId ?: endpointKey ?: "source:${source.displayName.trim().lowercase()}"
    }
}