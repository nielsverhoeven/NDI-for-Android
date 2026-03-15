package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.UserSelectionDao
import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.NdiSource
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.sdkbridge.NdiDiscoveryBridge
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.util.UUID

class NdiDiscoveryRepositoryImpl(
    private val bridge: NdiDiscoveryBridge,
    private val userSelectionDao: UserSelectionDao,
    private val scope: CoroutineScope = CoroutineScope(SupervisorJob() + Dispatchers.IO),
) : NdiDiscoveryRepository {

    private val discoveryState = MutableStateFlow(emptySnapshot())
    private var refreshJob: Job? = null

    override suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot {
        val startedAt = System.currentTimeMillis()
        discoveryState.value = discoveryState.value.copy(
            startedAtEpochMillis = startedAt,
            status = DiscoveryStatus.IN_PROGRESS,
        )

        return runCatching {
            val persistedSelection = userSelectionDao.getSelection()?.lastSelectedSourceId
            val sources = bridge.discoverSources().sortedByDescending { it.sourceId == persistedSelection }
            val completedAt = System.currentTimeMillis()
            val status = if (sources.isEmpty()) DiscoveryStatus.EMPTY else DiscoveryStatus.SUCCESS
            DiscoverySnapshot(
                snapshotId = UUID.randomUUID().toString(),
                startedAtEpochMillis = startedAt,
                completedAtEpochMillis = completedAt,
                status = status,
                sourceCount = sources.size,
                sources = sources,
            )
        }.getOrElse { error ->
            DiscoverySnapshot(
                snapshotId = UUID.randomUUID().toString(),
                startedAtEpochMillis = startedAt,
                completedAtEpochMillis = System.currentTimeMillis(),
                status = DiscoveryStatus.FAILURE,
                sourceCount = 0,
                sources = emptyList(),
                errorCode = trigger.name,
                errorMessage = error.message,
            )
        }.also { snapshot ->
            discoveryState.value = snapshot
        }
    }

    override fun observeDiscoveryState(): Flow<DiscoverySnapshot> = discoveryState.asStateFlow()

    override fun startForegroundAutoRefresh(intervalSeconds: Int) {
        if (refreshJob?.isActive == true) {
            return
        }

        refreshJob = scope.launch {
            while (true) {
                discoverSources(DiscoveryTrigger.FOREGROUND_TICK)
                delay(intervalSeconds * 1000L)
            }
        }
    }

    override fun stopForegroundAutoRefresh() {
        refreshJob?.cancel()
        refreshJob = null
    }

    private fun emptySnapshot(): DiscoverySnapshot {
        return DiscoverySnapshot(
            snapshotId = UUID.randomUUID().toString(),
            startedAtEpochMillis = 0L,
            completedAtEpochMillis = 0L,
            status = DiscoveryStatus.EMPTY,
            sourceCount = 0,
            sources = emptyList(),
        )
    }
}
