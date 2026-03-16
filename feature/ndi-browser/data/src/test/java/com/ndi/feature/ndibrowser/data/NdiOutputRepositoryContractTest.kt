package com.ndi.feature.ndibrowser.data

import com.ndi.core.database.OutputSessionDao
import com.ndi.core.database.OutputSessionEntity
import com.ndi.core.model.OutputState
import com.ndi.feature.ndibrowser.data.mapper.OutputSessionMapper
import com.ndi.feature.ndibrowser.data.repository.NdiOutputRepositoryImpl
import com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentRepository
import com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentState
import com.ndi.sdkbridge.NdiOutputBridge
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
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

    @Test
    fun startOutput_rejectsLocalScreenSourceWhenConsentMissing() = runTest {
        val bridge = FakeOutputBridge()
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = InMemoryOutputSessionDao(),
            outputBridge = bridge,
            screenCaptureConsentRepository = InMemoryScreenCaptureConsentRepository(granted = false),
            mapper = OutputSessionMapper(),
        )

        val result = runCatching { repository.startOutput("device-screen:local", "Local") }

        assertTrue(result.isFailure)
        assertEquals(0, bridge.localStartCount)
    }

    @Test
    fun startOutput_usesLocalSenderWhenConsentGranted() = runTest {
        val bridge = FakeOutputBridge()
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = InMemoryOutputSessionDao(),
            outputBridge = bridge,
            screenCaptureConsentRepository = InMemoryScreenCaptureConsentRepository(granted = true),
            mapper = OutputSessionMapper(),
        )

        val session = repository.startOutput("device-screen:local", "Local")

        assertEquals(OutputState.ACTIVE, session.state)
        assertEquals(1, bridge.localStartCount)
        assertEquals(0, bridge.startCount)
    }

    @Test
    fun startOutput_isIdempotentForRapidConcurrentRequests() = runTest {
        val bridge = FakeOutputBridge(startDelayMs = 100)
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = InMemoryOutputSessionDao(),
            outputBridge = bridge,
            mapper = OutputSessionMapper(),
        )

        val sessions = listOf(
            async { repository.startOutput("camera-rapid", "Live") },
            async { repository.startOutput("camera-rapid", "Live") },
        ).awaitAll()

        assertEquals(1, bridge.startCount)
        assertEquals(sessions.first().sessionId, sessions.last().sessionId)
        assertEquals(OutputState.ACTIVE, sessions.last().state)
    }
}

private class FakeOutputBridge(
    private val reachable: Boolean = true,
    private val startDelayMs: Long = 0L,
) : NdiOutputBridge {
    var startCount: Int = 0
    var localStartCount: Int = 0

    override suspend fun isSourceReachable(sourceId: String): Boolean = reachable

    override fun startSender(sourceId: String, streamName: String) {
        if (startDelayMs > 0) {
            Thread.sleep(startDelayMs)
        }
        startCount += 1
    }

    override fun stopSender() = Unit

    override fun startLocalScreenShareSender(streamName: String) {
        localStartCount += 1
    }

    override fun stopLocalScreenShareSender() = Unit
}

private class InMemoryScreenCaptureConsentRepository(
    private val granted: Boolean,
) : ScreenCaptureConsentRepository {
    override suspend fun beginConsentRequest(inputSourceId: String) = Unit

    override suspend fun registerConsentResult(
        inputSourceId: String,
        granted: Boolean,
        tokenRef: String?,
    ): ScreenCaptureConsentState {
        return ScreenCaptureConsentState(inputSourceId, granted, tokenRef)
    }

    override suspend fun getConsentState(inputSourceId: String): ScreenCaptureConsentState? {
        return ScreenCaptureConsentState(inputSourceId, granted = granted)
    }

    override suspend fun clearConsent(inputSourceId: String) = Unit
}

private class InMemoryOutputSessionDao : OutputSessionDao {
    private var latest: OutputSessionEntity? = null

    override suspend fun getLatest(): OutputSessionEntity? = latest

    override suspend fun upsert(session: OutputSessionEntity) {
        latest = session
    }
}
