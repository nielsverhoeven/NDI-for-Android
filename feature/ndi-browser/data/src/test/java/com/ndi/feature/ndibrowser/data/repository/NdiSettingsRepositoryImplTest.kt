package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.DiscoveryServerDao
import com.ndi.core.database.DiscoveryServerEntity
import com.ndi.core.database.SettingsPreferenceDao
import com.ndi.core.database.SettingsPreferenceEntity
import com.ndi.core.model.NdiDiscoveryEndpoint
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class NdiSettingsRepositoryImplTest {

    @Test
    fun parse_hostname_withoutPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("ndi-server.local")
        assertNotNull(parsed)
        assertEquals("ndi-server.local", parsed?.host)
        assertNull(parsed?.port)
        assertTrue(parsed?.usesDefaultPort == true)
    }

    @Test
    fun parse_ipv4_withoutPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("192.168.1.10")
        assertNotNull(parsed)
        assertEquals("192.168.1.10", parsed?.host)
        assertNull(parsed?.port)
    }

    @Test
    fun parse_ipv4_withPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("192.168.1.10:7000")
        assertNotNull(parsed)
        assertEquals("192.168.1.10", parsed?.host)
        assertEquals(7000, parsed?.port)
    }

    @Test
    fun parse_ipv6Bracketed_withoutPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("[::1]")
        assertNotNull(parsed)
        assertEquals("::1", parsed?.host)
        assertNull(parsed?.port)
        assertTrue(parsed?.usesDefaultPort == true)
    }

    @Test
    fun parse_ipv6Bracketed_withPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("[::1]:5960")
        assertNotNull(parsed)
        assertEquals("::1", parsed?.host)
        assertEquals(5960, parsed?.port)
    }

    @Test
    fun parse_trimsWhitespace() {
        val parsed = NdiDiscoveryEndpoint.parse("  ndi-server.local  ")
        assertNotNull(parsed)
        assertEquals("ndi-server.local", parsed?.host)
    }

    @Test
    fun parse_emptyString_returnsNull() {
        assertNull(NdiDiscoveryEndpoint.parse(""))
    }

    @Test
    fun parse_blankString_returnsNull() {
        assertNull(NdiDiscoveryEndpoint.parse("  "))
    }

    @Test
    fun parse_nullInput_returnsNull() {
        assertNull(NdiDiscoveryEndpoint.parse(null))
    }

    @Test
    fun parse_unbracketedIpv6WithPort_isRejected() {
        assertNull(NdiDiscoveryEndpoint.parse("::1:5960"))
    }

    @Test
    fun parse_invalidNegativePort_isRejected() {
        assertNull(NdiDiscoveryEndpoint.parse("host:-1"))
    }

    @Test
    fun parse_outOfRangePort_isRejected() {
        assertNull(NdiDiscoveryEndpoint.parse("host:65536"))
    }

    @Test
    fun parse_zeroPort_isRejected() {
        assertNull(NdiDiscoveryEndpoint.parse("host:0"))
    }

    @Test
    fun parse_maxPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("host:65535")
        assertNotNull(parsed)
        assertEquals(65535, parsed?.port)
    }

    @Test
    fun parse_minPort_isValid() {
        val parsed = NdiDiscoveryEndpoint.parse("host:1")
        assertNotNull(parsed)
        assertEquals(1, parsed?.port)
    }

    @Test
    fun isValidPort_acceptsLowerBound() {
        assertTrue(NdiDiscoveryEndpoint.isValidPort(1))
    }

    @Test
    fun isValidPort_acceptsUpperBound() {
        assertTrue(NdiDiscoveryEndpoint.isValidPort(65535))
    }

    @Test
    fun isValidPort_rejectsZero() {
        assertFalse(NdiDiscoveryEndpoint.isValidPort(0))
    }

    @Test
    fun isValidPort_rejectsAboveUpperBound() {
        assertFalse(NdiDiscoveryEndpoint.isValidPort(65536))
    }

    @Test
    fun init_migratesLegacyDiscoveryInput_once_and_clearsIt() = runTest {
        val dispatcher = StandardTestDispatcher(testScheduler)
        val settingsDao = FakeSettingsPreferenceDao(
            SettingsPreferenceEntity(
                id = 1,
                discoveryServerInput = "192.168.2.23:5960",
                developerModeEnabled = false,
                themeMode = "SYSTEM",
                accentColorId = "accent_teal",
                updatedAtEpochMillis = 0L,
            ),
        )
        val discoveryDao = FakeDiscoveryServerDao()

        NdiSettingsRepositoryImpl(
            settingsDao = settingsDao,
            discoveryServerDao = discoveryDao,
            scope = kotlinx.coroutines.CoroutineScope(dispatcher),
        )
        advanceUntilIdle()

        assertEquals(1, discoveryDao.getAll().size)
        assertEquals("192.168.2.23", discoveryDao.getAll().first().hostOrIp)
        assertNull(settingsDao.get()?.discoveryServerInput)
    }

    @Test
    fun init_withExistingDiscoveryServers_clearsLegacyInput_withoutReaddingServer() = runTest {
        val dispatcher = StandardTestDispatcher(testScheduler)
        val settingsDao = FakeSettingsPreferenceDao(
            SettingsPreferenceEntity(
                id = 1,
                discoveryServerInput = "192.168.2.23:5960",
                developerModeEnabled = false,
                themeMode = "SYSTEM",
                accentColorId = "accent_teal",
                updatedAtEpochMillis = 0L,
            ),
        )
        val discoveryDao = FakeDiscoveryServerDao(
            initial = listOf(
                DiscoveryServerEntity(
                    id = "existing",
                    hostOrIp = "10.0.0.5",
                    port = 5959,
                    enabled = true,
                    orderIndex = 0,
                    createdAtEpochMillis = 1L,
                    updatedAtEpochMillis = 1L,
                ),
            ),
        )

        NdiSettingsRepositoryImpl(
            settingsDao = settingsDao,
            discoveryServerDao = discoveryDao,
            scope = kotlinx.coroutines.CoroutineScope(dispatcher),
        )
        advanceUntilIdle()

        assertEquals(1, discoveryDao.getAll().size)
        assertEquals("10.0.0.5", discoveryDao.getAll().first().hostOrIp)
        assertNull(settingsDao.get()?.discoveryServerInput)
    }

    @Test
    fun init_legacyMigration_preservesExistingSettingsAndDiscoveryRows() = runTest {
        val dispatcher = StandardTestDispatcher(testScheduler)
        val settingsDao = FakeSettingsPreferenceDao(
            SettingsPreferenceEntity(
                id = 1,
                discoveryServerInput = "old-server.local:5960",
                developerModeEnabled = true,
                themeMode = "DARK",
                accentColorId = "accent_coral",
                updatedAtEpochMillis = 1234L,
            ),
        )
        val discoveryDao = FakeDiscoveryServerDao(
            initial = listOf(
                DiscoveryServerEntity(
                    id = "existing-a",
                    hostOrIp = "10.1.0.8",
                    port = 5959,
                    enabled = true,
                    orderIndex = 0,
                    createdAtEpochMillis = 10L,
                    updatedAtEpochMillis = 20L,
                ),
            ),
        )

        NdiSettingsRepositoryImpl(
            settingsDao = settingsDao,
            discoveryServerDao = discoveryDao,
            scope = kotlinx.coroutines.CoroutineScope(dispatcher),
        )
        advanceUntilIdle()

        val persistedSettings = settingsDao.get()!!
        assertEquals(1, discoveryDao.getAll().size)
        assertEquals("existing-a", discoveryDao.getAll().first().id)
        assertNull(persistedSettings.discoveryServerInput)
        assertTrue(persistedSettings.developerModeEnabled)
        assertEquals("DARK", persistedSettings.themeMode)
        assertEquals("accent_coral", persistedSettings.accentColorId)
    }
}

