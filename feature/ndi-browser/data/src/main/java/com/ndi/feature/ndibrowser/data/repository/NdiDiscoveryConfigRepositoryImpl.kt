package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.NdiDiscoveryApplyResult
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.feature.ndibrowser.domain.repository.DiscoveryServerRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryConfigRepository
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.util.UUID

class NdiDiscoveryConfigRepositoryImpl(
    private val discoveryServerRepository: DiscoveryServerRepository,
) : NdiDiscoveryConfigRepository {

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private val _currentEndpoint = MutableStateFlow<NdiDiscoveryEndpoint?>(null)

    init {
        scope.launch {
            discoveryServerRepository.observeServers().collect { servers ->
                val endpoint = servers
                    .asSequence()
                    .filter { it.enabled }
                    .sortedBy { it.orderIndex }
                    .firstOrNull()
                    ?.let {
                        NdiDiscoveryEndpoint(
                            host = it.hostOrIp,
                            port = it.port,
                            resolvedPort = it.port,
                            usesDefaultPort = false,
                        )
                    }
                _currentEndpoint.value = endpoint
            }
        }
    }

    override fun observeDiscoveryEndpoint(): Flow<NdiDiscoveryEndpoint?> = _currentEndpoint.asStateFlow()

    override suspend fun getCurrentEndpoint(): NdiDiscoveryEndpoint? = _currentEndpoint.value

    override suspend fun applyDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?): NdiDiscoveryApplyResult {
        // Discovery endpoint can only be changed via Discovery Servers submenu mutations.
        // Ignore direct apply requests to keep discovery source-of-truth in repository entries.
        return NdiDiscoveryApplyResult(
            applyId = UUID.randomUUID().toString(),
            endpoint = _currentEndpoint.value,
            interruptedActiveStream = false,
            fallbackTriggered = false,
            appliedAtEpochMillis = System.currentTimeMillis(),
        )
    }
}