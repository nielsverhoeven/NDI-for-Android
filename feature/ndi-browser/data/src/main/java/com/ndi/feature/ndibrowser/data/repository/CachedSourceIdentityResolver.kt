package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.NdiSource

class CachedSourceIdentityResolver {
    fun buildCacheKey(source: NdiSource): String {
        val stableId = source.sourceId.trim().takeIf { it.isNotBlank() }
        val canonicalDisplayIdentity = source.displayName
            .trim()
            .lowercase()
            .takeIf { it.isNotBlank() }
        val endpointKey = source.endpointAddress
            ?.trim()
            ?.lowercase()
            ?.takeIf { it.isNotBlank() }

        return stableId ?: canonicalDisplayIdentity ?: endpointKey ?: "source:unknown"
    }
}