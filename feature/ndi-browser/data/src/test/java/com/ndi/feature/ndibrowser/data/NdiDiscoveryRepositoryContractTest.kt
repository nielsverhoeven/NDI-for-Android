package com.ndi.feature.ndibrowser.data

import com.ndi.core.database.UserSelectionDao
import com.ndi.core.database.UserSelectionEntity
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.NdiLogCategory
import com.ndi.core.model.NdiLogLevel
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.core.model.NdiSource
import com.ndi.feature.ndibrowser.data.repository.DeveloperDiagnosticsLogBuffer
import com.ndi.feature.ndibrowser.data.repository.NdiDiscoveryRepositoryImpl
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryConfigRepository
import com.ndi.sdkbridge.NdiDiscoveryBridge
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class NdiDiscoveryRepositoryContractTest {

    @Test
    fun discoverSources_deduplicatesByCanonicalSourceId() = runTest {
        val repository = NdiDiscoveryRepositoryImpl(
            bridge = FakeDiscoveryBridge(
                listOf(
                    NdiSource("source-a", "Camera A", lastSeenAtEpochMillis = 1L),
                    NdiSource("source-a", "Camera A Duplicate", lastSeenAtEpochMillis = 2L),
                    NdiSource("source-b", "Camera B", lastSeenAtEpochMillis = 3L),
                ),
            ),
            userSelectionDao = FakeUserSelectionDao(),
            discoveryConfigRepository = FakeDiscoveryConfigRepository(),
            scope = TestScope(StandardTestDispatcher(testScheduler)),
        )

        val snapshot = repository.discoverSources(DiscoveryTrigger.MANUAL)

        assertEquals(DiscoveryStatus.SUCCESS, snapshot.status)
        // Expected: LOCAL_SCREEN (device-screen:local) is added first, then deduped sources
        assertEquals(listOf("device-screen:local", "source-a", "source-b"), snapshot.sources.map { it.sourceId })
    }

    @Test
    fun discoverSources_emitsFailureStateWhenBridgeThrows() = runTest {
        val repository = NdiDiscoveryRepositoryImpl(
            bridge = ThrowingDiscoveryBridge,
            userSelectionDao = FakeUserSelectionDao(),
            discoveryConfigRepository = FakeDiscoveryConfigRepository(),
            scope = TestScope(StandardTestDispatcher(testScheduler)),
        )

        val snapshot = repository.discoverSources(DiscoveryTrigger.MANUAL)

        assertEquals(DiscoveryStatus.FAILURE, snapshot.status)
        assertTrue(snapshot.errorMessage?.contains("bridge") == true)
    }

    @Test
    fun discoverSources_passesAllEndpointsAtOnceToTheBridge() = runTest {
        // The NDI SDK supports comma-separated multi-server natively via NDI_DISCOVERY_SERVER.
        // The repository must pass ALL configured endpoints in a single setDiscoveryEndpoints
        // call rather than one-at-a-time in a loop (which would trigger repeated NDI reinit).
        val bridge = FakeDiscoveryBridge(
            sources = emptyList(),
        )
        val repository = NdiDiscoveryRepositoryImpl(
            bridge = bridge,
            userSelectionDao = FakeUserSelectionDao(),
            discoveryConfigRepository = FakeDiscoveryConfigRepository(
                listOf(
                    NdiDiscoveryEndpoint("first.local", 5959, 5959, usesDefaultPort = false),
                    NdiDiscoveryEndpoint("second.local", 5960, 5960, usesDefaultPort = false),
                ),
            ),
            scope = TestScope(StandardTestDispatcher(testScheduler)),
        )

        repository.discoverSources(DiscoveryTrigger.MANUAL)

        // Exactly ONE setDiscoveryEndpoints call with both endpoints in order.
        assertEquals(1, bridge.configuredEndpointCalls.size)
        val call = bridge.configuredEndpointCalls.first()
        assertEquals(listOf("first.local", "second.local"), call.map { it.host })
    }

    @Test
    fun discoverSources_returnsEmptyWhenNoDiscoveredSourcesBeyondLocal() = runTest {
        val repository = NdiDiscoveryRepositoryImpl(
            bridge = FakeDiscoveryBridge(sources = emptyList()),
            userSelectionDao = FakeUserSelectionDao(),
            discoveryConfigRepository = FakeDiscoveryConfigRepository(),
            scope = TestScope(StandardTestDispatcher(testScheduler)),
        )

        val snapshot = repository.discoverSources(DiscoveryTrigger.MANUAL)

        assertEquals(DiscoveryStatus.EMPTY, snapshot.status)
        assertEquals(listOf("device-screen:local"), snapshot.sources.map { it.sourceId })
    }

    @Test
    fun discoverSources_logsDiscoveryInfoForConfiguredServers() = runTest {
        val bridge = FakeDiscoveryBridge(sources = emptyList())
        val diagnostics = DeveloperDiagnosticsLogBuffer()
        val repository = NdiDiscoveryRepositoryImpl(
            bridge = bridge,
            userSelectionDao = FakeUserSelectionDao(),
            discoveryConfigRepository = FakeDiscoveryConfigRepository(
                listOf(
                    NdiDiscoveryEndpoint("first.local", 5959, 5959, usesDefaultPort = false),
                    NdiDiscoveryEndpoint("second.local", 5960, 5960, usesDefaultPort = false),
                ),
            ),
            scope = TestScope(StandardTestDispatcher(testScheduler)),
            diagnosticsLogBuffer = diagnostics,
        )

        repository.discoverSources(DiscoveryTrigger.MANUAL)
        val logs = diagnostics.observeRecentLogs().first()

        // With the new architecture, one log entry describes all configured servers.
        assertTrue(logs.any {
            it.category == NdiLogCategory.DISCOVERY &&
                it.level == NdiLogLevel.INFO &&
                it.messageRedacted.contains("discovery via")
        })
    }

    @Test
    fun discoverSources_logsPartialEndpointReachabilityWhenSomeConfiguredServersAreUnavailable() = runTest {
        val diagnostics = DeveloperDiagnosticsLogBuffer()
        val repository = NdiDiscoveryRepositoryImpl(
            bridge = FakeDiscoveryBridge(
                sources = listOf(NdiSource("source-a", "Camera A", lastSeenAtEpochMillis = 1L)),
                reachableHosts = setOf("first.local"),
            ),
            userSelectionDao = FakeUserSelectionDao(),
            discoveryConfigRepository = FakeDiscoveryConfigRepository(
                listOf(
                    NdiDiscoveryEndpoint("first.local", 5959, 5959, usesDefaultPort = false),
                    NdiDiscoveryEndpoint("second.local", 5960, 5960, usesDefaultPort = false),
                ),
            ),
            scope = TestScope(StandardTestDispatcher(testScheduler)),
            diagnosticsLogBuffer = diagnostics,
        )

        repository.discoverSources(DiscoveryTrigger.MANUAL)
        val logs = diagnostics.observeRecentLogs().first()

        assertTrue(logs.any {
            it.category == NdiLogCategory.DISCOVERY &&
                it.level == NdiLogLevel.WARN &&
                it.messageRedacted.contains("reachability partial: reachable=1 unreachable=1 total=2")
        })
        assertTrue(logs.any {
            it.category == NdiLogCategory.DISCOVERY &&
                it.level == NdiLogLevel.INFO &&
                it.messageRedacted.contains("sourceCount=1 totalWithLocal=2")
        })
    }
}

