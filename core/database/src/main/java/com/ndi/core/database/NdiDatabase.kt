package com.ndi.core.database

import android.content.Context
import androidx.room.Dao
import androidx.room.Database
import androidx.room.Entity
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.PrimaryKey
import androidx.room.Query
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.Update
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase
import kotlinx.coroutines.flow.Flow

@Entity(tableName = "user_selection_state")
data class UserSelectionEntity(
    @PrimaryKey
    val id: Int = 1,
    val lastSelectedSourceId: String?,
    val lastSelectedAtEpochMillis: Long?,
    val shouldAutoplayOnLaunch: Boolean = false,
)

@Entity(tableName = "viewer_session")
data class ViewerSessionEntity(
    @PrimaryKey
    val sessionId: String,
    val selectedSourceId: String,
    val playbackState: String,
    val interruptionReason: String?,
    val retryWindowSeconds: Int,
    val retryAttempts: Int,
    val startedAtEpochMillis: Long,
    val endedAtEpochMillis: Long?,
)

@Entity(tableName = "output_configuration")
data class OutputConfigurationEntity(
    @PrimaryKey
    val id: Int = 1,
    val preferredStreamName: String,
    val lastSelectedInputSourceId: String?,
    val lastSelectedInputSourceKind: String?,
    val autoRetryEnabled: Boolean = true,
    val retryWindowSeconds: Int = 15,
    val updatedAtEpochMillis: Long,
)

@Entity(tableName = "output_session")
data class OutputSessionEntity(
    @PrimaryKey
    val sessionId: String,
    val inputSourceId: String,
    val inputSourceKind: String,
    val outboundStreamName: String,
    val consentState: String,
    val state: String,
    val startedAtEpochMillis: Long,
    val stoppedAtEpochMillis: Long?,
    val interruptionReason: String?,
    val retryAttempts: Int,
    val hostInstanceId: String,
)

@Dao
interface UserSelectionDao {
    @Query("SELECT * FROM user_selection_state WHERE id = 1")
    suspend fun getSelection(): UserSelectionEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(selection: UserSelectionEntity)
}

@Dao
interface ViewerSessionDao {
    @Query("SELECT * FROM viewer_session ORDER BY startedAtEpochMillis DESC LIMIT 1")
    suspend fun getLatest(): ViewerSessionEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(session: ViewerSessionEntity)
}

@Dao
interface OutputConfigurationDao {
    @Query("SELECT * FROM output_configuration WHERE id = 1")
    suspend fun get(): OutputConfigurationEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(configuration: OutputConfigurationEntity)
}

@Dao
interface OutputSessionDao {
    @Query("SELECT * FROM output_session ORDER BY startedAtEpochMillis DESC LIMIT 1")
    suspend fun getLatest(): OutputSessionEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(session: OutputSessionEntity)
}

@Entity(tableName = "settings_preference")
data class SettingsPreferenceEntity(
    @PrimaryKey
    val id: Int = 1,
    val discoveryServerInput: String?,
    val developerModeEnabled: Boolean,
    val themeMode: String = "SYSTEM",
    val accentColorId: String = "accent_teal",
    val updatedAtEpochMillis: Long,
)

@Dao
interface SettingsPreferenceDao {
    @Query("SELECT * FROM settings_preference WHERE id = 1")
    suspend fun get(): SettingsPreferenceEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: SettingsPreferenceEntity)
}

// ---- Spec 018: Discovery server persistence ----

@Entity(tableName = "discovery_servers")
data class DiscoveryServerEntity(
    @PrimaryKey
    val id: String,
    val hostOrIp: String,
    val port: Int,
    val enabled: Boolean,
    val orderIndex: Int,
    val createdAtEpochMillis: Long,
    val updatedAtEpochMillis: Long,
)

@Dao
interface DiscoveryServerDao {
    /** Observe all servers ordered by orderIndex. Emits on every change. */
    @Query("SELECT * FROM discovery_servers ORDER BY orderIndex ASC")
    fun observeAll(): Flow<List<DiscoveryServerEntity>>

    /** Return all servers ordered by orderIndex (suspend, one-shot). */
    @Query("SELECT * FROM discovery_servers ORDER BY orderIndex ASC")
    suspend fun getAll(): List<DiscoveryServerEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insert(entity: DiscoveryServerEntity)

    @Update
    suspend fun update(entity: DiscoveryServerEntity)

    @Query("DELETE FROM discovery_servers WHERE id = :id")
    suspend fun deleteById(id: String)

    @Query("SELECT * FROM discovery_servers WHERE id = :id LIMIT 1")
    suspend fun getById(id: String): DiscoveryServerEntity?

    /** Check for duplicate (hostOrIp + port) excluding a specific id (useful for update). */
    @Query(
        "SELECT COUNT(*) FROM discovery_servers WHERE hostOrIp = :hostOrIp AND port = :port AND id != :excludeId"
    )
    suspend fun countDuplicates(hostOrIp: String, port: Int, excludeId: String = ""): Int

    @Query("SELECT MAX(orderIndex) FROM discovery_servers")
    suspend fun getMaxOrderIndex(): Int?
}

@Database(
    entities = [
        UserSelectionEntity::class,
        ViewerSessionEntity::class,
        OutputConfigurationEntity::class,
        OutputSessionEntity::class,
        SettingsPreferenceEntity::class,
        DiscoveryServerEntity::class,
    ],
    version = 6,
    exportSchema = false,
)
abstract class NdiDatabase : RoomDatabase() {

    abstract fun userSelectionDao(): UserSelectionDao

    abstract fun viewerSessionDao(): ViewerSessionDao

