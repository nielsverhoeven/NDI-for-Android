package com.ndi.app.navigation

import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.TelemetryEvent
import com.ndi.core.model.OutputState
import com.ndi.core.model.navigation.BackgroundContinuationReason
import com.ndi.feature.ndibrowser.domain.repository.StreamContinuityRepository
import com.ndi.feature.ndibrowser.output.OutputTelemetryEmitter
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Instrumentation-level continuity checks for app-switch behavior.
 * Exercises the app lifecycle mediator path (AppContinuityViewModel) directly.
 */
@RunWith(AndroidJUnit4::class)
class StreamBackgroundContinuityUiTest {

    @Test
    fun appBackgroundEvent_marksContinuityUsingAppBackgroundReason() = runBlocking {
        val continuityRepository = AndroidTestStreamContinuityRepository(activeSourceId = "device-screen:local")
        val emitted = mutableListOf<TelemetryEvent>()
        val viewModel = AppContinuityViewModel(continuityRepository, OutputTelemetryEmitter { emitted += it })

        viewModel.onAppBackgrounded(isConfigurationChange = false)

        val continuity = continuityRepository.observeContinuityState().first()
        assertTrue(continuity.runningWhileBackgrounded)
        assertEquals(BackgroundContinuationReason.APP_BACKGROUND, continuity.backgroundReason)
        assertTrue(emitted.any { it.name == TelemetryEvent.OUTPUT_CONTINUITY_BACKGROUNDED })
    }

    @Test
    fun configurationChangeStop_doesNotMarkBackgrounded() = runBlocking {
        val continuityRepository = AndroidTestStreamContinuityRepository(activeSourceId = "device-screen:local")
        val viewModel = AppContinuityViewModel(continuityRepository, OutputTelemetryEmitter {})

        viewModel.onAppBackgrounded(isConfigurationChange = true)

        val continuity = continuityRepository.observeContinuityState().first()
        assertFalse(continuity.runningWhileBackgrounded)
        assertEquals(BackgroundContinuationReason.NONE, continuity.backgroundReason)
    }
}

private class AndroidTestStreamContinuityRepository(
    activeSourceId: String?,
) : StreamContinuityRepository {
    private val state = MutableStateFlow(
        com.ndi.core.model.navigation.StreamContinuityState(
            hasActiveOutput = activeSourceId != null,
            outputState = if (activeSourceId != null) OutputState.ACTIVE else OutputState.READY,
            lastKnownOutputSourceId = activeSourceId,
            lastKnownStreamName = if (activeSourceId != null) "Android Test Stream" else null,
        ),
    )

    override fun observeContinuityState(): Flow<com.ndi.core.model.navigation.StreamContinuityState> = state

    override suspend fun captureLastKnownState() = Unit

    override suspend fun markAppBackgrounded(reason: BackgroundContinuationReason) {
        state.value = state.value.copy(
            runningWhileBackgrounded = true,
            backgroundReason = reason,
            lastBackgroundedAtEpochMillis = System.currentTimeMillis(),
        )
    }

    override suspend fun markAppForegrounded() {
        state.value = state.value.copy(
            runningWhileBackgrounded = false,
            backgroundReason = BackgroundContinuationReason.NONE,
            lastBackgroundedAtEpochMillis = null,
        )
    }

    override suspend fun clearTransientStateOnExplicitStop() = Unit
}
