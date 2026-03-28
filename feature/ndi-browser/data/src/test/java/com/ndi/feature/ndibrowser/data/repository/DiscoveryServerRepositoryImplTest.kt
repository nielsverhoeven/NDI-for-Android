package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.DEFAULT_DISCOVERY_SERVER_PORT
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class DiscoveryServerRepositoryImplTest {

    private lateinit var fakeRepo: FakeDiscoveryServerRepository

    @Before
    fun setUp() {
        fakeRepo = FakeDiscoveryServerRepository()
    }

    // ---- US1: add-server normalization and default-port persistence ----

    @Test
    fun `addServer with blank port saves with default port 5959`() = runTest {
        fakeRepo.addServer("ndi-server.local", "")
        val servers = fakeRepo.observeServers().first()
        assertEquals(1, servers.size)
        assertEquals(DEFAULT_DISCOVERY_SERVER_PORT, servers[0].port)
    }

    @Test
    fun `addServer with explicit valid port saves that port`() = runTest {
        fakeRepo.addServer("ndi-server.local", "7777")
        val servers = fakeRepo.observeServers().first()
        assertEquals(7777, servers[0].port)
    }

    @Test
    fun `addServer trims leading and trailing whitespace from hostname`() = runTest {
        fakeRepo.addServer("  ndi-server.local  ", "")
        val servers = fakeRepo.observeServers().first()
        assertEquals("ndi-server.local", servers[0].hostOrIp)
    }

    @Test(expected = IllegalArgumentException::class)
    fun `addServer with blank hostname throws IllegalArgumentException`() = runTest {
        fakeRepo.addServer("", "5959")
    }

    @Test(expected = IllegalArgumentException::class)
    fun `addServer with invalid port string throws IllegalArgumentException`() = runTest {
        fakeRepo.addServer("ndi-server.local", "notaport")
    }

    @Test(expected = IllegalArgumentException::class)
    fun `addServer with port out of range throws IllegalArgumentException`() = runTest {
        fakeRepo.addServer("ndi-server.local", "99999")
    }

    // ---- US2: duplicate rejection ----

    @Test(expected = IllegalArgumentException::class)
    fun `addServer with duplicate host and port throws IllegalArgumentException`() = runTest {
        fakeRepo.addServer("ndi-server.local", "5959")
        fakeRepo.addServer("ndi-server.local", "5959")
    }

    @Test
    fun `addServer same host different port is allowed`() = runTest {
        fakeRepo.addServer("ndi-server.local", "5959")
        fakeRepo.addServer("ndi-server.local", "5960")
        val servers = fakeRepo.observeServers().first()
        assertEquals(2, servers.size)
    }

    // ---- US2: ordered multi-entry persistence ----

    @Test
    fun `addServer multiple servers appear in insertion order by default`() = runTest {
        fakeRepo.addServer("alpha.local", "")
        fakeRepo.addServer("beta.local", "5960")
        fakeRepo.addServer("gamma.local", "6000")
        val servers = fakeRepo.observeServers().first()
        assertEquals(3, servers.size)
        assertEquals("alpha.local", servers[0].hostOrIp)
        assertEquals("beta.local", servers[1].hostOrIp)
        assertEquals("gamma.local", servers[2].hostOrIp)
    }

    @Test
    fun `reorderServers persists new order`() = runTest {
        fakeRepo.addServer("alpha.local", "")
        fakeRepo.addServer("beta.local", "")
        val before = fakeRepo.observeServers().first()
        val ids = before.map { it.id }.reversed()
        fakeRepo.reorderServers(ids)
        val after = fakeRepo.observeServers().first()
        assertEquals(ids[0], after[0].id)
        assertEquals(ids[1], after[1].id)
    }

    // ---- US4: edit and delete ----

    @Test
    fun `updateServer changes host and port`() = runTest {
        val entry = fakeRepo.addServer("old.local", "5959")
        fakeRepo.updateServer(entry.id, "new.local", "7000")
        val servers = fakeRepo.observeServers().first()
        assertEquals("new.local", servers[0].hostOrIp)
        assertEquals(7000, servers[0].port)
    }

    @Test(expected = IllegalArgumentException::class)
    fun `updateServer to duplicate host+port throws`() = runTest {
        fakeRepo.addServer("alpha.local", "5959")
        val beta = fakeRepo.addServer("beta.local", "5960")
        fakeRepo.updateServer(beta.id, "alpha.local", "5959")
    }

    @Test(expected = NoSuchElementException::class)
    fun `updateServer with unknown id throws NoSuchElementException`() = runTest {
        fakeRepo.updateServer("nonexistent-id", "host.local", "5959")
    }

    @Test
    fun `removeServer deletes the entry`() = runTest {
        val entry = fakeRepo.addServer("remove-me.local", "")
        fakeRepo.removeServer(entry.id)
        val servers = fakeRepo.observeServers().first()
        assertEquals(0, servers.size)
    }

    // ---- US3: toggle persistence ----

    @Test
    fun `setServerEnabled false persists disabled state`() = runTest {
        val entry = fakeRepo.addServer("toggle-me.local", "")
        fakeRepo.setServerEnabled(entry.id, false)
        val servers = fakeRepo.observeServers().first()
        assertEquals(false, servers[0].enabled)
    }

    @Test
    fun `setServerEnabled true re-enables server`() = runTest {
        val entry = fakeRepo.addServer("toggle-me.local", "")
        fakeRepo.setServerEnabled(entry.id, false)
        fakeRepo.setServerEnabled(entry.id, true)
        val servers = fakeRepo.observeServers().first()
        assertEquals(true, servers[0].enabled)
    }

    // ---- US3: NO_ENABLED_SERVERS result ----

    @Test
    fun `resolveActiveDiscoveryTarget with no enabled servers returns NO_ENABLED_SERVERS`() = runTest {
        val entry = fakeRepo.addServer("host.local", "")
        fakeRepo.setServerEnabled(entry.id, false)
        val result = fakeRepo.resolveActiveDiscoveryTarget()
        assertEquals(com.ndi.core.model.DiscoverySelectionOutcome.NO_ENABLED_SERVERS, result.result)
    }

    @Test
    fun `resolveActiveDiscoveryTarget with enabled servers returns SUCCESS`() = runTest {
        fakeRepo.addServer("host.local", "")
        val result = fakeRepo.resolveActiveDiscoveryTarget()
        assertEquals(com.ndi.core.model.DiscoverySelectionOutcome.SUCCESS, result.result)
        assertNotNull(result.selectedEntryId)
    }
}
