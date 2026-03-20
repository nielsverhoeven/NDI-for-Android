package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.NdiDeveloperOverlayState
import com.ndi.core.model.NdiLogCategory
import com.ndi.core.model.NdiLogLevel
import com.ndi.core.model.NdiOverlayMode
import com.ndi.core.model.NdiRedactedLogEntry
import com.ndi.feature.ndibrowser.domain.repository.DeveloperDiagnosticsRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow

class DeveloperDiagnosticsRepositoryImpl : DeveloperDiagnosticsRepository {

    private val _overlayState = MutableStateFlow(defaultOverlayState())
    private val _recentLogs = MutableStateFlow<List<NdiRedactedLogEntry>>(emptyList())

    override fun observeOverlayState(): Flow<NdiDeveloperOverlayState> = _overlayState.asStateFlow()

    override fun observeRecentLogs(): Flow<List<NdiRedactedLogEntry>> = _recentLogs.asStateFlow()

    private fun defaultOverlayState(): NdiDeveloperOverlayState = NdiDeveloperOverlayState(
        visible = false,
        mode = NdiOverlayMode.DISABLED,
        streamDirectionLabel = "",
        streamStatusLabel = "",
        streamSourceLabel = null,
        warningMessage = null,
        recentLogs = emptyList(),
        updatedAtEpochMillis = System.currentTimeMillis(),
    )
}
