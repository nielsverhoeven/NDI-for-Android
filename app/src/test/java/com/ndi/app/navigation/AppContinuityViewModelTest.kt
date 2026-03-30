package com.ndi.app.navigation

import com.ndi.app.testutil.MainDispatcherRule
import com.ndi.core.model.OutputState
import com.ndi.core.model.TelemetryEvent
import com.ndi.core.model.navigation.BackgroundContinuationReason
import com.ndi.core.model.navigation.StreamContinuityState
import com.ndi.feature.ndibrowser.domain.repository.StreamContinuityRepository
import com.ndi.feature.ndibrowser.output.OutputTelemetryEmitter
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class AppContinuityViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun onAppBackgrounded_marksAppBackgroundReason() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeStreamContinuityRepository(activeSourceId = "device-screen:local")
        val emitted = mutableListOf<TelemetryEvent>()
        val viewModel = AppContinuityViewModel(repository, OutputTelemetryEmitter { emitted += it })

        viewModel.onAppBackgrounded(isConfigurationChange = false)
        advanceUntilIdle()

        assertEquals(BackgroundContinuationReason.APP_BACKGROUND, repository.lastBackgroundReason)
        assertTrue(emitted.any { it.name == TelemetryEvent.OUTPUT_CONTINUITY_BACKGROUNDED })
    }

    @Test
    fun onAppBackgrounded_skipsWhenConfigurationChanging() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeStreamContinuityRepository(activeSourceId = "device-screen:local")
        val viewModel = AppContinuityViewModel(repository, OutputTelemetryEmitter {})

        viewModel.onAppBackgrounded(isConfigurationChange = true)
        advanceUntilIdle()

        assertEquals(null, repository.lastBackgroundReason)
    }

    @Test
    fun onAppForegrounded_marksForegroundWhenActive() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeStreamContinuityRepository(activeSourceId = "device-screen:local")
        val emitted = mutableListOf<TelemetryEvent>()
        val viewModel = AppContinuityViewModel(repository, OutputTelemetryEmitter { emitted += it })

        viewModel.onAppBackgrounded(isConfigurationChange = false)
        advanceUntilIdle()
        viewModel.onAppForegrounded()
        advanceUntilIdle()

        assertTrue(repository.foregroundMarked)
        assertTrue(emitted.any { it.name == TelemetryEvent.OUTPUT_CONTINUITY_FOREGROUNDED })
    }

    @Test
    fun lifecycleEvents_doNothingWhenNoActiveOutput() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeStreamContinuityRepository(activeSourceId = null)
        val emitted = mutableListOf<TelemetryEvent>()
        val viewModel = AppContinuityViewModel(repository, OutputTelemetryEmitter { emitted += it })

        viewModel.onAppForegrounded()
        viewModel.onAppBackgrounded(isConfigurationChange = false)
        advanceUntilIdle()

        assertFalse(repository.foregroundMarked)
        assertEquals(null, repository.lastBackgroundReason)
        assertTrue(emitted.isEmpty())
    }
}

private class FakeStreamContinuityRepository(
    activeSourceId: String?,
) : StreamContinuityRepository {
    private val state = MutableStateFlow(
        StreamContinuityState(
            hasActiveOutput = activeSourceId != null,
            outputState = if (activeSourceId != null) OutputState.ACTIVE else OutputState.READY,
            lastKnownOutputSourceId = activeSourceId,
            lastKnownStreamName = if (activeSourceId != null) "Test Stream" else null,
        ),
    )

    var lastBackgroundReason: BackgroundContinuationReason? = null
    var foregroundMarked: Boolean = false

    override fun observeContinuityState(): Flow<StreamContinuityState> = state.asStateFlow()

    override suspend fun captureLastKnownState() = Unit

    override suspend fun markAppBackgrounded(reason: BackgroundContinuationReason) {
        lastBackgroundReason = reason
        state.value = state.value.copy(
            runningWhileBackgrounded = true,
            backgroundReason = reason,
            lastBackgroundedAtEpochMillis = System.currentTimeMillis(),
        )
    }

    override suspend fun markAppForegrounded() {
        foregroundMarked = true
        state.value = state.value.copy(
            runningWhileBackgrounded = false,
            backgroundReason = BackgroundContinuationReason.NONE,
            lastBackgroundedAtEpochMillis = null,
        )
    }

    override suspend fun clearTransientStateOnExplicitStop() = Unit
}
