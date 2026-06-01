package com.ndi.feature.ndibrowser.source_list

import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository

class SourcePreselectionController(
    private val userSelectionRepository: UserSelectionRepository,
) {

    suspend fun loadHighlightedSourceId(): String? {
        return userSelectionRepository.getLastSelectedSource()
    }

    suspend fun rememberSelection(sourceId: String) {
        userSelectionRepository.saveLastSelectedSource(sourceId)
    }
}
