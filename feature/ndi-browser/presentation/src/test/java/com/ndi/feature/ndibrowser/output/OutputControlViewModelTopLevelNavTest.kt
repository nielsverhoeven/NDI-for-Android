package com.ndi.feature.ndibrowser.output

import com.ndi.core.model.OutputConfiguration
import com.ndi.core.model.OutputHealthSnapshot
import com.ndi.core.model.OutputQualityLevel
import com.ndi.core.model.OutputSession
import com.ndi.core.model.OutputState
import com.ndi.core.model.navigation.BackgroundContinuationReason
import com.ndi.core.model.navigation.StreamContinuityState
import com.ndi.core.model.navigation.TopLevelDestination
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.OutputConfigurationRepository
import com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentRepository
import com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentState
import com.ndi.feature.ndibrowser.domain.repository.StreamContinuityRepository
import com.ndi.feature.ndibrowser.testutil.MainDispatcherRule
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import java.util.UUID

/**
 * Validates that top-level navigation away from Stream does NOT stop active output.
 * Process-death restore state must be contextual only (no auto-restart).
 */
@OptIn(ExperimentalCoroutinesApi::class)
class OutputControlViewModelTopLevelNavTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun activeOutput_remainsActiveAfterTopLevelNavigationAway() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val repository = NavAwareOutputRepository()
            val vm = OutputControlViewModel(
                repository,
                NavAwareOutputConfigurationRepository(),
                NavAwareConsentRepository(),
                OutputTelemetryEmitter {},
            )

            repository.emitState(OutputState.ACTIVE, "camera-1")
            advanceUntilIdle()

            // Simulating navigation away: output should NOT stop
            assertEquals(OutputState.ACTIVE, vm.uiState.value.outputState)
            assertEquals(0, repository.stopCalls)
        }

    @Test
    fun processDeathRestore_doesNotAutoRestart() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val repository = NavAwareOutputRepository()
            val vm = OutputControlViewModel(
                repository,
                NavAwareOutputConfigurationRepository(),
                NavAwareConsentRepository(),
                OutputTelemetryEmitter {},
            )

            // Simulate restore from process death: no onStartOutputPressed called
            repository.emitState(OutputState.READY, "camera-2")
            advanceUntilIdle()

            assertFalse(vm.uiState.value.recoveryInProgress)
            assertEquals(0, repository.startCalls)
        }

    @Test
    fun idempotentStopGuard_preventsDoubleStop() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val repository = NavAwareOutputRepository()
            val vm = OutputControlViewModel(
                repository,
                NavAwareOutputConfigurationRepository(),
                NavAwareConsentRepository(),
                OutputTelemetryEmitter {},
            )

            repository.emitState(OutputState.STOPPED, "camera-3")
            advanceUntilIdle()

            vm.onStopOutputPressed()
            advanceUntilIdle()

            assertEquals(0, repository.stopCalls)
        }

    @Test
    fun streamSetupControl_surfacesMapToStreamTopLevelDestination() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val repository = NavAwareOutputRepository()
            val vm = OutputControlViewModel(
                repository,
                NavAwareOutputConfigurationRepository(),
                NavAwareConsentRepository(),
                OutputTelemetryEmitter {},
            )

            repository.emitState(OutputState.READY, "camera-4")
            advanceUntilIdle()

            assertEquals(TopLevelDestination.STREAM, vm.uiState.value.topLevelDestination)
        }

    @Test
    fun explicitStop_clearsContinuityTransientState() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val repository = NavAwareOutputRepository()
            val continuityRepository = NavAwareStreamContinuityRepository()
            val vm = OutputControlViewModel(
                repository,
                NavAwareOutputConfigurationRepository(),
                NavAwareConsentRepository(),
                OutputTelemetryEmitter {},
                continuityRepository,
            )

            repository.emitState(OutputState.ACTIVE, "camera-5")
            advanceUntilIdle()
            vm.onStopOutputPressed()
            advanceUntilIdle()

            assertEquals(1, repository.stopCalls)
            assertTrue(continuityRepository.clearInvoked)
            assertEquals(OutputState.STOPPED, continuityRepository.observeContinuityState().value.outputState)
        }
}

