package com.ndi.feature.ndibrowser.data

import com.ndi.core.database.UserSelectionDao
import com.ndi.core.database.UserSelectionEntity
import com.ndi.feature.ndibrowser.data.repository.UserSelectionRepositoryImpl
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Test

class UserSelectionRepositoryContractTest {

    @Test
    fun saveLastSelectedSource_persistsAndReturnsStoredValue() = runTest {
        val dao = InMemoryUserSelectionDao()
        val repository = UserSelectionRepositoryImpl(dao)

        repository.saveLastSelectedSource("camera-7")

        assertEquals("camera-7", repository.getLastSelectedSource())
    }
}

private class InMemoryUserSelectionDao : UserSelectionDao {
    private var value: UserSelectionEntity? = null

    override suspend fun getSelection(): UserSelectionEntity? = value

    override suspend fun upsert(selection: UserSelectionEntity) {
        value = selection
    }
}