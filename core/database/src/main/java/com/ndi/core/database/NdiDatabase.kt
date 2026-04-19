package com.ndi.core.database

import android.content.Context
import androidx.room.Dao
import androidx.room.Database
import androidx.room.Entity
import androidx.room.ForeignKey
import androidx.room.Index
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

@Entity(tableName = "last_viewed_context")
data class LastViewedContextEntity(
    @PrimaryKey
    val contextId: String = "last_viewed_context",
    val sourceId: String,
    val lastFrameImagePath: String?,
    val lastFrameCapturedAtEpochMillis: Long?,
    val restoredAtEpochMillis: Long?,
)

@Entity(tableName = "connection_history_state")
data class ConnectionHistoryStateEntity(
    @PrimaryKey
    val sourceId: String,
    val previouslyConnected: Boolean = true,
    val firstSuccessfulFrameAtEpochMillis: Long,
    val lastSuccessfulFrameAtEpochMillis: Long,
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
interface LastViewedContextDao {
    @Query("SELECT * FROM last_viewed_context WHERE contextId = :contextId LIMIT 1")
    suspend fun get(contextId: String = "last_viewed_context"): LastViewedContextEntity?

    @Query("SELECT * FROM last_viewed_context WHERE contextId = :contextId LIMIT 1")
    fun observe(contextId: String = "last_viewed_context"): Flow<LastViewedContextEntity?>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: LastViewedContextEntity)

    @Query("DELETE FROM last_viewed_context WHERE contextId = :contextId")
    suspend fun clear(contextId: String = "last_viewed_context")
}

@Dao
interface ConnectionHistoryStateDao {
    @Query("SELECT * FROM connection_history_state ORDER BY lastSuccessfulFrameAtEpochMillis DESC")
    fun observeAll(): Flow<List<ConnectionHistoryStateEntity>>

    @Query("SELECT * FROM connection_history_state WHERE sourceId = :sourceId LIMIT 1")
    suspend fun getBySourceId(sourceId: String): ConnectionHistoryStateEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: ConnectionHistoryStateEntity)

    @Query("DELETE FROM connection_history_state")
    suspend fun clearAll()
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

    @Query("SELECT * FROM settings_preference WHERE id = 1")
    fun observe(): Flow<SettingsPreferenceEntity?>

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

// ---- Spec 022: Discovery server check status persistence ----

@Entity(tableName = "discovery_server_check_status")
data class DiscoveryServerCheckStatusEntity(
    @PrimaryKey
    val serverId: String,
    val checkType: String,
    val outcome: String,
    val checkedAtEpochMillis: Long,
    val failureCategory: String,
    val failureMessage: String?,
    val correlationId: String,
)

@Entity(
    tableName = "cached_sources",
    indices = [
        Index(value = ["endpointKey"]),
        Index(value = ["stableSourceId"]),
    ],
)
data class CachedSourceEntity(
    @PrimaryKey
    val cacheKey: String,
    val stableSourceId: String?,
    val lastObservedSourceId: String?,
    val displayName: String,
    val endpointHost: String,
    val endpointPort: Int,
    val endpointKey: String,
    val validationState: String,
    val lastAvailableAtEpochMillis: Long?,
    val lastValidatedAtEpochMillis: Long?,
    val lastValidationStartedAtEpochMillis: Long?,
    val firstCachedAtEpochMillis: Long,
    val lastDiscoveredAtEpochMillis: Long,
    val retainedPreviewImagePath: String?,
    val lastPreviewCapturedAtEpochMillis: Long?,
    val updatedAtEpochMillis: Long,
)

@Entity(
    tableName = "cached_source_discovery_server_xref",
    primaryKeys = ["cacheKey", "discoveryServerId"],
    foreignKeys = [
        ForeignKey(
            entity = CachedSourceEntity::class,
            parentColumns = ["cacheKey"],
            childColumns = ["cacheKey"],
            onDelete = ForeignKey.CASCADE,
        ),
        ForeignKey(
            entity = DiscoveryServerEntity::class,
            parentColumns = ["id"],
            childColumns = ["discoveryServerId"],
            onDelete = ForeignKey.CASCADE,
        ),
    ],
    indices = [
        Index(value = ["cacheKey"]),
        Index(value = ["discoveryServerId"]),
    ],
)
data class CachedSourceDiscoveryServerCrossRefEntity(
    val cacheKey: String,
    val discoveryServerId: String,
    val firstObservedAtEpochMillis: Long,
    val lastObservedAtEpochMillis: Long,
)

@Dao
interface DiscoveryServerCheckStatusDao {
    @Query("SELECT * FROM discovery_server_check_status WHERE serverId = :serverId LIMIT 1")
    suspend fun getByServerId(serverId: String): DiscoveryServerCheckStatusEntity?

    @Query("SELECT * FROM discovery_server_check_status")
    fun observeAll(): Flow<List<DiscoveryServerCheckStatusEntity>>

