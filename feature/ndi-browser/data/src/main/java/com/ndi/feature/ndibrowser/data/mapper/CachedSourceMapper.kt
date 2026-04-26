package com.ndi.feature.ndibrowser.data.mapper

import com.ndi.core.database.CachedSourceEntity
import com.ndi.core.model.CachedSourceRecord
import com.ndi.core.model.CachedSourceValidationState

class CachedSourceMapper {
    fun toRecord(
        entity: CachedSourceEntity,
        discoveryServerIds: List<String>,
    ): CachedSourceRecord {
        return CachedSourceRecord(
            cacheKey = entity.cacheKey,
            stableSourceId = entity.stableSourceId,
            lastObservedSourceId = entity.lastObservedSourceId,
            displayName = entity.displayName,
            endpointHost = entity.endpointHost,
            endpointPort = entity.endpointPort,
            endpointKey = entity.endpointKey,
            validationState = runCatching {
                CachedSourceValidationState.valueOf(entity.validationState)
            }.getOrDefault(CachedSourceValidationState.NOT_YET_VALIDATED),
            lastAvailableAtEpochMillis = entity.lastAvailableAtEpochMillis,
            lastValidatedAtEpochMillis = entity.lastValidatedAtEpochMillis,
            lastValidationStartedAtEpochMillis = entity.lastValidationStartedAtEpochMillis,
            firstCachedAtEpochMillis = entity.firstCachedAtEpochMillis,
            lastDiscoveredAtEpochMillis = entity.lastDiscoveredAtEpochMillis,
            retainedPreviewImagePath = entity.retainedPreviewImagePath,
            lastPreviewCapturedAtEpochMillis = entity.lastPreviewCapturedAtEpochMillis,
            updatedAtEpochMillis = entity.updatedAtEpochMillis,
            discoveryServerIds = discoveryServerIds,
        )
    }

    fun toEntity(record: CachedSourceRecord): CachedSourceEntity {
        return CachedSourceEntity(
            cacheKey = record.cacheKey,
            stableSourceId = record.stableSourceId,
            lastObservedSourceId = record.lastObservedSourceId,
            displayName = record.displayName,
            endpointHost = record.endpointHost,
            endpointPort = record.endpointPort,
            endpointKey = record.endpointKey,
            validationState = record.validationState.name,
            lastAvailableAtEpochMillis = record.lastAvailableAtEpochMillis,
            lastValidatedAtEpochMillis = record.lastValidatedAtEpochMillis,
            lastValidationStartedAtEpochMillis = record.lastValidationStartedAtEpochMillis,
            firstCachedAtEpochMillis = record.firstCachedAtEpochMillis,
            lastDiscoveredAtEpochMillis = record.lastDiscoveredAtEpochMillis,
            retainedPreviewImagePath = record.retainedPreviewImagePath,
            lastPreviewCapturedAtEpochMillis = record.lastPreviewCapturedAtEpochMillis,
            updatedAtEpochMillis = record.updatedAtEpochMillis,
        )
    }
}