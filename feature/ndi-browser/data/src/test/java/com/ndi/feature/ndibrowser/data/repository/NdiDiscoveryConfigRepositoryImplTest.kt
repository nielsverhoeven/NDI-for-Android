package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.DiscoverySelectionOutcome
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test

class NdiDiscoveryConfigRepositoryImplTest {

    private lateinit var fakeDiscoveryRepo: FakeDiscoveryServerRepository
    private lateinit var repository: NdiDiscoveryConfigRepositoryImpl

    @Before
    fun setUp() {
        fakeDiscoveryRepo = FakeDiscoveryServerRepository()
        repository = NdiDiscoveryConfigRepositoryImpl(fakeDiscoveryRepo)
    }

    @Test
    fun resolveWithNoEnabledReturnsNoEnabled() = runTest {
        val entry = fakeDiscoveryRepo.addServer("host.local", "")
        fakeDiscoveryRepo.setServerEnabled(entry.id, false)
        val result = fakeDiscoveryRepo.resolveActiveDiscoveryTarget()
        assertEquals(DiscoverySelectionOutcome.NO_ENABLED_SERVERS, result.result)
        assertNull(result.selectedEntryId)
        assertNotNull(result.errorMessage)
    }

    @Test
    fun resolveWithEnabledServerReturnsSuccess() = runTest {
        fakeDiscoveryRepo.addServer("host.local", "")
        val result = fakeDiscoveryRepo.resolveActiveDiscoveryTarget()
        assertEquals(DiscoverySelectionOutcome.SUCCESS, result.result)
        assertNotNull(result.selectedEntryId)
    }

    @Test
    fun resolveReturnsFirstEnabledByOrderIndex() = runTest {
        fakeDiscoveryRepo.addServer("first.local", "")
        fakeDiscoveryRepo.addServer("second.local", "")
        val allServers = fakeDiscoveryRepo.observeServers().first()
        fakeDiscoveryRepo.setServerEnabled(allServers[0].id, false)
        val result = fakeDiscoveryRepo.resolveActiveDiscoveryTarget()
        assertEquals(DiscoverySelectionOutcome.SUCCESS, result.result)
        val enabledAfter = fakeDiscoveryRepo.observeServers().first().filter { it.enabled }
        assertEquals(enabledAfter.first().id, result.selectedEntryId)
    }

    @Test
    fun resolveWithNoServersReturnsNoEnabled() = runTest {
        val result = fakeDiscoveryRepo.resolveActiveDiscoveryTarget()
        assertEquals(DiscoverySelectionOutcome.NO_ENABLED_SERVERS, result.result)
    }

    @Test
    fun setEnabledPersistsAcrossObserve() = runTest {
        val entry = fakeDiscoveryRepo.addServer("toggle.local", "")
        fakeDiscoveryRepo.setServerEnabled(entry.id, false)
        val servers = fakeDiscoveryRepo.observeServers().first()
        assertEquals(false, servers[0].enabled)
        fakeDiscoveryRepo.setServerEnabled(entry.id, true)
        val servers2 = fakeDiscoveryRepo.observeServers().first()
        assertEquals(true, servers2[0].enabled)
    }

    @Test
    fun reorderChangesFailoverOrder() = runTest {
        fakeDiscoveryRepo.addServer("first.local", "")
        fakeDiscoveryRepo.addServer("second.local", "")
        val servers = fakeDiscoveryRepo.observeServers().first()
        val reversed = servers.map { it.id }.reversed()
        fakeDiscoveryRepo.reorderServers(reversed)
        val after = fakeDiscoveryRepo.observeServers().first()
        assertEquals(reversed[0], after[0].id)
    }

    @Test
    fun getCurrentEndpoints_returnsAllEnabledServersInPersistedOrder() = runTest {
        val first = fakeDiscoveryRepo.addServer("first.local", "5959")
        fakeDiscoveryRepo.addServer("second.local", "5960")
        fakeDiscoveryRepo.addServer("third.local", "5961")
        fakeDiscoveryRepo.setServerEnabled(first.id, false)

        val endpoints = repository.getCurrentEndpoints()

        assertEquals(listOf("second.local", "third.local"), endpoints.map { it.host })
        assertEquals(listOf(5960, 5961), endpoints.map { it.resolvedPort })
    }

    @Test
    fun observeDiscoveryEndpoint_returnsFirstEnabledServer() = runTest {
        fakeDiscoveryRepo.addServer("first.local", "5959")
        fakeDiscoveryRepo.addServer("second.local", "5960")

        val endpoint = repository.observeDiscoveryEndpoint().first()

        assertEquals("first.local", endpoint?.host)
        assertEquals(5959, endpoint?.resolvedPort)
    }
}
