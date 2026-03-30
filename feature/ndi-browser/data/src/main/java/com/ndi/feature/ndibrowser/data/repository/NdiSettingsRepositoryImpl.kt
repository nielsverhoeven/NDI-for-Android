package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.DiscoveryServerDao
import com.ndi.core.database.DiscoveryServerEntity
import com.ndi.core.database.SettingsPreferenceDao
import com.ndi.core.database.SettingsPreferenceEntity
import com.ndi.core.model.DEFAULT_DISCOVERY_SERVER_PORT
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.core.model.NdiThemeMode
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.launch
import java.util.UUID

class NdiSettingsRepositoryImpl(
    private val settingsDao: SettingsPreferenceDao,
    /** Optional: provide to enable one-time legacy single-endpoint → discovery-server migration on init. */
    private val discoveryServerDao: DiscoveryServerDao? = null,
    private val scope: CoroutineScope = CoroutineScope(Dispatchers.IO + SupervisorJob()),
) : NdiSettingsRepository {
    private val _settings = MutableStateFlow<NdiSettingsSnapshot?>(null)

    init {
        scope.launch {
            val snapshot = settingsDao.get()?.toSnapshot() ?: defaultSnapshot()
            _settings.value = snapshot
            migrateLegacyEndpointIfNeeded(snapshot)
        }
    }

    override suspend fun getSettings(): NdiSettingsSnapshot {
        return settingsDao.get()?.toSnapshot() ?: defaultSnapshot()
    }

    override suspend fun saveSettings(snapshot: NdiSettingsSnapshot) {
        val normalized = snapshot.copy(
            discoveryServerInput = snapshot.discoveryServerInput?.trim()?.takeIf { it.isNotBlank() },
        )
        settingsDao.upsert(normalized.toEntity())
        _settings.value = normalized
    }

    override fun observeSettings(): Flow<NdiSettingsSnapshot> = _settings.filterNotNull()

    /**
     * One-time migration: if the legacy single-endpoint is configured and no discovery server
     * entries exist yet, import the legacy endpoint as the first enabled entry (index 0).
     * This preserves backward compatibility per spec 018 requirements.
     */
    private suspend fun migrateLegacyEndpointIfNeeded(snapshot: NdiSettingsSnapshot) {
        val dao = discoveryServerDao ?: return
        val legacyInput = snapshot.discoveryServerInput?.trim()?.takeIf { it.isNotBlank() } ?: return

        if (dao.getAll().isNotEmpty()) {
            clearLegacyDiscoveryInput(snapshot)
            return
        }

        val parsed = NdiDiscoveryEndpoint.parse(legacyInput)
        if (parsed != null) {
            val now = System.currentTimeMillis()
            dao.insert(
                DiscoveryServerEntity(
                    id = UUID.randomUUID().toString(),
                    hostOrIp = parsed.host,
                    port = parsed.resolvedPort.takeIf { it > 0 } ?: DEFAULT_DISCOVERY_SERVER_PORT,
                    enabled = true,
                    orderIndex = 0,
                    createdAtEpochMillis = now,
                    updatedAtEpochMillis = now,
                ),
            )
        }
        clearLegacyDiscoveryInput(snapshot)
    }

    private suspend fun clearLegacyDiscoveryInput(snapshot: NdiSettingsSnapshot) {
        if (snapshot.discoveryServerInput.isNullOrBlank()) return

        val cleared = snapshot.copy(
            discoveryServerInput = null,
            updatedAtEpochMillis = System.currentTimeMillis(),
        )
        settingsDao.upsert(cleared.toEntity())
        _settings.value = cleared
    }

    private fun defaultSnapshot() = NdiSettingsSnapshot(
        discoveryServerInput = null,
        developerModeEnabled = false,
        themeMode = NdiThemeMode.SYSTEM,
        accentColorId = "accent_teal",
        updatedAtEpochMillis = 0L,
    )
}

private fun SettingsPreferenceEntity.toSnapshot(): NdiSettingsSnapshot = NdiSettingsSnapshot(
    discoveryServerInput = discoveryServerInput,
    developerModeEnabled = developerModeEnabled,
    themeMode = runCatching { NdiThemeMode.valueOf(themeMode) }.getOrDefault(NdiThemeMode.SYSTEM),
    accentColorId = accentColorId,
    updatedAtEpochMillis = updatedAtEpochMillis,
)

private fun NdiSettingsSnapshot.toEntity(): SettingsPreferenceEntity = SettingsPreferenceEntity(
    id = 1,
    discoveryServerInput = discoveryServerInput,
    developerModeEnabled = developerModeEnabled,
    themeMode = themeMode.name,
    accentColorId = accentColorId,
    updatedAtEpochMillis = updatedAtEpochMillis,
)

