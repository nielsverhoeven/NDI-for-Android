package com.ndi.feature.ndibrowser.data

import com.ndi.core.database.UserSelectionDao
import com.ndi.core.database.UserSelectionEntity
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.core.model.NdiSource
import com.ndi.feature.ndibrowser.data.repository.NdiDiscoveryRepositoryImpl
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryConfigRepository
import com.ndi.sdkbridge.NdiDiscoveryBridge
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
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
}

private class FakeDiscoveryBridge(
    private val sources: List<NdiSource>,
) : NdiDiscoveryBridge {
    override suspend fun discoverSources(): List<NdiSource> = sources
    override fun setDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?) = Unit
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

private class FakeDiscoveryConfigRepository : NdiDiscoveryConfigRepository {
    override fun observeDiscoveryEndpoint(): Flow<NdiDiscoveryEndpoint?> = flowOf(null)
    override suspend fun getCurrentEndpoint(): NdiDiscoveryEndpoint? = null
    override suspend fun applyDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?) = 
        throw NotImplementedError("Not implemented in test")
}
