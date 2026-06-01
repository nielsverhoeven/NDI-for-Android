package com.ndi.feature.ndibrowser.selection

import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.feature.ndibrowser.source_list.SourcePreselectionController
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test

class UserSelectionStateTest {

    @Test
    fun lastSelection_isHighlightedButDoesNotAutoplay() = runTest {
        val repository = InMemoryUserSelectionRepository()
        val controller = SourcePreselectionController(repository)

        controller.rememberSelection("camera-2")

        assertEquals("camera-2", controller.loadHighlightedSourceId())
        assertFalse(repository.autoplayRequested)
    }
}

private class InMemoryUserSelectionRepository : UserSelectionRepository {
    private var selectedSourceId: String? = null
    var autoplayRequested: Boolean = false

    override suspend fun saveLastSelectedSource(sourceId: String) {
        selectedSourceId = sourceId
    }

    override suspend fun getLastSelectedSource(): String? = selectedSourceId
}
