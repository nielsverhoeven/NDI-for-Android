package com.ndi.feature.ndibrowser.output

import com.ndi.core.model.OutputConfiguration
import com.ndi.core.model.OutputHealthSnapshot
import com.ndi.core.model.OutputQualityLevel
import com.ndi.core.model.OutputSession
import com.ndi.core.model.OutputState
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.OutputConfigurationRepository
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
class OutputControlViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun onStartOutputPressed_movesToActiveState() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeOutputRepository()
        val viewModel = OutputControlViewModel(repository, InMemoryOutputConfigurationRepository(), OutputTelemetryEmitter {})

        viewModel.onOutputScreenVisible("camera-1")
        advanceUntilIdle()
        viewModel.onStartOutputPressed()
        advanceUntilIdle()

        assertEquals(OutputState.ACTIVE, viewModel.uiState.value.outputState)
        assertEquals("camera-1", viewModel.uiState.value.sourceId)
        assertEquals(1, repository.startCalls)
    }

    @Test
    fun onStartOutputPressed_isGuardedWhenAlreadyActive() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeOutputRepository()
        val viewModel = OutputControlViewModel(repository, InMemoryOutputConfigurationRepository(), OutputTelemetryEmitter {})

        viewModel.onOutputScreenVisible("camera-2")
        advanceUntilIdle()
        viewModel.onStartOutputPressed()
        advanceUntilIdle()
        viewModel.onStartOutputPressed()
        advanceUntilIdle()

        assertEquals(1, repository.startCalls)
    }

    @Test
    fun onStartOutputPressed_setsInterruptedStateOnFailure() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repository = FakeOutputRepository(failStart = true)
        val viewModel = OutputControlViewModel(repository, InMemoryOutputConfigurationRepository(), OutputTelemetryEmitter {})

        viewModel.onOutputScreenVisible("camera-3")
        advanceUntilIdle()
        viewModel.onStartOutputPressed()
        advanceUntilIdle()

        assertEquals(OutputState.INTERRUPTED, viewModel.uiState.value.outputState)
        assertTrue(viewModel.uiState.value.errorMessage?.isNotBlank() == true)
    }
}

private class FakeOutputRepository(
    private val failStart: Boolean = false,
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

    var startCalls: Int = 0

    override suspend fun startOutput(inputSourceId: String, streamName: String): OutputSession {
        startCalls += 1
        if (failStart) error("failed to start")
        val session = OutputSession(
            sessionId = UUID.randomUUID().toString(),
            inputSourceId = inputSourceId,
            outboundStreamName = streamName.ifBlank { "NDI Output" },
            state = OutputState.ACTIVE,
            startedAtEpochMillis = System.currentTimeMillis(),
        )
        sessions.value = session
        return session
    }

    override suspend fun stopOutput(): OutputSession {
        val session = sessions.value.copy(state = OutputState.STOPPED)
        sessions.value = session
        return session
    }

    override fun observeOutputSession(): Flow<OutputSession> = sessions

    override suspend fun retryInterruptedOutputWithinWindow(windowSeconds: Int): OutputSession {
        val session = sessions.value.copy(state = OutputState.ACTIVE)
        sessions.value = session
        return session
    }

    override fun observeOutputHealth(): Flow<OutputHealthSnapshot> = health
}

private class InMemoryOutputConfigurationRepository : OutputConfigurationRepository {
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
