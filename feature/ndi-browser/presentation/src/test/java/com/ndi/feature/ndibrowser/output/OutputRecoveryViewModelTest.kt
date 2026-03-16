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
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import java.util.UUID

@OptIn(ExperimentalCoroutinesApi::class)
class OutputRecoveryViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun interruptedSession_showsRecoveryActions() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = RecoveryOutputRepository()
        val viewModel = OutputControlViewModel(repository, RecoveryOutputConfigurationRepository(), RecoveryConsentRepository(), OutputTelemetryEmitter {})

        repository.emitInterrupted("camera-1", "network_lost")
        advanceUntilIdle()

        assertEquals(OutputState.INTERRUPTED, viewModel.uiState.value.outputState)
        assertTrue(viewModel.uiState.value.showRecoveryActions)
        assertTrue(viewModel.uiState.value.canRetry)
        assertTrue(viewModel.uiState.value.canStop)
    }

    @Test
    fun onRetryOutputPressed_recoversToActiveWithinWindow() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = RecoveryOutputRepository()
        val viewModel = OutputControlViewModel(repository, RecoveryOutputConfigurationRepository(), RecoveryConsentRepository(), OutputTelemetryEmitter {})

        repository.emitInterrupted("camera-2", "transient")
        advanceUntilIdle()

        viewModel.onRetryOutputPressed()
        advanceUntilIdle()

        assertEquals(OutputState.ACTIVE, viewModel.uiState.value.outputState)
        assertTrue(viewModel.uiState.value.canStop)
        assertEquals(1, repository.retryCalls)
    }
}

private class RecoveryOutputRepository : NdiOutputRepository {
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

    var retryCalls: Int = 0

    override suspend fun startOutput(inputSourceId: String, streamName: String): OutputSession = sessions.value

    override suspend fun stopOutput(): OutputSession {
        val stopped = sessions.value.copy(state = OutputState.STOPPED)
        sessions.value = stopped
        return stopped
    }

    override fun observeOutputSession(): Flow<OutputSession> = sessions

    override suspend fun retryInterruptedOutputWithinWindow(windowSeconds: Int): OutputSession {
        retryCalls += 1
        val active = sessions.value.copy(state = OutputState.ACTIVE, interruptionReason = null)
        sessions.value = active
        return active
    }

    override fun observeOutputHealth(): Flow<OutputHealthSnapshot> = health

    fun emitInterrupted(sourceId: String, reason: String) {
        sessions.value = sessions.value.copy(
            inputSourceId = sourceId,
            outboundStreamName = "NDI Output",
            state = OutputState.INTERRUPTED,
            interruptionReason = reason,
        )
    }
}

private class RecoveryOutputConfigurationRepository : OutputConfigurationRepository {
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

private class RecoveryConsentRepository : ScreenCaptureConsentRepository {
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

