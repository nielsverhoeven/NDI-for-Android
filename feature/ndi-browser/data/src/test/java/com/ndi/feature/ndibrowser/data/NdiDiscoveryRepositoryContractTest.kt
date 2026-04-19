package com.ndi.feature.ndibrowser.data

import com.ndi.core.database.UserSelectionDao
import com.ndi.core.database.UserSelectionEntity
import com.ndi.core.model.CachedSourceRecord
import com.ndi.core.model.CachedSourceValidationState
import com.ndi.core.model.DiscoveryCompatibilityStatus
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.core.model.NdiLogCategory
import com.ndi.core.model.NdiLogLevel
import com.ndi.core.model.NdiSource
import com.ndi.feature.ndibrowser.data.repository.DeveloperDiagnosticsLogBuffer
import com.ndi.feature.ndibrowser.data.repository.NdiDiscoveryRepositoryImpl
import com.ndi.feature.ndibrowser.domain.repository.CachedSourceRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryConfigRepository
import com.ndi.sdkbridge.NdiDiscoveryBridge
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
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

    @Test
    fun discoverSources_emitsMixedCompatibilityResults_whenSomeEndpointsAreBlocked() = runTest {
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
        )

        repository.discoverSources(DiscoveryTrigger.MANUAL)
        val compatibility = repository.observeCompatibilitySnapshot().first()

        assertTrue(compatibility.results.any {
            it.targetId == "first.local:5959" && it.status == DiscoveryCompatibilityStatus.LIMITED
        })
        assertTrue(compatibility.results.any {
            it.targetId == "second.local:5960" && it.status == DiscoveryCompatibilityStatus.BLOCKED
        })
        assertTrue(compatibility.results.any {
            it.targetId == "configured-endpoints-overall" && it.status == DiscoveryCompatibilityStatus.LIMITED
        })
        assertFalse(compatibility.results.any {
            it.targetId == "configured-endpoints-overall" && it.status == DiscoveryCompatibilityStatus.COMPATIBLE
        })
    }

    @Test
    fun discoverSources_emitsNonBlockedOverallCompatibility_whenAllConfiguredEndpointsReachable() = runTest {
        val repository = NdiDiscoveryRepositoryImpl(
            bridge = FakeDiscoveryBridge(
                sources = listOf(NdiSource("source-a", "Camera A", lastSeenAtEpochMillis = 1L)),
                reachableHosts = setOf("first.local", "second.local"),
            ),
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
        val compatibility = repository.observeCompatibilitySnapshot().first()

        assertTrue(compatibility.results.any {
            it.targetId == "configured-endpoints-overall" &&
                it.status == DiscoveryCompatibilityStatus.LIMITED
        })
        assertFalse(compatibility.results.any {
            it.status == DiscoveryCompatibilityStatus.BLOCKED
        })
    }

    @Test
    fun discoverSources_persistsDiscoveredSourceIntoCachedRepository() = runTest {
        val cachedRepo = FakeCachedSourceRepository()
        val repository = NdiDiscoveryRepositoryImpl(
            bridge = FakeDiscoveryBridge(
                sources = listOf(
                    NdiSource(
                        sourceId = "source-a",
                        displayName = "Camera A",
                        endpointAddress = "192.168.1.50:5960",
                        isAvailable = true,
                        lastSeenAtEpochMillis = 10L,
                    ),
                ),
            ),
            userSelectionDao = FakeUserSelectionDao(),
            discoveryConfigRepository = FakeDiscoveryConfigRepository(),
            scope = TestScope(StandardTestDispatcher(testScheduler)),
            cachedSourceRepository = cachedRepo,
        )

        repository.discoverSources(DiscoveryTrigger.MANUAL)

        assertEquals(1, cachedRepo.upserts.size)
        assertEquals("source-a", cachedRepo.upserts.single().cacheKey)
        assertEquals(CachedSourceValidationState.AVAILABLE, cachedRepo.upserts.single().validationState)
    }

    @Test
    fun discoverSources_updatesValidationStateAcrossSubsequentDiscoveries() = runTest {
        val cachedRepo = FakeCachedSourceRepository()
        val bridge = SequenceDiscoveryBridge(
            responses = mutableListOf(
                listOf(
                    NdiSource(
                        sourceId = "source-a",
                        displayName = "Camera A",
                        endpointAddress = "192.168.1.50:5960",
                        isAvailable = true,
                        lastSeenAtEpochMillis = 10L,
                    ),
                ),
                listOf(
                    NdiSource(
                        sourceId = "source-a",
                        displayName = "Camera A",
                        endpointAddress = "192.168.1.50:5960",
                        isAvailable = false,
                        lastSeenAtEpochMillis = 20L,
                    ),
                ),
            ),
        )
        val repository = NdiDiscoveryRepositoryImpl(
            bridge = bridge,
            userSelectionDao = FakeUserSelectionDao(),
            discoveryConfigRepository = FakeDiscoveryConfigRepository(),
            scope = TestScope(StandardTestDispatcher(testScheduler)),
            cachedSourceRepository = cachedRepo,
        )

        repository.discoverSources(DiscoveryTrigger.MANUAL)
        repository.discoverSources(DiscoveryTrigger.MANUAL)

        assertEquals(2, cachedRepo.upserts.size)
        assertEquals(CachedSourceValidationState.AVAILABLE, cachedRepo.upserts[0].validationState)
        assertEquals(CachedSourceValidationState.UNAVAILABLE, cachedRepo.upserts[1].validationState)
        assertEquals(cachedRepo.upserts[0].cacheKey, cachedRepo.upserts[1].cacheKey)
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

private class SequenceDiscoveryBridge(
    private val responses: MutableList<List<NdiSource>>,
) : NdiDiscoveryBridge {
    override suspend fun discoverSources(): List<NdiSource> {
        return if (responses.isEmpty()) emptyList() else responses.removeAt(0)
    }

    override fun setDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?) = Unit
}

private class FakeCachedSourceRepository : CachedSourceRepository {
    val upserts: MutableList<CachedSourceRecord> = mutableListOf()
    private val state = MutableStateFlow<List<CachedSourceRecord>>(emptyList())

    override fun observeCachedSources(): Flow<List<CachedSourceRecord>> = state

    override suspend fun getCachedSource(cacheKey: String): CachedSourceRecord? {
        return state.value.firstOrNull { it.cacheKey == cacheKey }
    }

    override suspend fun upsertCachedSource(record: CachedSourceRecord) {
        upserts += record
        val next = state.value.filterNot { it.cacheKey == record.cacheKey } + record
        state.value = next
    }

    override suspend fun upsertFromDiscovery(record: CachedSourceRecord) {
        upserts += record
        val existing = state.value.firstOrNull { it.cacheKey == record.cacheKey }
        val merged = if (existing != null) {
            record.copy(retainedPreviewImagePath = existing.retainedPreviewImagePath)
        } else {
            record
        }
        val next = state.value.filterNot { it.cacheKey == record.cacheKey } + merged
        state.value = next
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