    @Query("SELECT * FROM discovery_server_check_status WHERE serverId = :serverId LIMIT 1")
    fun observeByServerId(serverId: String): Flow<DiscoveryServerCheckStatusEntity?>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: DiscoveryServerCheckStatusEntity)
}

@Dao
interface CachedSourceDao {
    @Query("SELECT * FROM cached_sources ORDER BY updatedAtEpochMillis DESC")
    fun observeAll(): Flow<List<CachedSourceEntity>>

    @Query("SELECT * FROM cached_sources WHERE cacheKey = :cacheKey LIMIT 1")
    suspend fun getByKey(cacheKey: String): CachedSourceEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: CachedSourceEntity)

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insertIfAbsent(entity: CachedSourceEntity)

    /**
     * Update all discovery-sourced fields while PRESERVING the retained preview image path.
     * Use this from discovery persistence code so thumbnail images are not wiped by a new scan.
     */
    @Query(
        """
        UPDATE cached_sources
        SET lastObservedSourceId = :lastObservedSourceId,
            displayName = :displayName,
            endpointHost = :endpointHost,
            endpointPort = :endpointPort,
            endpointKey = :endpointKey,
            validationState = :validationState,
            lastAvailableAtEpochMillis = COALESCE(:lastAvailableAtEpochMillis, lastAvailableAtEpochMillis),
            lastValidatedAtEpochMillis = :lastValidatedAtEpochMillis,
            lastDiscoveredAtEpochMillis = :lastDiscoveredAtEpochMillis,
            updatedAtEpochMillis = :updatedAtEpochMillis
        WHERE cacheKey = :cacheKey
        """,
    )
    suspend fun updateFromDiscovery(
        cacheKey: String,
        lastObservedSourceId: String?,
        displayName: String,
        endpointHost: String,
        endpointPort: Int,
        endpointKey: String,
        validationState: String,
        lastAvailableAtEpochMillis: Long?,
        lastValidatedAtEpochMillis: Long?,
        lastDiscoveredAtEpochMillis: Long,
        updatedAtEpochMillis: Long,
    )

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertAll(entities: List<CachedSourceEntity>)

    @Query(
        """
        UPDATE cached_sources
        SET validationState = :validationState,
            lastValidationStartedAtEpochMillis = COALESCE(:startedAtEpochMillis, lastValidationStartedAtEpochMillis),
            lastValidatedAtEpochMillis = COALESCE(:validatedAtEpochMillis, lastValidatedAtEpochMillis),
            lastAvailableAtEpochMillis = COALESCE(:availableAtEpochMillis, lastAvailableAtEpochMillis),
            updatedAtEpochMillis = :updatedAtEpochMillis
        WHERE cacheKey = :cacheKey
        """,
    )
    suspend fun updateValidationState(
        cacheKey: String,
        validationState: String,
        startedAtEpochMillis: Long?,
        validatedAtEpochMillis: Long?,
        availableAtEpochMillis: Long?,
        updatedAtEpochMillis: Long,
    )
}

@Dao
interface CachedSourceDiscoveryServerCrossRefDao {
    @Query("SELECT * FROM cached_source_discovery_server_xref WHERE cacheKey = :cacheKey")
    suspend fun getByCacheKey(cacheKey: String): List<CachedSourceDiscoveryServerCrossRefEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(entity: CachedSourceDiscoveryServerCrossRefEntity)

    @Query(
        """
        UPDATE cached_source_discovery_server_xref
        SET lastObservedAtEpochMillis = :lastObservedAtEpochMillis
        WHERE cacheKey = :cacheKey AND discoveryServerId = :discoveryServerId
        """,
    )
    suspend fun updateLastObserved(
        cacheKey: String,
        discoveryServerId: String,
        lastObservedAtEpochMillis: Long,
    )
}

@Database(
    entities = [
        UserSelectionEntity::class,
        ViewerSessionEntity::class,
        LastViewedContextEntity::class,
        ConnectionHistoryStateEntity::class,
        OutputConfigurationEntity::class,
        OutputSessionEntity::class,
        SettingsPreferenceEntity::class,
        DiscoveryServerEntity::class,
        DiscoveryServerCheckStatusEntity::class,
        CachedSourceEntity::class,
        CachedSourceDiscoveryServerCrossRefEntity::class,
    ],
    version = 9,
    exportSchema = false,
)
abstract class NdiDatabase : RoomDatabase() {

    abstract fun userSelectionDao(): UserSelectionDao

    abstract fun viewerSessionDao(): ViewerSessionDao

    abstract fun lastViewedContextDao(): LastViewedContextDao

    abstract fun connectionHistoryStateDao(): ConnectionHistoryStateDao

    abstract fun outputConfigurationDao(): OutputConfigurationDao

    abstract fun outputSessionDao(): OutputSessionDao

