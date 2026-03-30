package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.DiscoveryServerDao
import com.ndi.core.database.DiscoveryServerEntity
import com.ndi.core.model.DEFAULT_DISCOVERY_SERVER_PORT
import com.ndi.core.model.DiscoverySelectionOutcome
import com.ndi.core.model.DiscoverySelectionResult
import com.ndi.core.model.DiscoveryServerEntry
import com.ndi.feature.ndibrowser.domain.repository.DiscoveryServerRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import java.util.UUID

class DiscoveryServerRepositoryImpl(
    private val discoveryServerDao: DiscoveryServerDao,
    private val discoveryServerReachabilityChecker: suspend (host: String, port: Int) -> Boolean = { _, _ -> true },
) : DiscoveryServerRepository {

    override fun observeServers(): Flow<List<DiscoveryServerEntry>> =
        discoveryServerDao.observeAll().map { entities ->
            entities.map { it.toEntry() }
        }

    override suspend fun addServer(hostOrIp: String, portInput: String): DiscoveryServerEntry {
        val normalized = hostOrIp.trim()
        validateHost(normalized)
        val port = resolvePort(portInput)
        validatePort(port)
        validateReachability(normalized, port)

        val duplicateCount = discoveryServerDao.countDuplicates(normalized, port, excludeId = "")
        if (duplicateCount > 0) {
            throw IllegalArgumentException("A server with host '$normalized' and port $port already exists.")
        }

        val nextIndex = (discoveryServerDao.getMaxOrderIndex() ?: -1) + 1
        val now = System.currentTimeMillis()
        val entity = DiscoveryServerEntity(
            id = UUID.randomUUID().toString(),
            hostOrIp = normalized,
            port = port,
            enabled = true,
            orderIndex = nextIndex,
            createdAtEpochMillis = now,
            updatedAtEpochMillis = now,
        )
        discoveryServerDao.insert(entity)
        return entity.toEntry()
    }

    override suspend fun updateServer(
        id: String,
        hostOrIp: String,
        portInput: String,
    ): DiscoveryServerEntry {
        val existing = discoveryServerDao.getById(id)
            ?: throw NoSuchElementException("No discovery server with id '$id'.")

        val normalized = hostOrIp.trim()
        validateHost(normalized)
        val port = resolvePort(portInput)
        validatePort(port)
        validateReachability(normalized, port)

        // Check for duplicates excluding this entry itself
        val duplicateCount = discoveryServerDao.countDuplicates(normalized, port, excludeId = id)
        if (duplicateCount > 0) {
            throw IllegalArgumentException("A server with host '$normalized' and port $port already exists.")
        }

        val updated = existing.copy(
            hostOrIp = normalized,
            port = port,
            updatedAtEpochMillis = System.currentTimeMillis(),
        )
        discoveryServerDao.update(updated)
        return updated.toEntry()
    }

    override suspend fun removeServer(id: String) {
        discoveryServerDao.deleteById(id)
    }

    override suspend fun setServerEnabled(id: String, enabled: Boolean): DiscoveryServerEntry {
        val existing = discoveryServerDao.getById(id)
            ?: throw NoSuchElementException("No discovery server with id '$id'.")
        val updated = existing.copy(
            enabled = enabled,
            updatedAtEpochMillis = System.currentTimeMillis(),
        )
        discoveryServerDao.update(updated)
        return updated.toEntry()
    }

    override suspend fun reorderServers(idsInOrder: List<String>): List<DiscoveryServerEntry> {
        val all = discoveryServerDao.getAll().associateBy { it.id }
        val now = System.currentTimeMillis()
        idsInOrder.forEachIndexed { index, id ->
            val entity = all[id] ?: return@forEachIndexed
            if (entity.orderIndex != index) {
                discoveryServerDao.update(entity.copy(orderIndex = index, updatedAtEpochMillis = now))
            }
        }
        return discoveryServerDao.getAll().map { it.toEntry() }
    }

    override suspend fun resolveActiveDiscoveryTarget(): DiscoverySelectionResult {
        val allEntries = discoveryServerDao.getAll().map { it.toEntry() }
        val enabled = allEntries.filter { it.enabled }.sortedBy { it.orderIndex }

        if (enabled.isEmpty()) {
            return DiscoverySelectionResult(
                attemptedEntryIds = emptyList(),
                selectedEntryId = null,
                result = DiscoverySelectionOutcome.NO_ENABLED_SERVERS,
                errorMessage = "No discovery servers are enabled. Enable at least one server in Settings.",
            )
        }

        val attempted = mutableListOf<String>()
        for (entry in enabled) {
            attempted += entry.id
            val isReachable = runCatching {
                discoveryServerReachabilityChecker(entry.hostOrIp, entry.port)
            }.getOrDefault(false)

            if (isReachable) {
                return DiscoverySelectionResult(
                    attemptedEntryIds = attempted,
                    selectedEntryId = entry.id,
                    result = DiscoverySelectionOutcome.SUCCESS,
                    errorMessage = null,
                )
            }
        }

        return DiscoverySelectionResult(
            attemptedEntryIds = attempted,
            selectedEntryId = null,
            result = DiscoverySelectionOutcome.ALL_ENABLED_UNREACHABLE,
            errorMessage = "All enabled discovery servers are unreachable. Verify host, port, and network access.",
        )
    }

    // ---- Validation helpers ----

    private fun validateHost(host: String) {
        if (host.isBlank()) {
            throw IllegalArgumentException("Hostname or IP address is required.")
        }
        if (host.length > 253) {
            throw IllegalArgumentException("Hostname is too long (max 253 characters).")
        }
    }

    private fun resolvePort(portInput: String): Int {
        if (portInput.isBlank()) return DEFAULT_DISCOVERY_SERVER_PORT
        return portInput.trim().toIntOrNull()
            ?: throw IllegalArgumentException("Port must be a valid number.")
    }

    private fun validatePort(port: Int) {
        if (port !in 1..65535) {
            throw IllegalArgumentException("Port must be between 1 and 65535.")
        }
    }

    private suspend fun validateReachability(host: String, port: Int) {
        val reachable = runCatching {
            discoveryServerReachabilityChecker(host, port)
        }.getOrDefault(false)

        if (!reachable) {
            throw IllegalArgumentException(
                "Cannot reach discovery server '$host:$port'. Verify the endpoint and network connectivity from this device.",
            )
        }
    }
}

// ---- Mapping extension ----

private fun DiscoveryServerEntity.toEntry() = DiscoveryServerEntry(
    id = id,
    hostOrIp = hostOrIp,
    port = port,
    enabled = enabled,
    orderIndex = orderIndex,
    createdAtEpochMillis = createdAtEpochMillis,
    updatedAtEpochMillis = updatedAtEpochMillis,
)
