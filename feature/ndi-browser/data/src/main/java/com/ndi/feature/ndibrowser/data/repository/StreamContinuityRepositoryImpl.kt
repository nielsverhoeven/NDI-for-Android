package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.OutputState
import com.ndi.core.model.navigation.BackgroundContinuationReason
import com.ndi.core.model.navigation.StreamContinuityState
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.StreamContinuityRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first

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
            runningWhileBackgrounded = false,
            backgroundReason = BackgroundContinuationReason.NONE,
            autoRestartPermitted = false,
        ),
    )

    override fun observeContinuityState(): Flow<StreamContinuityState> = _state.asStateFlow()

    override suspend fun captureLastKnownState() {
        val session = outputRepository.observeOutputSession().first()
        val hasActiveOutput =
            session.state == OutputState.ACTIVE ||
                session.state == OutputState.STARTING ||
                session.state == OutputState.INTERRUPTED
        val canRunWhileBackgrounded = session.state == OutputState.ACTIVE
        val preserveBackgroundState = canRunWhileBackgrounded && _state.value.runningWhileBackgrounded

        _state.value = StreamContinuityState(
            hasActiveOutput = hasActiveOutput,
            outputState = session.state,
            lastKnownOutputSourceId = session.inputSourceId.ifBlank { null },
            lastKnownStreamName = session.outboundStreamName.ifBlank { null },
            runningWhileBackgrounded = preserveBackgroundState,
            backgroundReason = if (preserveBackgroundState) _state.value.backgroundReason else BackgroundContinuationReason.NONE,
            lastBackgroundedAtEpochMillis = if (preserveBackgroundState) _state.value.lastBackgroundedAtEpochMillis else null,
            autoRestartPermitted = false,
        )
    }

    override suspend fun markAppBackgrounded(reason: BackgroundContinuationReason) {
        val current = _state.value
        _state.value = if (current.outputState == OutputState.ACTIVE) {
            current.copy(
                runningWhileBackgrounded = true,
                backgroundReason = reason,
                lastBackgroundedAtEpochMillis = System.currentTimeMillis(),
            )
        } else {
            current.copy(
                runningWhileBackgrounded = false,
                backgroundReason = BackgroundContinuationReason.NONE,
                lastBackgroundedAtEpochMillis = null,
            )
        }
    }

    override suspend fun markAppForegrounded() {
        _state.value = _state.value.copy(
            runningWhileBackgrounded = false,
            backgroundReason = BackgroundContinuationReason.NONE,
            lastBackgroundedAtEpochMillis = null,
        )
    }

    override suspend fun clearTransientStateOnExplicitStop() {
        _state.value = _state.value.copy(
            hasActiveOutput = false,
            outputState = OutputState.STOPPED,
            lastKnownOutputSourceId = null,
            lastKnownStreamName = null,
            runningWhileBackgrounded = false,
            backgroundReason = BackgroundContinuationReason.NONE,
            lastBackgroundedAtEpochMillis = null,
        )
    }
}

