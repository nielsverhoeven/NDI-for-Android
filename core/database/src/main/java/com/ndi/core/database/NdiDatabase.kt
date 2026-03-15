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

@Database(
    entities = [UserSelectionEntity::class, ViewerSessionEntity::class],
    version = 1,
    exportSchema = false,
)
abstract class NdiDatabase : RoomDatabase() {

    abstract fun userSelectionDao(): UserSelectionDao

    abstract fun viewerSessionDao(): ViewerSessionDao

    companion object {
        @Volatile
        private var instance: NdiDatabase? = null

        fun getInstance(context: Context): NdiDatabase {
            return instance ?: synchronized(this) {
                instance ?: Room.databaseBuilder(
                    context.applicationContext,
                    NdiDatabase::class.java,
                    "ndi_database",
                ).build().also { instance = it }
            }
        }
    }
}
