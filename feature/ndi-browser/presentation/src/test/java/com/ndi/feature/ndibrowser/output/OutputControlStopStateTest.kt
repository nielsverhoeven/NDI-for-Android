package com.ndi.feature.ndibrowser.output

import com.ndi.core.model.OutputConfiguration
import com.ndi.core.model.OutputHealthSnapshot
import com.ndi.core.model.OutputQualityLevel
import com.ndi.core.model.OutputSession
import com.ndi.core.model.OutputState
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.OutputConfigurationRepository
import com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentRepository
import com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentState
import com.ndi.feature.ndibrowser.testutil.MainDispatcherRule
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import java.util.UUID

@OptIn(ExperimentalCoroutinesApi::class)
class OutputControlStopStateTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun onStopOutputPressed_transitionsToStopped() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = StopAwareOutputRepository()
        val viewModel = OutputControlViewModel(repository, StopStateOutputConfigurationRepository(), StopStateConsentRepository(), OutputTelemetryEmitter {})

        repository.emitState(OutputState.ACTIVE, sourceId = "camera-1")
        advanceUntilIdle()

        viewModel.onStopOutputPressed()
        advanceUntilIdle()

        assertEquals(OutputState.STOPPED, viewModel.uiState.value.outputState)
        assertTrue(viewModel.uiState.value.canStart)
        assertFalse(viewModel.uiState.value.canStop)
        assertEquals(1, repository.stopCalls)
    }

    @Test
    fun onStopOutputPressed_isGuardedWhenAlreadyStopped() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = StopAwareOutputRepository()
        val viewModel = OutputControlViewModel(repository, StopStateOutputConfigurationRepository(), StopStateConsentRepository(), OutputTelemetryEmitter {})

        repository.emitState(OutputState.STOPPED, sourceId = "camera-2")
        advanceUntilIdle()

        viewModel.onStopOutputPressed()
        advanceUntilIdle()

        assertEquals(0, repository.stopCalls)
    }

    @Test
    fun onStopOutputPressed_setsInterruptedOnFailure() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = StopAwareOutputRepository(failStop = true)
        val viewModel = OutputControlViewModel(repository, StopStateOutputConfigurationRepository(), StopStateConsentRepository(), OutputTelemetryEmitter {})

        repository.emitState(OutputState.ACTIVE, sourceId = "camera-3")
        advanceUntilIdle()

        viewModel.onStopOutputPressed()
        advanceUntilIdle()

        assertEquals(OutputState.INTERRUPTED, viewModel.uiState.value.outputState)
        assertTrue(viewModel.uiState.value.errorMessage?.contains("failed") == true)
    }

    @Test
    fun stateMapping_reflectsControlAvailability() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = StopAwareOutputRepository()
        val viewModel = OutputControlViewModel(repository, StopStateOutputConfigurationRepository(), StopStateConsentRepository(), OutputTelemetryEmitter {})

        repository.emitState(OutputState.READY, sourceId = "cam")
        advanceUntilIdle()
        assertTrue(viewModel.uiState.value.canStart)
        assertFalse(viewModel.uiState.value.canStop)

        repository.emitState(OutputState.STARTING, sourceId = "cam")
        advanceUntilIdle()
        assertFalse(viewModel.uiState.value.canStart)
        assertTrue(viewModel.uiState.value.canStop)

        repository.emitState(OutputState.ACTIVE, sourceId = "cam")
        advanceUntilIdle()
        assertFalse(viewModel.uiState.value.canStart)
        assertTrue(viewModel.uiState.value.canStop)

        repository.emitState(OutputState.STOPPING, sourceId = "cam")
        advanceUntilIdle()
        assertFalse(viewModel.uiState.value.canStart)
        assertFalse(viewModel.uiState.value.canStop)

        repository.emitState(OutputState.STOPPED, sourceId = "cam")
        advanceUntilIdle()
        assertTrue(viewModel.uiState.value.canStart)
        assertFalse(viewModel.uiState.value.canStop)
    }
}

private class StopAwareOutputRepository(
    private val failStop: Boolean = false,
) : NdiOutputRepository {
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

    var stopCalls: Int = 0

    override suspend fun startOutput(inputSourceId: String, streamName: String): OutputSession {
        val active = sessions.value.copy(
            inputSourceId = inputSourceId,
            outboundStreamName = streamName,
            state = OutputState.ACTIVE,
        )
        sessions.value = active
        return active
    }

    override suspend fun stopOutput(): OutputSession {
        stopCalls += 1
        if (failStop) error("stop failed")
        val stopping = sessions.value.copy(state = OutputState.STOPPING)
        sessions.value = stopping
        val stopped = stopping.copy(state = OutputState.STOPPED)
        sessions.value = stopped
        return stopped
    }

    override fun observeOutputSession(): Flow<OutputSession> = sessions

    override suspend fun retryInterruptedOutputWithinWindow(windowSeconds: Int): OutputSession {
        return sessions.value
    }

    override fun observeOutputHealth(): Flow<OutputHealthSnapshot> = health

    fun emitState(state: OutputState, sourceId: String) {
        sessions.value = sessions.value.copy(
            inputSourceId = sourceId,
            state = state,
            outboundStreamName = "NDI Output",
        )
    }
}

private class StopStateOutputConfigurationRepository : OutputConfigurationRepository {
    private var config = OutputConfiguration(preferredStreamName = "NDI Output")

    override suspend fun savePreferredStreamName(value: String) {
        config = config.copy(preferredStreamName = value)
    }

    override suspend fun getPreferredStreamName(): String = config.preferredStreamName

    override suspend fun saveLastSelectedInputSource(sourceId: String) {
        config = config.copy(lastSelectedInputSourceId = sourceId)
    }

    override suspend fun getLastSelectedInputSource(): String? = config.lastSelectedInputSourceId

    override suspend fun getConfiguration(): OutputConfiguration = config
}

private class StopStateConsentRepository : ScreenCaptureConsentRepository {
    override suspend fun beginConsentRequest(inputSourceId: String) = Unit

    override suspend fun registerConsentResult(
        inputSourceId: String,
        granted: Boolean,
        tokenRef: String?,
    ): ScreenCaptureConsentState = ScreenCaptureConsentState(inputSourceId, granted, tokenRef)

    override suspend fun getConsentState(inputSourceId: String): ScreenCaptureConsentState? {
        return ScreenCaptureConsentState(inputSourceId, granted = true, tokenRef = "test")
    }

    override suspend fun clearConsent(inputSourceId: String) = Unit
}

