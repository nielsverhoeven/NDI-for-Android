package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.OutputState
import com.ndi.core.model.navigation.StreamContinuityState
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.StreamContinuityRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.map

/**
 * Captures Stream/output continuity state when navigating between top-level destinations.
 *
 * Key invariant: active output is NEVER stopped by top-level navigation.
 * After process death, state is contextual only — explicit user action required to restart.
 */
class StreamContinuityRepositoryImpl(
    private val outputRepository: NdiOutputRepository,
) : StreamContinuityRepository {

    private val _state = MutableStateFlow(
        StreamContinuityState(
            hasActiveOutput = false,
            outputState = OutputState.READY,
            autoRestartPermitted = false,
        ),
    )

    override fun observeContinuityState(): Flow<StreamContinuityState> = _state.asStateFlow()

    override suspend fun captureLastKnownState() {
        outputRepository.observeOutputSession()
            .map { session ->
                StreamContinuityState(
                    hasActiveOutput = session.state == OutputState.ACTIVE ||
                        session.state == OutputState.STARTING ||
                        session.state == OutputState.INTERRUPTED,
                    outputState = session.state,
                    lastKnownOutputSourceId = session.inputSourceId.ifBlank { null },
                    lastKnownStreamName = session.outboundStreamName.ifBlank { null },
                    autoRestartPermitted = false,
                )
            }
            .collect { captured -> _state.value = captured }
    }

    override suspend fun clearTransientStateOnExplicitStop() {
        _state.value = _state.value.copy(
            hasActiveOutput = false,
            outputState = OutputState.STOPPED,
            lastKnownOutputSourceId = null,
            lastKnownStreamName = null,
        )
    }
}

