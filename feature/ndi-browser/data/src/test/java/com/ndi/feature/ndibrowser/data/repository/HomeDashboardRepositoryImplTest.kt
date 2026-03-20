package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.OutputHealthSnapshot
import com.ndi.core.model.OutputQualityLevel
import com.ndi.core.model.OutputSession
import com.ndi.core.model.OutputState
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerSession
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.UUID

class HomeDashboardRepositoryImplTest {

    @Test
    fun refreshDashboardSnapshot_returnsNonNullSnapshot() = runTest {
        val repo = HomeDashboardRepositoryImpl(
            outputRepository = DashboardFakeOutputRepository(),
            viewerRepository = DashboardFakeViewerRepository(),
            userSelectionRepository = DashboardFakeUserSelectionRepository(),
        )
        val snapshot = repo.refreshDashboardSnapshot()
        assertTrue(snapshot.generatedAtEpochMillis > 0)
        assertTrue(snapshot.canNavigateToStream)
        assertTrue(snapshot.canNavigateToView)
    }

    @Test
    fun refreshDashboardSnapshot_withLastSelectedSource_includesSourceId() = runTest {
        val selectionRepo = DashboardFakeUserSelectionRepository(lastId = "camera-1")
        val repo = HomeDashboardRepositoryImpl(
            outputRepository = DashboardFakeOutputRepository(),
            viewerRepository = DashboardFakeViewerRepository(),
            userSelectionRepository = selectionRepo,
        )
        val snapshot = repo.refreshDashboardSnapshot()
        assertEquals("camera-1", snapshot.selectedViewSourceId)
    }

    @Test
    fun refreshDashboardSnapshot_noLastSource_selectedViewSourceIdIsNull() = runTest {
        val repo = HomeDashboardRepositoryImpl(
            outputRepository = DashboardFakeOutputRepository(),
            viewerRepository = DashboardFakeViewerRepository(),
            userSelectionRepository = DashboardFakeUserSelectionRepository(lastId = null),
        )
        val snapshot = repo.refreshDashboardSnapshot()
        assertNull(snapshot.selectedViewSourceId)
    }
}

private class DashboardFakeOutputRepository : NdiOutputRepository {
    private val session = MutableStateFlow(
        OutputSession(
            sessionId = UUID.randomUUID().toString(),
            inputSourceId = "",
            outboundStreamName = "",
            state = OutputState.READY,
            startedAtEpochMillis = 0L,
        ),
    )
    private val health = MutableStateFlow(
        OutputHealthSnapshot(
            snapshotId = UUID.randomUUID().toString(),
            sessionId = session.value.sessionId,
            capturedAtEpochMillis = 0L,
            networkReachable = true,
            inputReachable = true,
            qualityLevel = OutputQualityLevel.HEALTHY,
        ),
    )

    override suspend fun startOutput(inputSourceId: String, streamName: String) = session.value
    override suspend fun stopOutput() = session.value
    override fun observeOutputSession(): Flow<OutputSession> = session
    override suspend fun retryInterruptedOutputWithinWindow(windowSeconds: Int) = session.value
    override fun observeOutputHealth(): Flow<OutputHealthSnapshot> = health
}

private class DashboardFakeViewerRepository : NdiViewerRepository {
    private val session = MutableStateFlow(
        ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = "",
            playbackState = PlaybackState.STOPPED,
            startedAtEpochMillis = 0L,
        ),
    )

    override suspend fun connectToSource(sourceId: String) = session.value
    override fun observeViewerSession(): Flow<ViewerSession> = session
    override suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int) = session.value
    override suspend fun stopViewing() = Unit
}

private class DashboardFakeUserSelectionRepository(
    private val lastId: String? = null,
) : UserSelectionRepository {
    override suspend fun saveLastSelectedSource(sourceId: String) = Unit
    override suspend fun getLastSelectedSource(): String? = lastId
}



