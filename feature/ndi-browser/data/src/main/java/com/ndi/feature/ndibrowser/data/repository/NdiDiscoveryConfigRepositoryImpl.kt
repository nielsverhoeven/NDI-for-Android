package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.NdiDiscoveryApplyResult
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryConfigRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.util.UUID

class NdiDiscoveryConfigRepositoryImpl(
    private val settingsRepository: NdiSettingsRepository,
) : NdiDiscoveryConfigRepository {

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
    private val _currentEndpoint = MutableStateFlow<NdiDiscoveryEndpoint?>(null)

    init {
        scope.launch {
            settingsRepository.observeSettings().collect { snapshot ->
                _currentEndpoint.value = NdiDiscoveryEndpoint.parse(snapshot.discoveryServerInput)
            }
        }
    }

    override fun observeDiscoveryEndpoint(): Flow<NdiDiscoveryEndpoint?> = _currentEndpoint.asStateFlow()

    override suspend fun getCurrentEndpoint(): NdiDiscoveryEndpoint? = _currentEndpoint.value

    override suspend fun applyDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?): NdiDiscoveryApplyResult {
        val currentSettings = settingsRepository.getSettings()
        settingsRepository.saveSettings(
            currentSettings.copy(
                discoveryServerInput = endpoint?.let(::toRawInput),
                updatedAtEpochMillis = System.currentTimeMillis(),
            ),
        )
        _currentEndpoint.value = endpoint
        return NdiDiscoveryApplyResult(
            applyId = UUID.randomUUID().toString(),
            endpoint = endpoint,
            interruptedActiveStream = false,
            fallbackTriggered = false,
            appliedAtEpochMillis = System.currentTimeMillis(),
        )
    }

    private fun toRawInput(endpoint: NdiDiscoveryEndpoint): String {
        val host = if (endpoint.host.contains(':')) "[${endpoint.host}]" else endpoint.host
        return endpoint.port?.let { "$host:$it" } ?: host
    }
}