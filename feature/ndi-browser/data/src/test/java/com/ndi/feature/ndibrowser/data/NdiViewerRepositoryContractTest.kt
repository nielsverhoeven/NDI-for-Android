package com.ndi.feature.ndibrowser.data

import com.ndi.core.database.ViewerSessionDao
import com.ndi.core.database.ViewerSessionEntity
import com.ndi.core.model.PlaybackState
import com.ndi.feature.ndibrowser.data.repository.NdiViewerRepositoryImpl
import com.ndi.sdkbridge.NdiViewerBridge
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
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
}

private object AlwaysFailingViewerBridge : NdiViewerBridge {
    override fun startReceiver(sourceId: String) {
        error("receiver unavailable")
    }

    override fun stopReceiver() = Unit

    override fun getLatestReceiverFrame() = null
}

private class InMemoryViewerSessionDao : ViewerSessionDao {
    private var value: ViewerSessionEntity? = null

    override suspend fun getLatest(): ViewerSessionEntity? = value

    override suspend fun upsert(session: ViewerSessionEntity) {
        value = session
    }
}