    abstract fun settingsPreferenceDao(): SettingsPreferenceDao

    abstract fun discoveryServerDao(): DiscoveryServerDao
    abstract fun discoveryServerCheckStatusDao(): DiscoveryServerCheckStatusDao
    abstract fun cachedSourceDao(): CachedSourceDao
    abstract fun cachedSourceDiscoveryServerCrossRefDao(): CachedSourceDiscoveryServerCrossRefDao


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

        val MIGRATION_6_7 = object : Migration(6, 7) {
            override fun migrate(database: SupportSQLiteDatabase) {
                database.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS last_viewed_context (
                        contextId TEXT NOT NULL,
                        sourceId TEXT NOT NULL,
                        lastFrameImagePath TEXT,
                        lastFrameCapturedAtEpochMillis INTEGER,
                        restoredAtEpochMillis INTEGER,
                        PRIMARY KEY(contextId)
                    )
                    """.trimIndent(),
                )

                database.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS connection_history_state (
                        sourceId TEXT NOT NULL,
                        previouslyConnected INTEGER NOT NULL DEFAULT 1,
                        firstSuccessfulFrameAtEpochMillis INTEGER NOT NULL,
                        lastSuccessfulFrameAtEpochMillis INTEGER NOT NULL,
                        PRIMARY KEY(sourceId)
                    )
                    """.trimIndent(),
                )
            }
        }

        val MIGRATION_7_8 = object : Migration(7, 8) {
            override fun migrate(database: SupportSQLiteDatabase) {
                database.execSQL(
                    
"""
                    CREATE TABLE IF NOT EXISTS discovery_server_check_status (
                        serverId TEXT NOT NULL,
                        checkType TEXT NOT NULL,
                        outcome TEXT NOT NULL,
                        checkedAtEpochMillis INTEGER NOT NULL,
                        failureCategory TEXT NOT NULL,
                        failureMessage TEXT,
                        correlationId TEXT NOT NULL,
                        PRIMARY KEY(serverId)
                    )
                    
""".trimIndent(),
                )
            }
        }

        val MIGRATION_8_9 = object : Migration(8, 9) {
            override fun migrate(database: SupportSQLiteDatabase) {
                database.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS cached_sources (
                        cacheKey TEXT NOT NULL,
                        stableSourceId TEXT,
                        lastObservedSourceId TEXT,
                        displayName TEXT NOT NULL,
                        endpointHost TEXT NOT NULL,
                        endpointPort INTEGER NOT NULL,
                        endpointKey TEXT NOT NULL,
                        validationState TEXT NOT NULL,
                        lastAvailableAtEpochMillis INTEGER,
                        lastValidatedAtEpochMillis INTEGER,
                        lastValidationStartedAtEpochMillis INTEGER,
                        firstCachedAtEpochMillis INTEGER NOT NULL,
                        lastDiscoveredAtEpochMillis INTEGER NOT NULL,
                        retainedPreviewImagePath TEXT,
                        lastPreviewCapturedAtEpochMillis INTEGER,
                        updatedAtEpochMillis INTEGER NOT NULL,
                        PRIMARY KEY(cacheKey)
                    )
                    """.trimIndent(),
                )

                database.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS cached_source_discovery_server_xref (
                        cacheKey TEXT NOT NULL,
                        discoveryServerId TEXT NOT NULL,
                        firstObservedAtEpochMillis INTEGER NOT NULL,
                        lastObservedAtEpochMillis INTEGER NOT NULL,
                        PRIMARY KEY(cacheKey, discoveryServerId),
                        FOREIGN KEY(cacheKey) REFERENCES cached_sources(cacheKey) ON DELETE CASCADE,
                        FOREIGN KEY(discoveryServerId) REFERENCES discovery_servers(id) ON DELETE CASCADE
                    )
                    """.trimIndent(),
                )

                database.execSQL("CREATE INDEX IF NOT EXISTS index_cached_sources_endpointKey ON cached_sources(endpointKey)")
                database.execSQL("CREATE INDEX IF NOT EXISTS index_cached_sources_stableSourceId ON cached_sources(stableSourceId)")
                database.execSQL("CREATE INDEX IF NOT EXISTS index_cached_source_xref_cacheKey ON cached_source_discovery_server_xref(cacheKey)")
                database.execSQL("CREATE INDEX IF NOT EXISTS index_cached_source_xref_discoveryServerId ON cached_source_discovery_server_xref(discoveryServerId)")
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
                    .addMigrations(
                        MIGRATION_1_2,
                        MIGRATION_2_3,
                        MIGRATION_3_4,
                        MIGRATION_4_5,
                        MIGRATION_5_6,
                        MIGRATION_6_7,
                        MIGRATION_7_8,
                        MIGRATION_8_9,
                    )
                    .build()
                    .also { instance = it }
            }
        }
    }
}
