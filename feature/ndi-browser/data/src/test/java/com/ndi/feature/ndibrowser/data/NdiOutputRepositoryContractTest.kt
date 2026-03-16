package com.ndi.feature.ndibrowser.data

import com.ndi.core.database.OutputSessionDao
import com.ndi.core.database.OutputSessionEntity
import com.ndi.core.model.OutputState
import com.ndi.feature.ndibrowser.data.mapper.OutputSessionMapper
import com.ndi.feature.ndibrowser.data.repository.NdiOutputRepositoryImpl
import com.ndi.sdkbridge.NdiOutputBridge
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class NdiOutputRepositoryContractTest {

    @Test
    fun startOutput_isIdempotentWhenAlreadyActive() = runTest {
        val bridge = FakeOutputBridge()
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = InMemoryOutputSessionDao(),
            outputBridge = bridge,
            mapper = OutputSessionMapper(),
        )

        val first = repository.startOutput("camera-1", "Live")
        val second = repository.startOutput("camera-1", "Live")

        assertEquals(first.sessionId, second.sessionId)
        assertEquals(OutputState.ACTIVE, second.state)
        assertEquals(1, bridge.startCount)
    }

    @Test
    fun startOutput_rejectsUnreachableSource() = runTest {
        val bridge = FakeOutputBridge(reachable = false)
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = InMemoryOutputSessionDao(),
            outputBridge = bridge,
            mapper = OutputSessionMapper(),
        )

        val result = runCatching { repository.startOutput("camera-x", "Live") }

        assertTrue(result.isFailure)
    }
}

private class FakeOutputBridge(
    private val reachable: Boolean = true,
) : NdiOutputBridge {
    var startCount: Int = 0

    override suspend fun isSourceReachable(sourceId: String): Boolean = reachable

    override fun startSender(sourceId: String, streamName: String) {
        startCount += 1
    }

    override fun stopSender() = Unit
}

private class InMemoryOutputSessionDao : OutputSessionDao {
    private var latest: OutputSessionEntity? = null

    override suspend fun getLatest(): OutputSessionEntity? = latest

    override suspend fun upsert(session: OutputSessionEntity) {
        latest = session
    }
}