    abstract fun outputConfigurationDao(): OutputConfigurationDao

    abstract fun outputSessionDao(): OutputSessionDao

    abstract fun settingsPreferenceDao(): SettingsPreferenceDao

    abstract fun discoveryServerDao(): DiscoveryServerDao

    companion object {
        private fun hasColumn(database: SupportSQLiteDatabase, tableName: String, columnName: String): Boolean {
            database.query("PRAGMA table_info($tableName)").use { cursor ->
                val nameColumnIndex = cursor.getColumnIndex("name")
                if (nameColumnIndex == -1) {
                    return false
                }

                while (cursor.moveToNext()) {
                    if (cursor.getString(nameColumnIndex) == columnName) {
                        return true
                    }
                }
            }

            return false
        }

        val MIGRATION_1_2 = object : Migration(1, 2) {
            override fun migrate(database: SupportSQLiteDatabase) {
                database.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS output_configuration (
                        id INTEGER NOT NULL,
                        preferredStreamName TEXT NOT NULL,
                        lastSelectedInputSourceId TEXT,
                        lastSelectedInputSourceKind TEXT,
                        autoRetryEnabled INTEGER NOT NULL,
                        retryWindowSeconds INTEGER NOT NULL,
                        updatedAtEpochMillis INTEGER NOT NULL,
                        PRIMARY KEY(id)
                    )
                    """.trimIndent(),
                )
                database.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS output_session (
                        sessionId TEXT NOT NULL,
                        inputSourceId TEXT NOT NULL,
                        inputSourceKind TEXT NOT NULL,
                        outboundStreamName TEXT NOT NULL,
                        consentState TEXT NOT NULL,
                        state TEXT NOT NULL,
                        startedAtEpochMillis INTEGER NOT NULL,
                        stoppedAtEpochMillis INTEGER,
                        interruptionReason TEXT,
                        retryAttempts INTEGER NOT NULL,
                        hostInstanceId TEXT NOT NULL,
                        PRIMARY KEY(sessionId)
                    )
                    """.trimIndent(),
                )
            }
        }

        val MIGRATION_2_3 = object : Migration(2, 3) {
            override fun migrate(database: SupportSQLiteDatabase) {
                if (!hasColumn(database, "output_configuration", "lastSelectedInputSourceKind")) {
                    database.execSQL("ALTER TABLE output_configuration ADD COLUMN lastSelectedInputSourceKind TEXT")
                }

                if (!hasColumn(database, "output_configuration", "autoRetryEnabled")) {
                    database.execSQL("ALTER TABLE output_configuration ADD COLUMN autoRetryEnabled INTEGER NOT NULL DEFAULT 1")
                }

                if (!hasColumn(database, "output_session", "inputSourceKind")) {
                    database.execSQL("ALTER TABLE output_session ADD COLUMN inputSourceKind TEXT NOT NULL DEFAULT 'DISCOVERED_NDI'")
                }

                if (!hasColumn(database, "output_session", "consentState")) {
                    database.execSQL("ALTER TABLE output_session ADD COLUMN consentState TEXT NOT NULL DEFAULT 'NOT_REQUIRED'")
                }
            }
        }

        val MIGRATION_3_4 = object : Migration(3, 4) {
            override fun migrate(database: SupportSQLiteDatabase) {
                database.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS settings_preference (
                        id INTEGER NOT NULL,
                        discoveryServerInput TEXT,
                        developerModeEnabled INTEGER NOT NULL DEFAULT 0,
                        themeMode TEXT NOT NULL DEFAULT 'SYSTEM',
                        accentColorId TEXT NOT NULL DEFAULT 'accent_teal',
                        updatedAtEpochMillis INTEGER NOT NULL DEFAULT 0,
                        PRIMARY KEY(id)
                    )
                    """.trimIndent(),
                )
            }
        }

        val MIGRATION_4_5 = object : Migration(4, 5) {
            override fun migrate(database: SupportSQLiteDatabase) {
                if (!hasColumn(database, "settings_preference", "themeMode")) {
                    database.execSQL("ALTER TABLE settings_preference ADD COLUMN themeMode TEXT NOT NULL DEFAULT 'SYSTEM'")
                }

                if (!hasColumn(database, "settings_preference", "accentColorId")) {
                    database.execSQL("ALTER TABLE settings_preference ADD COLUMN accentColorId TEXT NOT NULL DEFAULT 'accent_teal'")
                }
            }
        }

        val MIGRATION_5_6 = object : Migration(5, 6) {
            override fun migrate(database: SupportSQLiteDatabase) {
                database.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS discovery_servers (
                        id TEXT NOT NULL,
                        hostOrIp TEXT NOT NULL,
                        port INTEGER NOT NULL,
                        enabled INTEGER NOT NULL DEFAULT 1,
                        orderIndex INTEGER NOT NULL DEFAULT 0,
                        createdAtEpochMillis INTEGER NOT NULL DEFAULT 0,
                        updatedAtEpochMillis INTEGER NOT NULL DEFAULT 0,
                        PRIMARY KEY(id)
                    )
                    """.trimIndent(),
                )
            }
        }

        @Volatile
        private var instance: NdiDatabase? = null

        fun getInstance(context: Context): NdiDatabase {
            return instance ?: synchronized(this) {
                instance ?: Room.databaseBuilder(
                    context.applicationContext,
                    NdiDatabase::class.java,
                    "ndi_database",
                )
                    .addMigrations(MIGRATION_1_2, MIGRATION_2_3, MIGRATION_3_4, MIGRATION_4_5, MIGRATION_5_6)
                    .build()
                    .also { instance = it }
            }
        }
    }
}
