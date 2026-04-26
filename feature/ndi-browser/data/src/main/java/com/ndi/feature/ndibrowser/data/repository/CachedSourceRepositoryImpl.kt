package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.CachedSourceDao
import com.ndi.core.database.CachedSourceDiscoveryServerCrossRefDao
import com.ndi.core.database.CachedSourceDiscoveryServerCrossRefEntity
import com.ndi.core.model.CachedSourceRecord
import com.ndi.core.model.CachedSourceValidationState
import com.ndi.feature.ndibrowser.data.mapper.CachedSourceMapper
import com.ndi.feature.ndibrowser.domain.repository.CachedSourceRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

class CachedSourceRepositoryImpl(
    private val cachedSourceDao: CachedSourceDao,
    private val crossRefDao: CachedSourceDiscoveryServerCrossRefDao,
    private val mapper: CachedSourceMapper = CachedSourceMapper(),
) : CachedSourceRepository {

    override fun observeCachedSources(): Flow<List<CachedSourceRecord>> {
        return cachedSourceDao.observeAll().map { entities ->
            entities.map { entity ->
                val ids = runCatching {
                    crossRefDao.getByCacheKey(entity.cacheKey).map { it.discoveryServerId }
                }.getOrDefault(emptyList())
                mapper.toRecord(entity, ids)
            }
        }
    }

    override suspend fun getCachedSource(cacheKey: String): CachedSourceRecord? {
        val entity = cachedSourceDao.getByKey(cacheKey) ?: return null
        val ids = crossRefDao.getByCacheKey(cacheKey).map { it.discoveryServerId }
        return mapper.toRecord(entity, ids)
    }

    override suspend fun upsertCachedSource(record: CachedSourceRecord) {
        cachedSourceDao.upsert(mapper.toEntity(record))
    }

    override suspend fun upsertFromDiscovery(record: CachedSourceRecord) {
        // Insert the full row if new; for existing rows update only discovery fields
        // so the retained preview image path is never wiped by a discovery scan.
        cachedSourceDao.insertIfAbsent(mapper.toEntity(record))
        cachedSourceDao.updateFromDiscovery(
            cacheKey = record.cacheKey,
            lastObservedSourceId = record.lastObservedSourceId,
            displayName = record.displayName,
            endpointHost = record.endpointHost,
            endpointPort = record.endpointPort,
            endpointKey = record.endpointKey,
            validationState = record.validationState.name,
            lastAvailableAtEpochMillis = record.lastAvailableAtEpochMillis,
            lastValidatedAtEpochMillis = record.lastValidatedAtEpochMillis,
            lastDiscoveredAtEpochMillis = record.lastDiscoveredAtEpochMillis,
            updatedAtEpochMillis = record.updatedAtEpochMillis,
        )
    }

    override suspend fun upsertDiscoveryAssociation(
        cacheKey: String,
        discoveryServerId: String,
        observedAtEpochMillis: Long,
    ) {
        val existing = crossRefDao.getByCacheKey(cacheKey).firstOrNull { it.discoveryServerId == discoveryServerId }
        if (existing == null) {
            crossRefDao.upsert(
                CachedSourceDiscoveryServerCrossRefEntity(
                    cacheKey = cacheKey,
                    discoveryServerId = discoveryServerId,
                    firstObservedAtEpochMillis = observedAtEpochMillis,
                    lastObservedAtEpochMillis = observedAtEpochMillis,
                ),
            )
            return
        }

        crossRefDao.updateLastObserved(
            cacheKey = cacheKey,
            discoveryServerId = discoveryServerId,
            lastObservedAtEpochMillis = observedAtEpochMillis,
        )
    }

    override suspend fun markValidationState(
        cacheKey: String,
        state: CachedSourceValidationState,
        validationStartedAtEpochMillis: Long?,
        validatedAtEpochMillis: Long?,
        availableAtEpochMillis: Long?,
    ) {
        cachedSourceDao.updateValidationState(
            cacheKey = cacheKey,
            validationState = state.name,
            startedAtEpochMillis = validationStartedAtEpochMillis,
            validatedAtEpochMillis = validatedAtEpochMillis,
            availableAtEpochMillis = availableAtEpochMillis,
            updatedAtEpochMillis = System.currentTimeMillis(),
        )
    }
}