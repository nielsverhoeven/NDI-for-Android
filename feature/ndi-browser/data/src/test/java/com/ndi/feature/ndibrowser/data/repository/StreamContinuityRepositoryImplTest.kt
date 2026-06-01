package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.OutputHealthSnapshot
import com.ndi.core.model.OutputQualityLevel
import com.ndi.core.model.OutputSession
import com.ndi.core.model.OutputState
import com.ndi.core.model.navigation.BackgroundContinuationReason
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.UUID

class StreamContinuityRepositoryImplTest {

    @Test
    fun markAppBackgrounded_whenOutputActive_marksRunningInBackground() = runTest {
        val outputRepository = FakeOutputRepository()
        outputRepository.sessionFlow.value = outputRepository.sessionFlow.value.copy(
            state = OutputState.ACTIVE,
            inputSourceId = "device-screen:local",
            outboundStreamName = "NDI Stream",
        )

        val repository = StreamContinuityRepositoryImpl(outputRepository)
        repository.captureLastKnownState()
        repository.markAppBackgrounded(BackgroundContinuationReason.APP_BACKGROUND)

        val continuity = repository.observeContinuityState().first()
        assertTrue(continuity.hasActiveOutput)
        assertTrue(continuity.runningWhileBackgrounded)
        assertEquals(BackgroundContinuationReason.APP_BACKGROUND, continuity.backgroundReason)
        assertEquals("device-screen:local", continuity.lastKnownOutputSourceId)
    }

    @Test
    fun markAppForegrounded_clearsBackgroundMarkers() = runTest {
        val outputRepository = FakeOutputRepository()
        outputRepository.sessionFlow.value = outputRepository.sessionFlow.value.copy(state = OutputState.ACTIVE)

        val repository = StreamContinuityRepositoryImpl(outputRepository)
        repository.captureLastKnownState()
        repository.markAppBackgrounded(BackgroundContinuationReason.APP_BACKGROUND)
        repository.markAppForegrounded()

        val continuity = repository.observeContinuityState().first()
        assertFalse(continuity.runningWhileBackgrounded)
        assertEquals(BackgroundContinuationReason.NONE, continuity.backgroundReason)
    }

    @Test
    fun markAppBackgrounded_whenNoActiveOutput_keepsBackgroundDisabled() = runTest {
        val outputRepository = FakeOutputRepository()
        outputRepository.sessionFlow.value = outputRepository.sessionFlow.value.copy(state = OutputState.STOPPED)

        val repository = StreamContinuityRepositoryImpl(outputRepository)
        repository.captureLastKnownState()
        repository.markAppBackgrounded(BackgroundContinuationReason.APP_BACKGROUND)

        val continuity = repository.observeContinuityState().first()
        assertFalse(continuity.hasActiveOutput)
        assertFalse(continuity.runningWhileBackgrounded)
        assertEquals(BackgroundContinuationReason.NONE, continuity.backgroundReason)
    }

    @Test
    fun markAppBackgrounded_whenOutputStarting_doesNotEnableBackgroundRunFlag() = runTest {
        val outputRepository = FakeOutputRepository()
        outputRepository.sessionFlow.value = outputRepository.sessionFlow.value.copy(state = OutputState.STARTING)

        val repository = StreamContinuityRepositoryImpl(outputRepository)
        repository.captureLastKnownState()
        repository.markAppBackgrounded(BackgroundContinuationReason.APP_BACKGROUND)

        val continuity = repository.observeContinuityState().first()
        assertTrue(continuity.hasActiveOutput)
        assertFalse(continuity.runningWhileBackgrounded)
        assertEquals(BackgroundContinuationReason.NONE, continuity.backgroundReason)
    }
}

private class FakeOutputRepository : NdiOutputRepository {
    val sessionFlow = MutableStateFlow(
        OutputSession(
            sessionId = UUID.randomUUID().toString(),
            inputSourceId = "",
            outboundStreamName = "",
            state = OutputState.READY,
            startedAtEpochMillis = 0L,
        ),
    )

    private val healthFlow = MutableStateFlow(
        OutputHealthSnapshot(
            snapshotId = UUID.randomUUID().toString(),
            sessionId = sessionFlow.value.sessionId,
            capturedAtEpochMillis = 0L,
            networkReachable = true,
            inputReachable = true,
            qualityLevel = OutputQualityLevel.HEALTHY,
        ),
    )

    override suspend fun startOutput(inputSourceId: String, streamName: String): OutputSession = sessionFlow.value

    override suspend fun stopOutput(): OutputSession = sessionFlow.value

    override fun observeOutputSession(): Flow<OutputSession> = sessionFlow

    override suspend fun retryInterruptedOutputWithinWindow(windowSeconds: Int): OutputSession = sessionFlow.value

    override fun observeOutputHealth(): Flow<OutputHealthSnapshot> = healthFlow
}
