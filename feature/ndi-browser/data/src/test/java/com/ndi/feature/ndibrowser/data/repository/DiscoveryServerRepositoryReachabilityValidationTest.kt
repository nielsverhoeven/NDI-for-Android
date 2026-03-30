package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.DiscoveryServerDao
import com.ndi.core.database.DiscoveryServerCheckStatusDao
import com.ndi.core.database.DiscoveryServerCheckStatusEntity
import com.ndi.core.database.DiscoveryServerEntity
import com.ndi.core.model.DiscoverySelectionOutcome
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Test

class DiscoveryServerRepositoryReachabilityValidationTest {

    @Test
    fun addServer_savesServerEvenWhenEndpointUnreachable() = runTest {
        val dao = InMemoryDiscoveryServerDao()
        val checkDao = InMemoryDiscoveryServerCheckStatusDao()
        val repository = DiscoveryServerRepositoryImpl(
            discoveryServerDao = dao,
            discoveryServerCheckStatusDao = checkDao,
            discoveryServerReachabilityChecker = { _, _ -> false },
        )

        val result = repository.addServer("10.10.0.53", "5959")
        assertNotNull(result)  // server is saved regardless of reachability
    }

    @Test
    fun resolveActiveDiscoveryTarget_skipsUnreachable_andReturnsFirstReachable() = runTest {
        val dao = InMemoryDiscoveryServerDao()
        dao.insert(
            DiscoveryServerEntity(
                id = "first",
                hostOrIp = "192.168.2.23",
                port = 5959,
                enabled = true,
                orderIndex = 0,
                createdAtEpochMillis = 0L,
                updatedAtEpochMillis = 0L,
            ),
        )
        dao.insert(
            DiscoveryServerEntity(
                id = "second",
                hostOrIp = "10.10.0.53",
                port = 5959,
                enabled = true,
                orderIndex = 1,
                createdAtEpochMillis = 0L,
                updatedAtEpochMillis = 0L,
            ),
        )

        val repository = DiscoveryServerRepositoryImpl(
            discoveryServerDao = dao,
            discoveryServerCheckStatusDao = InMemoryDiscoveryServerCheckStatusDao(),
            discoveryServerReachabilityChecker = { host, _ -> host == "10.10.0.53" },
        )

        val result = repository.resolveActiveDiscoveryTarget()

        assertEquals(DiscoverySelectionOutcome.SUCCESS, result.result)
        assertEquals("second", result.selectedEntryId)
        assertEquals(listOf("first", "second"), result.attemptedEntryIds)
    }

    @Test
    fun resolveActiveDiscoveryTarget_returnsAllEnabledUnreachable_whenNoneReachable() = runTest {
        val dao = InMemoryDiscoveryServerDao()
        dao.insert(
            DiscoveryServerEntity(
                id = "only",
                hostOrIp = "192.168.2.23",
                port = 5959,
                enabled = true,
                orderIndex = 0,
                createdAtEpochMillis = 0L,
                updatedAtEpochMillis = 0L,
            ),
        )

        val repository = DiscoveryServerRepositoryImpl(
            discoveryServerDao = dao,
            discoveryServerCheckStatusDao = InMemoryDiscoveryServerCheckStatusDao(),
            discoveryServerReachabilityChecker = { _, _ -> false },
        )

        val result = repository.resolveActiveDiscoveryTarget()

        assertEquals(DiscoverySelectionOutcome.ALL_ENABLED_UNREACHABLE, result.result)
        assertEquals(null, result.selectedEntryId)
        assertEquals(listOf("only"), result.attemptedEntryIds)
    }
}

private class InMemoryDiscoveryServerDao : DiscoveryServerDao {
    private val entities = MutableStateFlow<List<DiscoveryServerEntity>>(emptyList())

    override fun observeAll(): Flow<List<DiscoveryServerEntity>> = entities

    override suspend fun getAll(): List<DiscoveryServerEntity> = entities.value.sortedBy { it.orderIndex }

    override suspend fun insert(entity: DiscoveryServerEntity) {
        entities.value = entities.value + entity
    }

    override suspend fun update(entity: DiscoveryServerEntity) {
        entities.value = entities.value.map { current ->
            if (current.id == entity.id) entity else current
        }
    }

    override suspend fun deleteById(id: String) {
        entities.value = entities.value.filterNot { it.id == id }
    }

    override suspend fun getById(id: String): DiscoveryServerEntity? = entities.value.firstOrNull { it.id == id }

    override suspend fun countDuplicates(hostOrIp: String, port: Int, excludeId: String): Int {
        return entities.value.count { it.hostOrIp == hostOrIp && it.port == port && it.id != excludeId }
    }

    override suspend fun getMaxOrderIndex(): Int? = entities.value.maxOfOrNull { it.orderIndex }
}

private class InMemoryDiscoveryServerCheckStatusDao : DiscoveryServerCheckStatusDao {
    private val statuses = MutableStateFlow<List<DiscoveryServerCheckStatusEntity>>(emptyList())

    override suspend fun getByServerId(serverId: String): DiscoveryServerCheckStatusEntity? =
        statuses.value.firstOrNull { it.serverId == serverId }

    override fun observeAll(): Flow<List<DiscoveryServerCheckStatusEntity>> = statuses

    override fun observeByServerId(serverId: String): Flow<DiscoveryServerCheckStatusEntity?> =
        statuses.map { list -> list.firstOrNull { it.serverId == serverId } }

    override suspend fun upsert(entity: DiscoveryServerCheckStatusEntity) {
        statuses.value = statuses.value.filterNot { it.serverId == entity.serverId } + entity
    }
}