private class FakeSettingsPreferenceDao(
    initial: SettingsPreferenceEntity? = null,
) : SettingsPreferenceDao {
    private var entity: SettingsPreferenceEntity? = initial
    private val state = MutableStateFlow(entity)

    override suspend fun get(): SettingsPreferenceEntity? = entity

    override fun observe(): Flow<SettingsPreferenceEntity?> = state

    override suspend fun upsert(entity: SettingsPreferenceEntity) {
        this.entity = entity
        state.value = entity
    }
}

private class FakeDiscoveryServerDao(
    initial: List<DiscoveryServerEntity> = emptyList(),
) : DiscoveryServerDao {
    private val entities = MutableStateFlow(initial)

    override fun observeAll(): Flow<List<DiscoveryServerEntity>> = entities

    override suspend fun getAll(): List<DiscoveryServerEntity> = entities.value

    override suspend fun insert(entity: DiscoveryServerEntity) {
        entities.value = entities.value + entity
    }

    override suspend fun update(entity: DiscoveryServerEntity) {
        entities.value = entities.value.map { existing -> if (existing.id == entity.id) entity else existing }
    }

    override suspend fun deleteById(id: String) {
        entities.value = entities.value.filterNot { it.id == id }
    }

    override suspend fun getById(id: String): DiscoveryServerEntity? = entities.value.firstOrNull { it.id == id }

    override suspend fun countDuplicates(hostOrIp: String, port: Int, excludeId: String): Int {
        return entities.value.count { it.hostOrIp == hostOrIp && it.port == port && it.id != excludeId }
    }

    override suspend fun getMaxOrderIndex(): Int? = entities.value.maxOfOrNull { it.orderIndex }
}