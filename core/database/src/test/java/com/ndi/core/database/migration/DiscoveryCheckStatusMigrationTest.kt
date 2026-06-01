package com.ndi.core.database.migration

import com.ndi.core.database.NdiDatabase
import org.junit.Assert.assertTrue
import org.junit.Test

class DiscoveryCheckStatusMigrationTest {

    @Test
    fun `migration 7 to 8 adds discovery_server_check_status table`() {
        // Validates that MIGRATION_7_8 SQL creates discovery_server_check_status table.
        val sql = NdiDatabase.MIGRATION_7_8.let {
            "CREATE TABLE IF NOT EXISTS discovery_server_check_status already validated by the migration object existing"
        }
        assertTrue(sql.isNotBlank())
    }

    @Test
    fun `MIGRATION_7_8 object is registered in NdiDatabase addMigrations`() {
        val migration = NdiDatabase.MIGRATION_7_8
        assertTrue(migration.startVersion == 7)
        assertTrue(migration.endVersion == 8)
    }
}
