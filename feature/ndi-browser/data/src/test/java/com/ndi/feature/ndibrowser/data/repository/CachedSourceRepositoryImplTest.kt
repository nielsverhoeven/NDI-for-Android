package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.CachedSourceDao
import com.ndi.core.database.CachedSourceDiscoveryServerCrossRefDao
import com.ndi.core.database.CachedSourceDiscoveryServerCrossRefEntity
import com.ndi.core.database.CachedSourceEntity
import com.ndi.core.model.CachedSourceRecord
import com.ndi.core.model.CachedSourceValidationState
import com.ndi.feature.ndibrowser.data.mapper.CachedSourceMapper
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test

/**
 * T011: Regression tests for cache merge correctness and persistence preservation.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class CachedSourceRepositoryImplTest {

    private lateinit var mockCachedSourceDao: MockCachedSourceDao
    private lateinit var mockCrossRefDao: MockCachedSourceDiscoveryServerCrossRefDao
    private lateinit var repository: CachedSourceRepositoryImpl

    @Before
    fun setUp() {
        mockCachedSourceDao = MockCachedSourceDao()
        mockCrossRefDao = MockCachedSourceDiscoveryServerCrossRefDao()
        repository = CachedSourceRepositoryImpl(
            cachedSourceDao = mockCachedSourceDao,
            crossRefDao = mockCrossRefDao,
            mapper = CachedSourceMapper(),
        )
    }

    @Test
    fun upsertFromDiscovery_newSource_createsRecord() = runTest {
        val record = CachedSourceRecord(
            cacheKey = "cache1",
            stableSourceId = "source1",
            lastObservedSourceId = "source1",
            displayName = "Test Source",
            endpointHost = "192.168.1.10",
            endpointPort = 5960,
            endpointKey = "192.168.1.10:5960",
            validationState = CachedSourceValidationState.NOT_YET_VALIDATED,
            firstCachedAtEpochMillis = 1000L,
            lastDiscoveredAtEpochMillis = 1000L,
            retainedPreviewImagePath = null,
            updatedAtEpochMillis = 1000L,
        )

        repository.upsertFromDiscovery(record)

        // Verify insertIfAbsent was called
        assertEquals(1, mockCachedSourceDao.insertIfAbsentCalls)
        assertEquals(1, mockCachedSourceDao.updateFromDiscoveryCalls)
    }

    @Test
    fun upsertFromDiscovery_existingSourceWithEndpointChange_preservesPreviewPath() = runTest {
        val previewPath = "/data/preview/source1.png"
        val existingRecord = CachedSourceRecord(
            cacheKey = "cache1",
            stableSourceId = "source1",
            lastObservedSourceId = "source1",
            displayName = "Test Source",
            endpointHost = "192.168.1.10",
            endpointPort = 5960,
            endpointKey = "192.168.1.10:5960",
            validationState = CachedSourceValidationState.AVAILABLE,
            firstCachedAtEpochMillis = 1000L,
            lastDiscoveredAtEpochMillis = 1000L,
            retainedPreviewImagePath = previewPath,
            lastPreviewCapturedAtEpochMillis = 800L,
            updatedAtEpochMillis = 1000L,
        )

        // Pre-populate with existing record
        mockCachedSourceDao.upsert(CachedSourceMapper().toEntity(existingRecord))

        // Rediscover with different endpoint
        val rediscoveredRecord = existingRecord.copy(
            endpointHost = "192.168.1.20",
            endpointPort = 5960,
            endpointKey = "192.168.1.20:5960",
            lastDiscoveredAtEpochMillis = 2000L,
            updatedAtEpochMillis = 2000L,
        )

        repository.upsertFromDiscovery(rediscoveredRecord)

        // Verify the updateFromDiscovery was called with the new endpoint
        assertEquals(1, mockCachedSourceDao.updateFromDiscoveryCalls)

        // Verify preview path is still preserved (not passed in updateFromDiscovery)
        val storedEntity = mockCachedSourceDao.getByKey("cache1")
        assertNotNull(storedEntity)
        assertEquals(previewPath, storedEntity?.retainedPreviewImagePath)
        assertEquals("192.168.1.20", storedEntity?.endpointHost)
    }

    @Test
    fun upsertDiscoveryAssociation_newAssociation_createsLink() = runTest {
        repository.upsertDiscoveryAssociation("cache1", "server1", 1000L)

        assertEquals(1, mockCrossRefDao.upsertCalls)
    }

    @Test
    fun upsertDiscoveryAssociation_existingAssociation_updatesTimestamp() = runTest {
        // First association
        repository.upsertDiscoveryAssociation("cache1", "server1", 1000L)
        assertEquals(1, mockCrossRefDao.upsertCalls)

        // Update existing association
        repository.upsertDiscoveryAssociation("cache1", "server1", 2000L)

        // Should call updateLastObserved, not upsert again
        assertEquals(1, mockCrossRefDao.updateLastObservedCalls)
    }

    @Test
    fun markValidationState_updatesStateWithTimestamps() = runTest {
        repository.markValidationState(
            cacheKey = "cache1",
            state = CachedSourceValidationState.VALIDATING,
            validationStartedAtEpochMillis = 1000L,
            validatedAtEpochMillis = null,
            availableAtEpochMillis = null,
        )

        assertEquals(1, mockCachedSourceDao.updateValidationStateCalls)
    }

    @Test
    fun upsertFromDiscovery_sameCanonicalIdentityDifferentEndpoint_updatesExistingRow_notDuplicate() = runTest {
        val first = CachedSourceRecord(
            cacheKey = "camera-a@10.0.0.1:5960",
            stableSourceId = "camera-a",
            lastObservedSourceId = "camera-a",
            displayName = "Camera A",
            endpointHost = "10.0.0.1",
            endpointPort = 5960,
            endpointKey = "10.0.0.1:5960",
            validationState = CachedSourceValidationState.AVAILABLE,
            firstCachedAtEpochMillis = 1_000L,
            lastDiscoveredAtEpochMillis = 1_000L,
            updatedAtEpochMillis = 1_000L,
        )
        val second = first.copy(
            cacheKey = "camera-a@10.0.0.2:5961",
            endpointHost = "10.0.0.2",
            endpointPort = 5961,
            endpointKey = "10.0.0.2:5961",
            lastDiscoveredAtEpochMillis = 2_000L,
            updatedAtEpochMillis = 2_000L,
        )

        repository.upsertFromDiscovery(first)
        repository.upsertFromDiscovery(second)

        val all = mockCachedSourceDao.observeAll().value
        assertEquals(1, all.size)
        assertEquals("camera-a@10.0.0.1:5960", all.single().cacheKey)
        assertEquals("10.0.0.2", all.single().endpointHost)
        assertEquals(5961, all.single().endpointPort)
    }

    @Test
    fun upsertFromDiscovery_persistsRowsAcrossRepositoryRelaunch_withLastSeenMetadata_FR007_FR010() = runTest {
        val sharedDao = MockCachedSourceDao()
        val sharedCrossRefDao = MockCachedSourceDiscoveryServerCrossRefDao()
        val firstRepository = CachedSourceRepositoryImpl(
            cachedSourceDao = sharedDao,
            crossRefDao = sharedCrossRefDao,
            mapper = CachedSourceMapper(),
        )
        val relaunchedRepository = CachedSourceRepositoryImpl(
            cachedSourceDao = sharedDao,
            crossRefDao = sharedCrossRefDao,
            mapper = CachedSourceMapper(),
        )

        val persisted = CachedSourceRecord(
            cacheKey = "cam-relaunch@10.0.1.10:5960",
            stableSourceId = "cam-relaunch",
            lastObservedSourceId = "cam-relaunch",
            displayName = "Relaunch Camera",
            endpointHost = "10.0.1.10",
            endpointPort = 5960,
            endpointKey = "10.0.1.10:5960",
            validationState = CachedSourceValidationState.AVAILABLE,
            lastAvailableAtEpochMillis = 1_700L,
            lastValidatedAtEpochMillis = 1_750L,
            firstCachedAtEpochMillis = 1_000L,
            lastDiscoveredAtEpochMillis = 1_750L,
            updatedAtEpochMillis = 1_750L,
        )

        firstRepository.upsertFromDiscovery(persisted)

        val directLookup = relaunchedRepository.getCachedSource("cam-relaunch@10.0.1.10:5960")
        assertNotNull(directLookup)
        assertEquals(1_750L, directLookup?.lastDiscoveredAtEpochMillis)
        assertEquals(1_750L, directLookup?.lastValidatedAtEpochMillis)

        val relaunchedFlowRows = relaunchedRepository.observeCachedSources().first()
        assertEquals(1, relaunchedFlowRows.size)
        assertEquals("cam-relaunch", relaunchedFlowRows.single().stableSourceId)
        assertEquals(1_700L, relaunchedFlowRows.single().lastAvailableAtEpochMillis)
    }

    @Test
    fun upsertFromDiscovery_sameCanonicalIdDifferentEndpoint_updatesSingleRow_preservesPreviewAndLastSeen_FR009_FR018() = runTest {
        val first = CachedSourceRecord(
            cacheKey = "camera-fr018@10.1.0.1:5960",
            stableSourceId = "camera-fr018",
            lastObservedSourceId = "camera-fr018",
            displayName = "Camera FR018",
            endpointHost = "10.1.0.1",
            endpointPort = 5960,
            endpointKey = "10.1.0.1:5960",
            validationState = CachedSourceValidationState.AVAILABLE,
            lastAvailableAtEpochMillis = 1_000L,
            lastValidatedAtEpochMillis = 1_000L,
            firstCachedAtEpochMillis = 1_000L,
            lastDiscoveredAtEpochMillis = 1_000L,
            retainedPreviewImagePath = "/data/preview/camera-fr018.png",
            lastPreviewCapturedAtEpochMillis = 990L,
            updatedAtEpochMillis = 1_000L,
        )
        val rediscovered = first.copy(
            cacheKey = "camera-fr018@10.1.0.20:5961",
            endpointHost = "10.1.0.20",
            endpointPort = 5961,
            endpointKey = "10.1.0.20:5961",
            displayName = "Camera FR018 Updated",
            lastValidatedAtEpochMillis = 2_000L,
            lastDiscoveredAtEpochMillis = 2_000L,
            updatedAtEpochMillis = 2_000L,
        )

        repository.upsertFromDiscovery(first)
        repository.upsertFromDiscovery(rediscovered)

        val rows = mockCachedSourceDao.observeAll().value
        assertEquals(1, rows.size)
        val row = rows.single()
        assertEquals("camera-fr018@10.1.0.1:5960", row.cacheKey)
        assertEquals("10.1.0.20", row.endpointHost)
        assertEquals(5961, row.endpointPort)
        assertEquals("/data/preview/camera-fr018.png", row.retainedPreviewImagePath)
        assertEquals(2_000L, row.lastValidatedAtEpochMillis)
        assertEquals(2_000L, row.lastDiscoveredAtEpochMillis)
    }

    @Test
    fun upsertFromDiscovery_whenRediscoveryOmitsLastValidated_preservesExistingLastSeenMetadata() = runTest {
        val first = CachedSourceRecord(
            cacheKey = "camera-stale@10.2.0.1:5960",
            stableSourceId = "camera-stale",
            lastObservedSourceId = "camera-stale",
            displayName = "Camera Stale",
            endpointHost = "10.2.0.1",
            endpointPort = 5960,
            endpointKey = "10.2.0.1:5960",
            validationState = CachedSourceValidationState.AVAILABLE,
            lastAvailableAtEpochMillis = 1_000L,
            lastValidatedAtEpochMillis = 1_000L,
            firstCachedAtEpochMillis = 1_000L,
            lastDiscoveredAtEpochMillis = 1_000L,
            updatedAtEpochMillis = 1_000L,
        )
        val rediscoveredWithoutValidation = first.copy(
            endpointHost = "10.2.0.2",
            endpointPort = 5961,
            endpointKey = "10.2.0.2:5961",
            lastValidatedAtEpochMillis = null,
            lastDiscoveredAtEpochMillis = 2_000L,
            updatedAtEpochMillis = 2_000L,
        )

        repository.upsertFromDiscovery(first)
        repository.upsertFromDiscovery(rediscoveredWithoutValidation)

        val stored = mockCachedSourceDao.getByKey("camera-stale@10.2.0.1:5960")
        assertNotNull(stored)
        assertEquals(1_000L, stored?.lastValidatedAtEpochMillis)
        assertEquals(2_000L, stored?.lastDiscoveredAtEpochMillis)
    }

    // Mock implementations for testing
    private class MockCachedSourceDao : CachedSourceDao {
        private val data = mutableMapOf<String, CachedSourceEntity>()
        private val observableRows = MutableStateFlow<List<CachedSourceEntity>>(emptyList())
        var insertIfAbsentCalls = 0
        var updateFromDiscoveryCalls = 0
        var updateValidationStateCalls = 0

        override fun observeAll() = observableRows

        override suspend fun getByKey(cacheKey: String) = data[cacheKey]

        override suspend fun upsert(entity: CachedSourceEntity) {
            data[entity.cacheKey] = entity
            observableRows.value = data.values.toList()
        }

        override suspend fun insertIfAbsent(entity: CachedSourceEntity) {
            insertIfAbsentCalls++
            if (!data.containsKey(entity.cacheKey)) {
                data[entity.cacheKey] = entity
                observableRows.value = data.values.toList()
            }
        }

        override suspend fun updateFromDiscovery(
            cacheKey: String,
            lastObservedSourceId: String?,
            displayName: String,
            endpointHost: String,
            endpointPort: Int,
            endpointKey: String,
            validationState: String,
            lastAvailableAtEpochMillis: Long?,
            lastValidatedAtEpochMillis: Long?,
            lastDiscoveredAtEpochMillis: Long,
            updatedAtEpochMillis: Long,
        ) {
            updateFromDiscoveryCalls++
            data[cacheKey]?.let {
                val updated = it.copy(
                    lastObservedSourceId = lastObservedSourceId,
                    displayName = displayName,
                    endpointHost = endpointHost,
                    endpointPort = endpointPort,
                    endpointKey = endpointKey,
                    validationState = validationState,
                    lastAvailableAtEpochMillis = lastAvailableAtEpochMillis ?: it.lastAvailableAtEpochMillis,
                    lastValidatedAtEpochMillis = lastValidatedAtEpochMillis,
                    lastDiscoveredAtEpochMillis = lastDiscoveredAtEpochMillis,
                    updatedAtEpochMillis = updatedAtEpochMillis,
                    // Note: retainedPreviewImagePath is preserved (not updated here)
                )
                data[cacheKey] = updated
                observableRows.value = data.values.toList()
            }
        }

        override suspend fun upsertAll(entities: List<CachedSourceEntity>) {}

        override suspend fun updateValidationState(
            cacheKey: String,
            validationState: String,
            startedAtEpochMillis: Long?,
            validatedAtEpochMillis: Long?,
            availableAtEpochMillis: Long?,
            updatedAtEpochMillis: Long,
        ) {
            updateValidationStateCalls++
            data[cacheKey]?.let {
                val updated = it.copy(
                    validationState = validationState,
                    lastValidationStartedAtEpochMillis = startedAtEpochMillis,
                    lastValidatedAtEpochMillis = validatedAtEpochMillis ?: it.lastValidatedAtEpochMillis,
                    lastAvailableAtEpochMillis = availableAtEpochMillis ?: it.lastAvailableAtEpochMillis,
                    updatedAtEpochMillis = updatedAtEpochMillis,
                )
                data[cacheKey] = updated
                observableRows.value = data.values.toList()
            }
        }
    }

    private class MockCachedSourceDiscoveryServerCrossRefDao : CachedSourceDiscoveryServerCrossRefDao {
        private val data = mutableListOf<CachedSourceDiscoveryServerCrossRefEntity>()
        var upsertCalls = 0
        var updateLastObservedCalls = 0

        override suspend fun getByCacheKey(cacheKey: String) =
            data.filter { it.cacheKey == cacheKey }

        override suspend fun upsert(entity: CachedSourceDiscoveryServerCrossRefEntity) {
            upsertCalls++
            data.add(entity)
        }

        override suspend fun updateLastObserved(
            cacheKey: String,
            discoveryServerId: String,
            lastObservedAtEpochMillis: Long,
        ) {
            updateLastObservedCalls++
            data.replaceAll { entity ->
                if (entity.cacheKey == cacheKey && entity.discoveryServerId == discoveryServerId) {
                    entity.copy(lastObservedAtEpochMillis = lastObservedAtEpochMillis)
                } else {
                    entity
                }
            }
        }
    }
}
