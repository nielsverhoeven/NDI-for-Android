package com.ndi.feature.ndibrowser.data.mapper

import com.ndi.core.database.CachedSourceEntity
import com.ndi.core.model.CachedSourceRecord
import com.ndi.core.model.CachedSourceValidationState
import org.junit.Assert.assertEquals
import org.junit.Test

class CachedSourceMapperTest {

    private val mapper = CachedSourceMapper()

    @Test
    fun toRecord_mapsValidationAndDiscoveryServers() {
        val entity = CachedSourceEntity(
            cacheKey = "key-1",
            stableSourceId = "source-1",
            lastObservedSourceId = "source-1",
            displayName = "Camera 1",
            endpointHost = "10.0.0.8",
            endpointPort = 5960,
            endpointKey = "10.0.0.8:5960",
            validationState = CachedSourceValidationState.VALIDATING.name,
            lastAvailableAtEpochMillis = 10L,
            lastValidatedAtEpochMillis = 11L,
            lastValidationStartedAtEpochMillis = 12L,
            firstCachedAtEpochMillis = 1L,
            lastDiscoveredAtEpochMillis = 2L,
            retainedPreviewImagePath = "preview.png",
            lastPreviewCapturedAtEpochMillis = 13L,
            updatedAtEpochMillis = 14L,
        )

        val record = mapper.toRecord(entity, listOf("server-a", "server-b"))

        assertEquals("key-1", record.cacheKey)
        assertEquals(CachedSourceValidationState.VALIDATING, record.validationState)
        assertEquals(2, record.discoveryServerIds.size)
    }

    @Test
    fun toEntity_mapsValidationStateName() {
        val record = CachedSourceRecord(
            cacheKey = "key-2",
            stableSourceId = null,
            lastObservedSourceId = "runtime-2",
            displayName = "Camera 2",
            endpointHost = "10.0.0.9",
            endpointPort = 5961,
            endpointKey = "10.0.0.9:5961",
            validationState = CachedSourceValidationState.AVAILABLE,
            firstCachedAtEpochMillis = 100L,
            lastDiscoveredAtEpochMillis = 101L,
            updatedAtEpochMillis = 102L,
        )

        val entity = mapper.toEntity(record)

        assertEquals(CachedSourceValidationState.AVAILABLE.name, entity.validationState)
        assertEquals("10.0.0.9", entity.endpointHost)
        assertEquals(5961, entity.endpointPort)
    }
}
