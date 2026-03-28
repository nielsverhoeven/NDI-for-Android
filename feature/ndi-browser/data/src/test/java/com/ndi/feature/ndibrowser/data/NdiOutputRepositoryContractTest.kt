package com.ndi.feature.ndibrowser.data

import com.ndi.core.database.OutputSessionDao
import com.ndi.core.database.OutputSessionEntity
import com.ndi.core.model.NdiDiscoveryApplyResult
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.core.model.OutputState
import com.ndi.feature.ndibrowser.data.mapper.OutputSessionMapper
import com.ndi.feature.ndibrowser.data.repository.NdiOutputRepositoryImpl
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryConfigRepository
import com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentRepository
import com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentState
import com.ndi.sdkbridge.NdiOutputBridge
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.UUID

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

    @Test
    fun startOutput_failsWhenConfiguredDiscoveryServerIsUnreachable() = runTest {
        val bridge = FakeOutputBridge(discoveryReachable = false)
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = InMemoryOutputSessionDao(),
            outputBridge = bridge,
            discoveryConfigRepository = FakeOutputDiscoveryConfigRepository(
                NdiDiscoveryEndpoint(
                    host = "example.invalid",
                    port = 5960,
                    resolvedPort = 5960,
                    usesDefaultPort = false,
                ),
            ),
            mapper = OutputSessionMapper(),
        )

        val result = runCatching { repository.startOutput("camera-1", "Live") }

        assertTrue(result.isFailure)
        assertTrue(result.exceptionOrNull()?.message?.contains("discovery server", ignoreCase = true) == true)
        val latest = repository.observeOutputSession().first()
        assertEquals(OutputState.INTERRUPTED, latest.state)
    }

    @Test
    fun startOutput_usesConfiguredReachableDiscoveryServerPath() = runTest {
        val bridge = FakeOutputBridge(discoveryReachable = true)
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = InMemoryOutputSessionDao(),
            outputBridge = bridge,
            discoveryConfigRepository = FakeOutputDiscoveryConfigRepository(
                NdiDiscoveryEndpoint(
                    host = "127.0.0.1",
                    port = 5960,
                    resolvedPort = 5960,
                    usesDefaultPort = false,
                ),
            ),
            mapper = OutputSessionMapper(),
        )

        val session = repository.startOutput("camera-1", "Live")

        assertEquals(OutputState.ACTIVE, session.state)
        assertEquals(1, bridge.discoveryReachabilityChecks)
    }

    @Test
    fun startOutput_skipsDiscoveryReachabilityWhenNoEndpointConfigured() = runTest {
        val bridge = FakeOutputBridge(discoveryReachable = true)
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = InMemoryOutputSessionDao(),
            outputBridge = bridge,
            discoveryConfigRepository = FakeOutputDiscoveryConfigRepository(null),
            mapper = OutputSessionMapper(),
        )

        val session = repository.startOutput("camera-2", "Live")

        assertEquals(OutputState.ACTIVE, session.state)
        assertEquals(0, bridge.discoveryReachabilityChecks)
    }
}

private class FakeOutputBridge(
    private val reachable: Boolean = true,
    private val discoveryReachable: Boolean = true,
    private val startDelayMs: Long = 0L,
) : NdiOutputBridge {
    var startCount: Int = 0
    var localStartCount: Int = 0
    var discoveryReachabilityChecks: Int = 0

    override suspend fun isSourceReachable(sourceId: String): Boolean = reachable

    override suspend fun isDiscoveryServerReachable(host: String, port: Int?): Boolean {
        discoveryReachabilityChecks += 1
        return discoveryReachable
    }

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

private class FakeOutputDiscoveryConfigRepository(
    private val endpoint: NdiDiscoveryEndpoint?,
) : NdiDiscoveryConfigRepository {
    override fun observeDiscoveryEndpoint(): Flow<NdiDiscoveryEndpoint?> = flowOf(endpoint)

    override suspend fun applyDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?): NdiDiscoveryApplyResult {
        return NdiDiscoveryApplyResult(
            applyId = UUID.randomUUID().toString(),
            endpoint = endpoint,
            interruptedActiveStream = false,
            fallbackTriggered = false,
            appliedAtEpochMillis = System.currentTimeMillis(),
        )
    }

    override suspend fun getCurrentEndpoint(): NdiDiscoveryEndpoint? = endpoint
}
