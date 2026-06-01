package com.ndi.feature.ndibrowser.data.repository

import android.util.Log
import com.ndi.core.database.DiscoveryServerCheckStatusDao
import com.ndi.core.database.DiscoveryServerCheckStatusEntity
import com.ndi.core.database.DiscoveryServerDao
import com.ndi.core.database.DiscoveryServerEntity
import com.ndi.core.model.DEFAULT_DISCOVERY_SERVER_PORT
import com.ndi.core.model.DiscoveryCheckOutcome
import com.ndi.core.model.DiscoveryCheckType
import com.ndi.core.model.DiscoveryFailureCategory
import com.ndi.core.model.DiscoverySelectionOutcome
import com.ndi.core.model.DiscoverySelectionResult
import com.ndi.core.model.DiscoveryServerCheckStatus
import com.ndi.core.model.DiscoveryServerEntry
import com.ndi.feature.ndibrowser.data.util.CorrelationId
import com.ndi.feature.ndibrowser.domain.repository.DiscoveryServerRepository
import com.ndi.sdkbridge.NdiDiscoveryBridge
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import java.util.UUID

class DiscoveryServerRepositoryImpl(
    private val discoveryServerDao: DiscoveryServerDao,
    private val discoveryServerCheckStatusDao: DiscoveryServerCheckStatusDao,
    private val discoveryBridge: NdiDiscoveryBridge? = null,
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
        val correlationId = CorrelationId.generate()
        Log.d("NdiDiscovery", "discovery_server_add_started host=$normalized port=$port correlationId=$correlationId")

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
        Log.d("NdiDiscovery", "discovery_server_add_completed serverId=${entity.id} correlationId=$correlationId")

        // Perform check after registration (FR-010: server stays even if check fails)
        runCatching { performDiscoveryServerCheck(entity.id, correlationId) }

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
                discoveryBridge?.isDiscoveryServerReachable(entry.hostOrIp, entry.port)
                    ?: discoveryServerReachabilityChecker(entry.hostOrIp, entry.port)
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

    override suspend fun performDiscoveryServerCheck(
        serverId: String,
        correlationId: String,
    ): DiscoveryServerCheckStatus {
        val entry = discoveryServerDao.getById(serverId)
            ?: throw NoSuchElementException("No discovery server with id '$serverId'.")
        Log.d("NdiDiscovery", "discovery_server_check_started serverId=$serverId correlationId=$correlationId")
        val now = System.currentTimeMillis()
        val (success, failureCategoryStr, failureMessage) = runCatching {
            discoveryBridge?.performDiscoveryCheck(entry.hostOrIp, entry.port, correlationId)
                ?: if (discoveryServerReachabilityChecker(entry.hostOrIp, entry.port)) {
                    Triple(true, "NONE", null)
                } else {
                    Triple(false, "ENDPOINT_UNREACHABLE", "Cannot reach ${entry.hostOrIp}:${entry.port}")
                }
        }.getOrElse { e ->
            val category = when {
                e is java.net.SocketTimeoutException -> "TIMEOUT"
                else -> "UNKNOWN"
            }
            Triple(false, category, e.message ?: "Unknown error during discovery check")
        }
        val statusEntity = DiscoveryServerCheckStatusEntity(
            serverId = serverId,
            checkType = DiscoveryCheckType.ADD_VALIDATION.name,
            outcome = if (success) DiscoveryCheckOutcome.SUCCESS.name else DiscoveryCheckOutcome.FAILURE.name,
            checkedAtEpochMillis = now,
            failureCategory = failureCategoryStr,
            failureMessage = failureMessage,
            correlationId = correlationId,
        )
        discoveryServerCheckStatusDao.upsert(statusEntity)
        Log.d("NdiDiscovery", "discovery_server_check_completed serverId=$serverId correlationId=$correlationId outcome=${statusEntity.outcome}")
        return statusEntity.toCheckStatus()
    }

    override suspend fun recheckServer(
        serverId: String,
        correlationId: String,
    ): DiscoveryServerCheckStatus {
        discoveryServerDao.getById(serverId)
            ?: throw NoSuchElementException("No discovery server with id '$serverId'.")
        Log.d("NdiDiscovery", "discovery_server_recheck_started serverId=$serverId correlationId=$correlationId")
        val checkResult = performDiscoveryServerCheck(serverId, correlationId)
        // Overwrite checkType to MANUAL_RECHECK
        val recheckEntity = DiscoveryServerCheckStatusEntity(
            serverId = serverId,
            checkType = DiscoveryCheckType.MANUAL_RECHECK.name,
            outcome = checkResult.outcome.name,
            checkedAtEpochMillis = checkResult.checkedAtEpochMillis,
            failureCategory = checkResult.failureCategory.name,
            failureMessage = checkResult.failureMessage,
            correlationId = correlationId,
        )
        discoveryServerCheckStatusDao.upsert(recheckEntity)
        Log.d("NdiDiscovery", "discovery_server_recheck_completed serverId=$serverId correlationId=$correlationId outcome=${checkResult.outcome}")
        return recheckEntity.toCheckStatus()
    }

    override suspend fun getServerCheckStatus(serverId: String): DiscoveryServerCheckStatus? =
        discoveryServerCheckStatusDao.getByServerId(serverId)?.toCheckStatus()

    override fun observeServerCheckStatus(serverId: String): Flow<DiscoveryServerCheckStatus?> =
        discoveryServerCheckStatusDao.observeByServerId(serverId).map { it?.toCheckStatus() }
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

private fun DiscoveryServerCheckStatusEntity.toCheckStatus() = DiscoveryServerCheckStatus(
    serverId = serverId,
    checkType = DiscoveryCheckType.valueOf(checkType),
    outcome = DiscoveryCheckOutcome.valueOf(outcome),
    checkedAtEpochMillis = checkedAtEpochMillis,
    failureCategory = DiscoveryFailureCategory.valueOf(failureCategory),
    failureMessage = failureMessage,
    correlationId = correlationId,
)
