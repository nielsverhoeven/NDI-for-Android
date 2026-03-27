package com.ndi.feature.ndibrowser.data.repository

import android.util.Log
import com.ndi.core.database.UserSelectionDao
import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.feature.ndibrowser.data.DiscoveryRefreshCoordinator
import com.ndi.feature.ndibrowser.data.mapper.NdiSourceMapper
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryConfigRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.sdkbridge.NdiDiscoveryBridge
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.withContext
import kotlinx.coroutines.flow.asStateFlow
import java.util.UUID

class NdiDiscoveryRepositoryImpl(
    private val bridge: NdiDiscoveryBridge,
    private val userSelectionDao: UserSelectionDao,
    private val discoveryConfigRepository: NdiDiscoveryConfigRepository,
    private val scope: CoroutineScope = CoroutineScope(SupervisorJob() + Dispatchers.IO),
    private val sourceMapper: NdiSourceMapper = NdiSourceMapper(),
    private val refreshCoordinator: DiscoveryRefreshCoordinator = DiscoveryRefreshCoordinator(scope),
) : NdiDiscoveryRepository {

    private companion object {
        const val LOCAL_SCREEN_SOURCE_ID = "device-screen:local"
        const val LOCAL_SCREEN_DISPLAY_NAME = "This Device Screen"
        const val TAG = "NdiDiscoveryRepo"
    }

    private val discoveryState = MutableStateFlow(emptySnapshot())

    override suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot {
        val startedAt = System.currentTimeMillis()
        discoveryState.value = discoveryState.value.copy(
            startedAtEpochMillis = startedAt,
            status = DiscoveryStatus.IN_PROGRESS,
        )

        return runCatching {
            // Fetch and set the configured discovery endpoint on the bridge
            val endpoint = discoveryConfigRepository.getCurrentEndpoint()
            runCatching { Log.d(TAG, "Discovery trigger=$trigger, endpoint=$endpoint") }
                // setDiscoveryEndpoint calls nativeSetDiscoveryExtraIps (JNI), which
                // acquires g_state_mutex.  Calling it on the main thread while the
                // native mutex is contended causes an ANR.  Always run on IO.
                withContext(Dispatchers.IO) {
                    bridge.setDiscoveryEndpoint(endpoint)
                }
            
            userSelectionDao.getSelection()
            val sources = sourceMapper.map(bridge.discoverSources())
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
        refreshCoordinator.start(intervalSeconds) {
            discoverSources(DiscoveryTrigger.FOREGROUND_TICK)
        }
    }

    override fun stopForegroundAutoRefresh() {
        refreshCoordinator.stop()
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