private class FakeDiscoveryBridge(
    private val sources: List<NdiSource>,
    private val reachableHosts: Set<String> = emptySet(),
) : NdiDiscoveryBridge {
    val configuredEndpointCalls: MutableList<List<NdiDiscoveryEndpoint>> = mutableListOf()

    override suspend fun discoverSources(): List<NdiSource> = sources

    override suspend fun isDiscoveryServerReachable(host: String, port: Int?): Boolean {
        return host in reachableHosts
    }

    override fun setDiscoveryEndpoints(endpoints: List<NdiDiscoveryEndpoint>) {
        configuredEndpointCalls += endpoints
    }

    override fun setDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?) {
        configuredEndpointCalls += listOfNotNull(endpoint)
    }
}

private object ThrowingDiscoveryBridge : NdiDiscoveryBridge {
    override suspend fun discoverSources(): List<NdiSource> {
        error("bridge failure")
    }

    override fun setDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?) = Unit
}

private class FakeUserSelectionDao : UserSelectionDao {
    override suspend fun getSelection(): UserSelectionEntity? = null

    override suspend fun upsert(selection: UserSelectionEntity) = Unit
}

private class FakeDiscoveryConfigRepository(
    private val endpoints: List<NdiDiscoveryEndpoint> = emptyList(),
) : NdiDiscoveryConfigRepository {
    override fun observeDiscoveryEndpoints(): Flow<List<NdiDiscoveryEndpoint>> = flowOf(endpoints)

    override fun observeDiscoveryEndpoint(): Flow<NdiDiscoveryEndpoint?> = flowOf(endpoints.firstOrNull())

    override suspend fun getCurrentEndpoints(): List<NdiDiscoveryEndpoint> = endpoints

    override suspend fun getCurrentEndpoint(): NdiDiscoveryEndpoint? = endpoints.firstOrNull()

    override suspend fun applyDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?) =
        throw NotImplementedError("Not implemented in test")
}
