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
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

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
    val retryWindowSeconds: Int = 15,
    val updatedAtEpochMillis: Long,
)

@Entity(tableName = "output_session")
data class OutputSessionEntity(
    @PrimaryKey
    val sessionId: String,
    val inputSourceId: String,
    val outboundStreamName: String,
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

@Database(
    entities = [
        UserSelectionEntity::class,
        ViewerSessionEntity::class,
        OutputConfigurationEntity::class,
        OutputSessionEntity::class,
    ],
    version = 2,
    exportSchema = false,
)
abstract class NdiDatabase : RoomDatabase() {

    abstract fun userSelectionDao(): UserSelectionDao

    abstract fun viewerSessionDao(): ViewerSessionDao

    abstract fun outputConfigurationDao(): OutputConfigurationDao

    abstract fun outputSessionDao(): OutputSessionDao

    companion object {
        val MIGRATION_1_2 = object : Migration(1, 2) {
            override fun migrate(database: SupportSQLiteDatabase) {
                database.execSQL(
                    """
                    CREATE TABLE IF NOT EXISTS output_configuration (
                        id INTEGER NOT NULL,
                        preferredStreamName TEXT NOT NULL,
                        lastSelectedInputSourceId TEXT,
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
                        outboundStreamName TEXT NOT NULL,
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

        @Volatile
        private var instance: NdiDatabase? = null

        fun getInstance(context: Context): NdiDatabase {
            return instance ?: synchronized(this) {
                instance ?: Room.databaseBuilder(
                    context.applicationContext,
                    NdiDatabase::class.java,
                    "ndi_database",
                )
                    .addMigrations(MIGRATION_1_2)
                    .build()
                    .also { instance = it }
            }
        }
    }
}
