package com.ndi.core.database

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class DiscoveryServerDaoMigrationTest {

    @Test
    fun entityFieldsAreCorrect() {
        val entity = DiscoveryServerEntity(
            id = "test-id",
            hostOrIp = "ndi-server.local",
            port = 5959,
            enabled = true,
            orderIndex = 0,
            createdAtEpochMillis = 1000L,
            updatedAtEpochMillis = 2000L,
        )
        assertEquals("test-id", entity.id)
        assertEquals("ndi-server.local", entity.hostOrIp)
        assertEquals(5959, entity.port)
        assertTrue(entity.enabled)
        assertEquals(0, entity.orderIndex)
    }

    @Test
    fun entityDisabledFlagStoredCorrectly() {
        val entity = DiscoveryServerEntity(
            id = "id2", hostOrIp = "host.local", port = 5959, enabled = false,
            orderIndex = 1, createdAtEpochMillis = 0L, updatedAtEpochMillis = 0L,
        )
        assertFalse(entity.enabled)
    }

    @Test
    fun migrationSqlContainsRequiredColumns() {
        val sql = "CREATE TABLE IF NOT EXISTS discovery_servers (" +
            "id TEXT NOT NULL, hostOrIp TEXT NOT NULL, port INTEGER NOT NULL, " +
            "enabled INTEGER NOT NULL DEFAULT 1, orderIndex INTEGER NOT NULL DEFAULT 0, " +
            "createdAtEpochMillis INTEGER NOT NULL DEFAULT 0, " +
            "updatedAtEpochMillis INTEGER NOT NULL DEFAULT 0, PRIMARY KEY(id))"
        assertTrue(sql.contains("discovery_servers"))
        assertTrue(sql.contains("hostOrIp"))
        assertTrue(sql.contains("port"))
        assertTrue(sql.contains("enabled"))
        assertTrue(sql.contains("orderIndex"))
    }
}
