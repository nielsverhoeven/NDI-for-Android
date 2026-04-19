package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.CachedSourceRecord
import com.ndi.core.model.CachedSourceValidationState
import com.ndi.core.model.OutputState
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.navigation.HomeDashboardSnapshot
import com.ndi.feature.ndibrowser.domain.repository.CachedSourceRepository
import com.ndi.feature.ndibrowser.domain.repository.HomeDashboardRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flowOf

/**
 * Aggregates non-sensitive status from output and viewer repositories to produce a Home
 * dashboard snapshot. Does not initiate any playback or output side effects.
 */
class HomeDashboardRepositoryImpl(
    private val outputRepository: NdiOutputRepository,
    private val viewerRepository: NdiViewerRepository,
    private val userSelectionRepository: UserSelectionRepository,
    private val cachedSourceRepository: CachedSourceRepository? = null,
) : HomeDashboardRepository {

    override fun observeDashboardSnapshot(): Flow<HomeDashboardSnapshot> {
        val cachedSourcesFlow: Flow<List<CachedSourceRecord>> =
            cachedSourceRepository?.observeCachedSources()
                ?: flowOf(emptyList())

        return combine(
            outputRepository.observeOutputSession(),
            viewerRepository.observeViewerSession(),
            cachedSourcesFlow,
        ) { outputSession, viewerSession, cachedSources: List<CachedSourceRecord> ->
            val selectedSourceId = viewerSession.selectedSourceId.ifBlank { null }
            HomeDashboardSnapshot(
                generatedAtEpochMillis = System.currentTimeMillis(),
                streamStatus = outputSession.state,
                streamSourceId = outputSession.inputSourceId.ifBlank { null },
                selectedViewSourceId = selectedSourceId,
                selectedViewSourceDisplayName = selectedSourceId,
                viewPlaybackStatus = viewerSession.playbackState,
                canNavigateToStream = true,
                canNavigateToView = resolveCanNavigateToView(selectedSourceId, cachedSources),
            )
        }
    }

    override suspend fun refreshDashboardSnapshot(): HomeDashboardSnapshot {
        val lastSelectedSourceId = userSelectionRepository.getLastSelectedSource()
        val cachedSources = cachedSourceRepository?.observeCachedSources()?.first() ?: emptyList()
        return HomeDashboardSnapshot(
            generatedAtEpochMillis = System.currentTimeMillis(),
            streamStatus = OutputState.READY,
            streamSourceId = null,
            selectedViewSourceId = lastSelectedSourceId,
            selectedViewSourceDisplayName = lastSelectedSourceId,
            viewPlaybackStatus = PlaybackState.STOPPED,
            canNavigateToStream = true,
            canNavigateToView = resolveCanNavigateToView(lastSelectedSourceId, cachedSources),
        )
    }

    private fun resolveCanNavigateToView(
        sourceId: String?,
        cachedSources: List<CachedSourceRecord>,
    ): Boolean {
        val selectedSourceId = sourceId?.takeIf { it.isNotBlank() } ?: return true
        val cached = cachedSources.firstOrNull { it.cacheKey == selectedSourceId || it.lastObservedSourceId == selectedSourceId }

        return when (cached?.validationState) {
            CachedSourceValidationState.AVAILABLE -> true
            CachedSourceValidationState.VALIDATING,
            CachedSourceValidationState.UNAVAILABLE,
            CachedSourceValidationState.NOT_YET_VALIDATED,
            null,
            -> false
        }
    }
}

