package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.OutputState
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.navigation.HomeDashboardSnapshot
import com.ndi.feature.ndibrowser.domain.repository.HomeDashboardRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine

/**
 * Aggregates non-sensitive status from output and viewer repositories to produce a Home
 * dashboard snapshot. Does not initiate any playback or output side effects.
 */
class HomeDashboardRepositoryImpl(
    private val outputRepository: NdiOutputRepository,
    private val viewerRepository: NdiViewerRepository,
    private val userSelectionRepository: UserSelectionRepository,
) : HomeDashboardRepository {

    private val _snapshotState = MutableStateFlow(buildSnapshot())

    override fun observeDashboardSnapshot(): Flow<HomeDashboardSnapshot> {
        return combine(
            outputRepository.observeOutputSession(),
            viewerRepository.observeViewerSession(),
        ) { outputSession, viewerSession ->
            HomeDashboardSnapshot(
                generatedAtEpochMillis = System.currentTimeMillis(),
                streamStatus = outputSession.state,
                streamSourceId = outputSession.inputSourceId.ifBlank { null },
                selectedViewSourceId = viewerSession.selectedSourceId.ifBlank { null },
                selectedViewSourceDisplayName = viewerSession.selectedSourceId.ifBlank { null },
                viewPlaybackStatus = viewerSession.playbackState,
                canNavigateToStream = true,
                canNavigateToView = true,
            )
        }
    }

    override suspend fun refreshDashboardSnapshot(): HomeDashboardSnapshot {
        val lastSelectedSourceId = userSelectionRepository.getLastSelectedSource()
        val snapshot = HomeDashboardSnapshot(
            generatedAtEpochMillis = System.currentTimeMillis(),
            streamStatus = OutputState.READY,
            streamSourceId = null,
            selectedViewSourceId = lastSelectedSourceId,
            selectedViewSourceDisplayName = lastSelectedSourceId,
            viewPlaybackStatus = PlaybackState.STOPPED,
            canNavigateToStream = true,
            canNavigateToView = true,
        )
        _snapshotState.value = snapshot
        return snapshot
    }

    private fun buildSnapshot(): HomeDashboardSnapshot = HomeDashboardSnapshot(
        generatedAtEpochMillis = System.currentTimeMillis(),
        streamStatus = OutputState.READY,
        viewPlaybackStatus = PlaybackState.STOPPED,
    )
}

