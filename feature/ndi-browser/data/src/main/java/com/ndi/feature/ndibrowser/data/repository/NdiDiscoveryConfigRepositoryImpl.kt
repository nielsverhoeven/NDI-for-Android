package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.NdiDiscoveryApplyResult
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.feature.ndibrowser.domain.repository.DiscoveryServerRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryConfigRepository
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import java.util.UUID

class NdiDiscoveryConfigRepositoryImpl(
    private val discoveryServerRepository: DiscoveryServerRepository,
) : NdiDiscoveryConfigRepository {

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    private val currentEndpoints = discoveryServerRepository.observeServers()
        .map(::toEnabledEndpoints)
        .stateIn(
            scope = scope,
            started = SharingStarted.Eagerly,
            initialValue = emptyList(),
        )

    override fun observeDiscoveryEndpoints(): Flow<List<NdiDiscoveryEndpoint>> = currentEndpoints

    override fun observeDiscoveryEndpoint(): Flow<NdiDiscoveryEndpoint?> = currentEndpoints.map { endpoints ->
        endpoints.firstOrNull()
    }

    override suspend fun getCurrentEndpoints(): List<NdiDiscoveryEndpoint> {
        val cached = currentEndpoints.value
        if (cached.isNotEmpty()) {
            return cached
        }

        return toEnabledEndpoints(discoveryServerRepository.observeServers().first())
    }

    override suspend fun getCurrentEndpoint(): NdiDiscoveryEndpoint? = getCurrentEndpoints().firstOrNull()

    override suspend fun applyDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?): NdiDiscoveryApplyResult {
        // Discovery endpoint can only be changed via Discovery Servers submenu mutations.
        // Ignore direct apply requests to keep discovery source-of-truth in repository entries.
        return NdiDiscoveryApplyResult(
            applyId = UUID.randomUUID().toString(),
            endpoint = currentEndpoints.value.firstOrNull(),
            interruptedActiveStream = false,
            fallbackTriggered = false,
            appliedAtEpochMillis = System.currentTimeMillis(),
        )
    }

    private fun toEnabledEndpoints(servers: List<com.ndi.core.model.DiscoveryServerEntry>): List<NdiDiscoveryEndpoint> {
        return servers
            .asSequence()
            .filter { it.enabled }
            .sortedBy { it.orderIndex }
            .map {
                NdiDiscoveryEndpoint(
                    host = it.hostOrIp,
                    port = it.port,
                    resolvedPort = it.port,
                    usesDefaultPort = false,
                )
            }
            .toList()
    }
}