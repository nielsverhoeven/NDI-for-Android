package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow

internal class InMemorySettingsRepository(
    initialSnapshot: NdiSettingsSnapshot = NdiSettingsSnapshot(
        discoveryServerInput = null,
        developerModeEnabled = false,
        updatedAtEpochMillis = 0L,
    ),
) : NdiSettingsRepository {
    private val state = MutableStateFlow(initialSnapshot)

    val savedSnapshots = mutableListOf<NdiSettingsSnapshot>()

    override suspend fun getSettings(): NdiSettingsSnapshot = state.value

    override suspend fun saveSettings(snapshot: NdiSettingsSnapshot) {
        savedSnapshots += snapshot
        state.value = snapshot
    }

    override fun observeSettings(): Flow<NdiSettingsSnapshot> = state
}
