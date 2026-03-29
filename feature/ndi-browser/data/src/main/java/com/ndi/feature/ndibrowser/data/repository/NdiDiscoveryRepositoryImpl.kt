package com.ndi.feature.ndibrowser.data.repository

import android.util.Log
import com.ndi.core.database.UserSelectionDao
import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.NdiLogCategory
import com.ndi.core.model.NdiLogLevel
import com.ndi.feature.ndibrowser.data.AvailabilityDebounceTracker
import com.ndi.feature.ndibrowser.data.DiscoveryRefreshCoordinator
import com.ndi.feature.ndibrowser.data.mapper.NdiSourceMapper
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryConfigRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.SourceAvailabilityStatus
import com.ndi.sdkbridge.NdiDiscoveryBridge
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.withContext
import java.util.UUID

class NdiDiscoveryRepositoryImpl(
    private val bridge: NdiDiscoveryBridge,
    private val userSelectionDao: UserSelectionDao,
    private val discoveryConfigRepository: NdiDiscoveryConfigRepository,
    private val scope: CoroutineScope = CoroutineScope(SupervisorJob() + Dispatchers.IO),
    private val sourceMapper: NdiSourceMapper = NdiSourceMapper(),
    private val refreshCoordinator: DiscoveryRefreshCoordinator = DiscoveryRefreshCoordinator(scope),
    private val availabilityTracker: AvailabilityDebounceTracker = AvailabilityDebounceTracker(),
    private val diagnosticsLogBuffer: DeveloperDiagnosticsLogBuffer? = null,
) : NdiDiscoveryRepository {

    private companion object {
        const val LOCAL_SCREEN_SOURCE_ID = "device-screen:local"
        const val LOCAL_SCREEN_DISPLAY_NAME = "This Device Screen"
        const val TAG = "NdiDiscoveryRepo"
    }

    private val discoveryState = MutableStateFlow(emptySnapshot())
    private val availabilityHistory = MutableStateFlow<Map<String, SourceAvailabilityStatus>>(emptyMap())

    override suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot {
        val startedAt = System.currentTimeMillis()
        discoveryState.value = discoveryState.value.copy(
            startedAtEpochMillis = startedAt,
            status = DiscoveryStatus.IN_PROGRESS,
        )

        return runCatching {
            val endpoints = discoveryConfigRepository.getCurrentEndpoints()
            runCatching { Log.d(TAG, "Discovery trigger=$trigger, endpoints=$endpoints") }

            // Set ALL configured endpoints at once. The NDI SDK (v5+) supports a
            // comma-separated list in NDI_DISCOVERY_SERVER and contacts all servers
            // simultaneously via a single finder instance. Calling setDiscoveryEndpoints
            // with one entry at a time inside a loop would trigger NDI reinit per
            // iteration, discarding the accumulated source list on every tick.
            withContext(Dispatchers.IO) {
                bridge.setDiscoveryEndpoints(endpoints)
            }

            diagnosticsLogBuffer?.appendLog(
                category = NdiLogCategory.DISCOVERY,
                level = NdiLogLevel.INFO,
                message = if (endpoints.isEmpty()) {
                    "discovery using default network discovery (no custom servers configured)"
                } else {
                    "discovery via ${endpoints.joinToString { "${it.host}:${it.resolvedPort}" }}"
                },
            )

            val discovered = withContext(Dispatchers.IO) {
                sourceMapper.map(bridge.discoverSources())
            }

            diagnosticsLogBuffer?.appendLog(
                category = NdiLogCategory.DISCOVERY,
                level = NdiLogLevel.INFO,
                message = "discovery returned ${discovered.size} sources",
            )

            userSelectionDao.getSelection()
            val sources = discovered
                .distinctBy { it.sourceId }  // Deduplicate by canonical source ID
                .toMutableList().apply {
                add(
                    0,
                    com.ndi.core.model.NdiSource(
                        sourceId = LOCAL_SCREEN_SOURCE_ID,
                        displayName = LOCAL_SCREEN_DISPLAY_NAME,
                        endpointAddress = null,
                        isReachable = true,
                        lastSeenAtEpochMillis = System.currentTimeMillis(),
                    ),
                )
            }
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
        }.onFailure { e ->
            // Restore cooperative cancellation — runCatching catches CancellationException,
            // which would prevent the coroutine from honouring its cancellation signal.
            if (e is CancellationException) throw e
        }.getOrElse { error ->
            diagnosticsLogBuffer?.appendLog(
                category = NdiLogCategory.DISCOVERY,
                level = NdiLogLevel.ERROR,
                message = "discovery failed: ${error.message ?: "unknown error"}",
            )
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
            if (snapshot.status != DiscoveryStatus.FAILURE) {
                diagnosticsLogBuffer?.appendLog(
                    category = NdiLogCategory.DISCOVERY,
                    level = NdiLogLevel.INFO,
                    message = "discovery ${snapshot.status.name.lowercase()} with ${snapshot.sourceCount} sources",
                )
            }
            discoveryState.value = snapshot
            // Update availability history based on the new snapshot
            updateAvailabilityHistory(snapshot)
        }
    }

    private fun updateAvailabilityHistory(snapshot: DiscoverySnapshot) {
        val nowEpochMillis = System.currentTimeMillis()
        val seenSourceIds = snapshot.sources.map { it.sourceId }.toSet()
        val currentHistory = availabilityHistory.value.toMutableMap()

        // Update or initialize status for seen sources
        for (source in snapshot.sources) {
            val previous = currentHistory[source.sourceId]
            val updated = availabilityTracker.update(
                previous = previous,
                sourceId = source.sourceId,
                seenInSnapshot = true,
                nowEpochMillis = nowEpochMillis,
            )
            currentHistory[source.sourceId] = updated
        }

        // Mark previously seen sources as missing if not in current snapshot
        for ((sourceId, previousStatus) in currentHistory) {
            if (sourceId !in seenSourceIds && sourceId != LOCAL_SCREEN_SOURCE_ID) {
                val updated = availabilityTracker.update(
                    previous = previousStatus,
                    sourceId = sourceId,
                    seenInSnapshot = false,
                    nowEpochMillis = nowEpochMillis,
                )
                currentHistory[sourceId] = updated
            }
        }

        availabilityHistory.value = currentHistory
    }

    override fun observeDiscoveryState(): Flow<DiscoverySnapshot> = discoveryState.asStateFlow()

    override fun startForegroundAutoRefresh(intervalSeconds: Int) {
        refreshCoordinator.start(intervalSeconds) {
            discoverSources(DiscoveryTrigger.FOREGROUND_TICK)
        }
    }

    override fun stopForegroundAutoRefresh() {
        refreshCoordinator.stop()
    }

    override fun observeAvailabilityHistory(): Flow<Map<String, SourceAvailabilityStatus>> {
        return availabilityHistory.asStateFlow()
    }

    override suspend fun getSourceAvailabilityStatus(sourceId: String): SourceAvailabilityStatus? {
        return availabilityHistory.value[sourceId]
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
