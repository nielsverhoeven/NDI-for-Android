package com.ndi.feature.ndibrowser.data

import com.ndi.core.database.ViewerSessionDao
import com.ndi.core.database.ViewerSessionEntity
import com.ndi.core.model.CachedSourceRecord
import com.ndi.core.model.CachedSourceValidationState
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.feature.ndibrowser.data.repository.NdiViewerRepositoryImpl
import com.ndi.feature.ndibrowser.domain.repository.CachedSourceRepository
import com.ndi.sdkbridge.NdiViewerBridge
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Test

class NdiViewerRepositoryContractTest {

    @Test
    fun retryReconnectWithinWindow_stopsWhenRecoveryFails() = runTest {
        val repository = NdiViewerRepositoryImpl(
            bridge = AlwaysFailingViewerBridge,
            viewerSessionDao = InMemoryViewerSessionDao(),
            reconnectCoordinator = ViewerReconnectCoordinator(retryDelayMillis = 0),
        )

        val session = repository.retryReconnectWithinWindow("camera-9", windowSeconds = 2)

        assertEquals(PlaybackState.STOPPED, session.playbackState)
        assertEquals("camera-9", session.selectedSourceId)
        assertEquals(2, session.retryAttempts)
    }

    @Test
    fun connectToSource_routesReceiverStartupViaPersistedSourceEndpoint_notDiscoveryServerEndpoint() = runTest {
        val bridge = CapturingViewerBridge()
        val cachedRepo = SingleRecordCachedSourceRepository(
            CachedSourceRecord(
                cacheKey = "camera-1",
                stableSourceId = "camera-1",
                lastObservedSourceId = "camera-1",
                displayName = "Camera 1",
                endpointHost = "10.20.30.40",
                endpointPort = 5961,
                endpointKey = "10.20.30.40:5961",
                validationState = CachedSourceValidationState.AVAILABLE,
                firstCachedAtEpochMillis = 1L,
                lastDiscoveredAtEpochMillis = 2L,
                updatedAtEpochMillis = 2L,
            ),
        )
        val repository = NdiViewerRepositoryImpl(
            bridge = bridge,
            viewerSessionDao = InMemoryViewerSessionDao(),
            cachedSourceRepository = cachedRepo,
        )

        val session = repository.connectToSource("camera-1")

        assertEquals(PlaybackState.PLAYING, session.playbackState)
        assertEquals("10.20.30.40:5961", bridge.lastStartReceiverSourceId)
        assertNotEquals("discovery-a.local:5959", bridge.lastStartReceiverSourceId)
    }
}

private object AlwaysFailingViewerBridge : NdiViewerBridge {
    override fun startReceiver(sourceId: String) {
        error("receiver unavailable")
    }

    override fun stopReceiver() = Unit

    override fun getLatestReceiverFrame() = null
}

private class CapturingViewerBridge : NdiViewerBridge {
    var lastStartReceiverSourceId: String? = null

    override fun startReceiver(sourceId: String) {
        lastStartReceiverSourceId = sourceId
    }

    override fun stopReceiver() = Unit

    override fun getLatestReceiverFrame(): ViewerVideoFrame {
        return ViewerVideoFrame(
            width = 2,
            height = 2,
            argbPixels = intArrayOf(1, 1, 1, 1),
        )
    }
}

private class InMemoryViewerSessionDao : ViewerSessionDao {
    private var value: ViewerSessionEntity? = null

    override suspend fun getLatest(): ViewerSessionEntity? = value

    override suspend fun upsert(session: ViewerSessionEntity) {
        value = session
    }
}

private class SingleRecordCachedSourceRepository(
    record: CachedSourceRecord,
) : CachedSourceRepository {
    private val state = MutableStateFlow(listOf(record))

    override fun observeCachedSources(): Flow<List<CachedSourceRecord>> = state

    override suspend fun getCachedSource(cacheKey: String): CachedSourceRecord? {
        return state.value.firstOrNull { it.cacheKey == cacheKey }
    }

    override suspend fun upsertCachedSource(record: CachedSourceRecord) {
        state.value = listOf(record)
    }

    override suspend fun upsertFromDiscovery(record: CachedSourceRecord) {
        state.value = listOf(record)
    }

    override suspend fun upsertDiscoveryAssociation(
        cacheKey: String,
        discoveryServerId: String,
        observedAtEpochMillis: Long,
    ) = Unit

    override suspend fun markValidationState(
        cacheKey: String,
        state: CachedSourceValidationState,
        validationStartedAtEpochMillis: Long?,
        validatedAtEpochMillis: Long?,
        availableAtEpochMillis: Long?,
    ) = Unit
}