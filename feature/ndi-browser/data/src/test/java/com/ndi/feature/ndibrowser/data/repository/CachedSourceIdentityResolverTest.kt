package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.NdiSource
import org.junit.Assert.assertEquals
import org.junit.Test

class CachedSourceIdentityResolverTest {

    private val resolver = CachedSourceIdentityResolver()

    @Test
    fun buildCacheKey_prefersStableSourceId_whenPresent() {
        val source = NdiSource(
            sourceId = "ndi-source-123",
            displayName = "Camera 1",
            endpointAddress = "10.0.0.8:5960",
            lastSeenAtEpochMillis = 1L,
        )

        val key = resolver.buildCacheKey(source)

        assertEquals("ndi-source-123", key)
    }

    @Test
    fun buildCacheKey_usesEndpoint_whenSourceIdBlank() {
        val source = NdiSource(
            sourceId = "",
            displayName = "Camera 2",
            endpointAddress = "10.0.0.9:5960",
            lastSeenAtEpochMillis = 1L,
        )

        val key = resolver.buildCacheKey(source)

        assertEquals("camera 2", key)
    }

    @Test
    fun buildCacheKey_usesCanonicalIdentityWhenSourceIdBlankAndEndpointChanges() {
        val first = NdiSource(
            sourceId = "",
            displayName = "Studio Camera A",
            endpointAddress = "10.0.0.9:5960",
            lastSeenAtEpochMillis = 1L,
        )
        val second = first.copy(endpointAddress = "10.0.0.10:5961", lastSeenAtEpochMillis = 2L)

        val key1 = resolver.buildCacheKey(first)
        val key2 = resolver.buildCacheKey(second)

        assertEquals("studio camera a", key1)
        assertEquals(key1, key2)
    }
}
