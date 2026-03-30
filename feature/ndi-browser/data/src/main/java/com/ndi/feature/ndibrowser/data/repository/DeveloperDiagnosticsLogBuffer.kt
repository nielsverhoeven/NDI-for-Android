package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.NdiLogCategory
import com.ndi.core.model.NdiLogLevel
import com.ndi.core.model.NdiRedactedLogEntry
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow

class DeveloperDiagnosticsLogBuffer(
    private val maxEntries: Int = 5,
) {

    private val recentLogs = MutableStateFlow<List<NdiRedactedLogEntry>>(emptyList())

    fun observeRecentLogs(): Flow<List<NdiRedactedLogEntry>> = recentLogs.asStateFlow()

    fun appendLog(
        category: NdiLogCategory,
        level: NdiLogLevel,
        message: String,
    ) {
        val entry = NdiRedactedLogEntry(
            timestampEpochMillis = System.currentTimeMillis(),
            level = level,
            category = category,
            messageRedacted = message,
            redactionApplied = false,
        )
        recentLogs.value = (recentLogs.value + entry).takeLast(maxEntries)
    }
}