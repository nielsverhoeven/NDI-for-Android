package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.PlaybackState
import com.ndi.core.model.navigation.ViewContinuityState
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.feature.ndibrowser.domain.repository.ViewContinuityRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * Tracks View/playback continuity when navigating between top-level destinations.
 *
 * Key invariants:
 * - Leaving View via top-level navigation MUST stop playback immediately.
 * - Selected source remains for no-autoplay restore on return.
 * - autoplayPermitted is always false in this feature version.
 */
class ViewContinuityRepositoryImpl(
    private val viewerRepository: NdiViewerRepository,
    private val userSelectionRepository: UserSelectionRepository,
) : ViewContinuityRepository {

    private val _state = MutableStateFlow(
        ViewContinuityState(
            playbackState = PlaybackState.STOPPED,
            autoplayPermitted = false,
        ),
    )

    override fun observeContinuityState(): Flow<ViewContinuityState> = _state.asStateFlow()

    override suspend fun stopForTopLevelNavigation() {
        viewerRepository.stopViewing()
        _state.value = _state.value.copy(
            playbackState = PlaybackState.STOPPED,
            stoppedByTopLevelNavigation = true,
            autoplayPermitted = false,
        )
    }

    override suspend fun getLastSelectedSourceId(): String? =
        userSelectionRepository.getLastSelectedSource()
}

