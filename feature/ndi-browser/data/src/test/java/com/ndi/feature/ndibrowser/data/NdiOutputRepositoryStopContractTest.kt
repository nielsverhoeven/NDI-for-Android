package com.ndi.feature.ndibrowser.data

import com.ndi.core.database.OutputSessionDao
import com.ndi.core.database.OutputSessionEntity
import com.ndi.core.model.OutputHealthSnapshot
import com.ndi.core.model.OutputQualityLevel
import com.ndi.core.model.OutputState
import com.ndi.feature.ndibrowser.data.mapper.OutputSessionMapper
import com.ndi.feature.ndibrowser.data.repository.NdiOutputRepositoryImpl
import com.ndi.sdkbridge.NdiOutputBridge
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class NdiOutputRepositoryStopContractTest {

    @Test
    fun stopOutput_transitionsToStoppedFromActiveAndPersists() = runTest {
        val dao = StopContractOutputSessionDao()
        val bridge = FakeStopBridge()
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = dao,
            outputBridge = bridge,
            mapper = OutputSessionMapper(),
        )

        repository.startOutput("camera-1", "Live")
        val stopped = repository.stopOutput()

        assertEquals(OutputState.STOPPED, stopped.state)
        assertEquals(OutputState.STOPPED.name, dao.latest?.state)
        assertEquals(1, bridge.stopCount)
    }

    @Test
    fun stopOutput_returnsStoppedWhenSessionIsActive() = runTest {
        val dao = StopContractOutputSessionDao()
        val bridge = FakeStopBridge()
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = dao,
            outputBridge = bridge,
            mapper = OutputSessionMapper(),
        )

        repository.startOutput("camera-2", "Live")
        val stopped = repository.stopOutput()

        assertEquals(OutputState.STOPPED, stopped.state)
    }

    @Test
    fun stopOutput_isIdempotentWhenAlreadyStopped() = runTest {
        val dao = StopContractOutputSessionDao()
        val bridge = FakeStopBridge()
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = dao,
            outputBridge = bridge,
            mapper = OutputSessionMapper(),
        )

        repository.startOutput("camera-3", "Live")
        repository.stopOutput()
        val second = repository.stopOutput()

        assertEquals(OutputState.STOPPED, second.state)
        assertEquals(1, bridge.stopCount)
    }

    @Test
    fun stopOutput_updatesHealthSnapshot() = runTest {
        val dao = StopContractOutputSessionDao()
        val bridge = FakeStopBridge()
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = dao,
            outputBridge = bridge,
            mapper = OutputSessionMapper(),
        )

        repository.startOutput("camera-4", "Live")
        repository.stopOutput()

        val health: OutputHealthSnapshot = repository.observeOutputHealth().first()
        assertEquals(OutputQualityLevel.DEGRADED, health.qualityLevel)
        assertTrue(health.messageCode?.contains("stopped") == true)
    }
}

private class FakeStopBridge : NdiOutputBridge {
    var stopCount: Int = 0

    override suspend fun isSourceReachable(sourceId: String): Boolean = true

    override fun startSender(sourceId: String, streamName: String) = Unit

    override fun stopSender() {
        stopCount += 1
    }
}

private class StopContractOutputSessionDao : OutputSessionDao {
    var latest: OutputSessionEntity? = null

    override suspend fun getLatest(): OutputSessionEntity? = latest

    override suspend fun upsert(session: OutputSessionEntity) {
        latest = session
    }
}
