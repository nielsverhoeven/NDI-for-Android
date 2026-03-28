package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.DEFAULT_DISCOVERY_SERVER_PORT
import com.ndi.core.model.DiscoverySelectionOutcome
import com.ndi.core.model.DiscoverySelectionResult
import com.ndi.core.model.DiscoveryServerEntry
import com.ndi.feature.ndibrowser.domain.repository.DiscoveryServerRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.map
import java.util.UUID

/**
 * In-memory fake implementation of DiscoveryServerRepository for unit tests.
 */
class FakeDiscoveryServerRepository : DiscoveryServerRepository {

    private val _entries = MutableStateFlow<List<DiscoveryServerEntry>>(emptyList())

    fun seedServer(hostOrIp: String, port: Int, enabled: Boolean = true): DiscoveryServerEntry {
        val entry = DiscoveryServerEntry(
            id = UUID.randomUUID().toString(),
            hostOrIp = hostOrIp,
            port = port,
            enabled = enabled,
            orderIndex = _entries.value.size,
            createdAtEpochMillis = 1000L,
            updatedAtEpochMillis = 1000L,
        )
        _entries.value = _entries.value + entry
        return entry
    }

    override fun observeServers(): Flow<List<DiscoveryServerEntry>> = _entries

    override suspend fun addServer(hostOrIp: String, portInput: String): DiscoveryServerEntry {
        val normalized = hostOrIp.trim()
        if (normalized.isBlank()) throw IllegalArgumentException("Hostname or IP address is required.")
        val port = if (portInput.isBlank()) DEFAULT_DISCOVERY_SERVER_PORT else {
            val parsed = portInput.trim().toIntOrNull()
                ?: throw IllegalArgumentException("Port must be a valid number.")
            if (parsed !in 1..65535) throw IllegalArgumentException("Port out of range.")
            parsed
        }
        val duplicate = _entries.value.any { it.hostOrIp == normalized && it.port == port }
        if (duplicate) throw IllegalArgumentException("A server with host '$normalized' and port $port already exists.")

        val entry = DiscoveryServerEntry(
            id = UUID.randomUUID().toString(),
            hostOrIp = normalized,
            port = port,
            enabled = true,
            orderIndex = _entries.value.size,
            createdAtEpochMillis = System.currentTimeMillis(),
            updatedAtEpochMillis = System.currentTimeMillis(),
        )
        _entries.value = _entries.value + entry
        return entry
    }

    override suspend fun updateServer(
        id: String,
        hostOrIp: String,
        portInput: String,
    ): DiscoveryServerEntry {
        val existing = _entries.value.firstOrNull { it.id == id }
            ?: throw NoSuchElementException("No server with id '$id'.")
        val normalized = hostOrIp.trim()
        if (normalized.isBlank()) throw IllegalArgumentException("Hostname or IP address is required.")
        val port = if (portInput.isBlank()) DEFAULT_DISCOVERY_SERVER_PORT else {
            portInput.trim().toIntOrNull()
                ?: throw IllegalArgumentException("Port must be a valid number.")
        }
        val duplicate = _entries.value.any { it.hostOrIp == normalized && it.port == port && it.id != id }
        if (duplicate) throw IllegalArgumentException("Duplicate server.")

        val updated = existing.copy(
            hostOrIp = normalized,
            port = port,
            updatedAtEpochMillis = System.currentTimeMillis(),
        )
        _entries.value = _entries.value.map { if (it.id == id) updated else it }
        return updated
    }

    override suspend fun removeServer(id: String) {
        _entries.value = _entries.value.filter { it.id != id }
    }

    override suspend fun setServerEnabled(id: String, enabled: Boolean): DiscoveryServerEntry {
        val existing = _entries.value.firstOrNull { it.id == id }
            ?: throw NoSuchElementException("No server with id '$id'.")
        val updated = existing.copy(enabled = enabled)
        _entries.value = _entries.value.map { if (it.id == id) updated else it }
        return updated
    }

    override suspend fun reorderServers(idsInOrder: List<String>): List<DiscoveryServerEntry> {
        val map = _entries.value.associateBy { it.id }
        val reordered = idsInOrder.mapIndexed { index, id ->
            map[id]?.copy(orderIndex = index)
        }.filterNotNull()
        _entries.value = reordered
        return reordered
    }

    override suspend fun resolveActiveDiscoveryTarget(): DiscoverySelectionResult {
        val enabled = _entries.value.filter { it.enabled }.sortedBy { it.orderIndex }
        return if (enabled.isEmpty()) {
            DiscoverySelectionResult(
                attemptedEntryIds = emptyList(),
                selectedEntryId = null,
                result = DiscoverySelectionOutcome.NO_ENABLED_SERVERS,
                errorMessage = "No enabled servers.",
            )
        } else {
            DiscoverySelectionResult(
                attemptedEntryIds = listOf(enabled.first().id),
                selectedEntryId = enabled.first().id,
                result = DiscoverySelectionOutcome.SUCCESS,
                errorMessage = null,
            )
        }
    }
}