private class NavAwareOutputRepository : NdiOutputRepository {
    private val sessions = MutableStateFlow(
        OutputSession(
            sessionId = UUID.randomUUID().toString(),
            inputSourceId = "",
            outboundStreamName = "",
            state = OutputState.READY,
            startedAtEpochMillis = 0L,
        ),
    )
    private val health = MutableStateFlow(
        OutputHealthSnapshot(
            snapshotId = UUID.randomUUID().toString(),
            sessionId = sessions.value.sessionId,
            capturedAtEpochMillis = 0L,
            networkReachable = true,
            inputReachable = true,
            qualityLevel = OutputQualityLevel.HEALTHY,
        ),
    )

    var stopCalls = 0
    var startCalls = 0

    override suspend fun startOutput(inputSourceId: String, streamName: String): OutputSession {
        startCalls++
        val active = sessions.value.copy(state = OutputState.ACTIVE)
        sessions.value = active
        return active
    }

    override suspend fun stopOutput(): OutputSession {
        stopCalls++
        val stopped = sessions.value.copy(state = OutputState.STOPPED)
        sessions.value = stopped
        return stopped
    }

    override fun observeOutputSession(): Flow<OutputSession> = sessions
    override suspend fun retryInterruptedOutputWithinWindow(windowSeconds: Int) = sessions.value
    override fun observeOutputHealth(): Flow<OutputHealthSnapshot> = health

    fun emitState(state: OutputState, sourceId: String) {
        sessions.value = sessions.value.copy(inputSourceId = sourceId, state = state)
    }
}

private class NavAwareOutputConfigurationRepository : OutputConfigurationRepository {
    override suspend fun savePreferredStreamName(value: String) = Unit
    override suspend fun getPreferredStreamName() = "NDI Output"
    override suspend fun saveLastSelectedInputSource(sourceId: String) = Unit
    override suspend fun getLastSelectedInputSource(): String? = null
    override suspend fun getConfiguration() = OutputConfiguration(preferredStreamName = "NDI Output")
}

private class NavAwareConsentRepository : ScreenCaptureConsentRepository {
    override suspend fun beginConsentRequest(inputSourceId: String) = Unit
    override suspend fun registerConsentResult(inputSourceId: String, granted: Boolean, tokenRef: String?) =
        ScreenCaptureConsentState(inputSourceId, granted, tokenRef)
    override suspend fun getConsentState(inputSourceId: String) =
        ScreenCaptureConsentState(inputSourceId, granted = true)
    override suspend fun clearConsent(inputSourceId: String) = Unit
}

private class NavAwareStreamContinuityRepository : StreamContinuityRepository {
    private val state = MutableStateFlow(
        StreamContinuityState(
            hasActiveOutput = false,
            outputState = OutputState.READY,
        ),
    )
    var clearInvoked: Boolean = false

    override fun observeContinuityState(): StateFlow<StreamContinuityState> = state.asStateFlow()

    override suspend fun captureLastKnownState() {
        state.value = state.value.copy(
            hasActiveOutput = true,
            outputState = OutputState.ACTIVE,
        )
    }

    override suspend fun markAppBackgrounded(reason: BackgroundContinuationReason) {
        state.value = state.value.copy(
            hasActiveOutput = true,
            outputState = OutputState.ACTIVE,
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

    override suspend fun clearTransientStateOnExplicitStop() {
        clearInvoked = true
        state.value = state.value.copy(
            hasActiveOutput = false,
            outputState = OutputState.STOPPED,
            runningWhileBackgrounded = false,
            backgroundReason = BackgroundContinuationReason.NONE,
            lastBackgroundedAtEpochMillis = null,
        )
    }
}

