package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.UserSelectionDao
import com.ndi.core.database.UserSelectionEntity
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository

class UserSelectionRepositoryImpl(
    private val userSelectionDao: UserSelectionDao,
) : UserSelectionRepository {

    override suspend fun saveLastSelectedSource(sourceId: String) {
        userSelectionDao.upsert(
            UserSelectionEntity(
                lastSelectedSourceId = sourceId,
                lastSelectedAtEpochMillis = System.currentTimeMillis(),
                shouldAutoplayOnLaunch = false,
            ),
        )
    }

    override suspend fun getLastSelectedSource(): String? {
        return userSelectionDao.getSelection()?.lastSelectedSourceId
    }
}
