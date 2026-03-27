package com.ndi.feature.ndibrowser.data

import com.ndi.core.database.OutputSessionDao
import com.ndi.core.database.OutputSessionEntity
import com.ndi.core.model.OutputSession
import com.ndi.core.model.OutputState
import com.ndi.feature.ndibrowser.data.OutputRecoveryCoordinator
import com.ndi.feature.ndibrowser.data.mapper.OutputSessionMapper
import com.ndi.feature.ndibrowser.data.repository.NdiOutputRepositoryImpl
import com.ndi.sdkbridge.NdiOutputBridge
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Test

class NdiOutputRecoveryRepositoryContractTest {

    @Test
    fun retryInterruptedOutputWithinWindow_recoversWhenAttemptSucceeds() = runTest {
        val bridge = RecoveryBridge(failuresBeforeSuccess = 1)
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = RecoveryContractOutputSessionDao(),
            outputBridge = bridge,
            mapper = OutputSessionMapper(),
            recoveryCoordinator = OutputRecoveryCoordinator(retryDelayMillis = 0L),
        )

        bridge.setUnreachableOnNextReachabilityCheck()
        runCatching { repository.startOutput("camera-1", "Live") }
        repository.retryInterruptedOutputWithinWindow(windowSeconds = 2)

        val terminal: OutputSession = repository.observeOutputSession().first()
        assertEquals(OutputState.ACTIVE, terminal.state)
        assertEquals(2, bridge.startCalls)
    }

    @Test
    fun retryInterruptedOutputWithinWindow_stopsAfterWindowWhenAttemptsFail() = runTest {
        val bridge = RecoveryBridge(failuresBeforeSuccess = Int.MAX_VALUE)
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = RecoveryContractOutputSessionDao(),
            outputBridge = bridge,
            mapper = OutputSessionMapper(),
            recoveryCoordinator = OutputRecoveryCoordinator(retryDelayMillis = 0L),
        )

        bridge.setUnreachableOnNextReachabilityCheck()
        runCatching { repository.startOutput("camera-2", "Live") }
        val terminal = repository.retryInterruptedOutputWithinWindow(windowSeconds = 2)

        assertEquals(OutputState.STOPPED, terminal.state)
        assertEquals(2, bridge.startCalls)
    }

    @Test
    fun retryInterruptedOutputWithinWindow_usesLocalSenderForDeviceScreenSessions() = runTest {
        val bridge = RecoveryBridge(failuresBeforeSuccess = Int.MAX_VALUE, localFailuresBeforeSuccess = 1)
        val repository = NdiOutputRepositoryImpl(
            outputSessionDao = RecoveryContractOutputSessionDao(),
            outputBridge = bridge,
            screenCaptureConsentRepository = RecoveryConsentRepository(granted = true),
            mapper = OutputSessionMapper(),
            recoveryCoordinator = OutputRecoveryCoordinator(retryDelayMillis = 0L),
        )

        runCatching { repository.startOutput("device-screen:local", "Local") }
        val terminal = repository.retryInterruptedOutputWithinWindow(windowSeconds = 2)

        assertEquals(OutputState.ACTIVE, terminal.state)
        assertEquals(0, bridge.startCalls)
        assertEquals(2, bridge.localStartCalls)
    }
}

private class RecoveryBridge(
    private val failuresBeforeSuccess: Int,
    private val localFailuresBeforeSuccess: Int = Int.MAX_VALUE,
) : NdiOutputBridge {
    var startCalls: Int = 0
    var localStartCalls: Int = 0
    private var forceUnreachableNextCheck: Boolean = false

    override suspend fun isSourceReachable(sourceId: String): Boolean {
        if (forceUnreachableNextCheck) {
            forceUnreachableNextCheck = false
            return false
        }
        return true
    }

    override fun startSender(sourceId: String, streamName: String) {
        startCalls += 1
        if (startCalls <= failuresBeforeSuccess) {
            error("retry failure")
        }
    }

    override fun stopSender() = Unit

    override fun startLocalScreenShareSender(streamName: String) {
        localStartCalls += 1
        if (localStartCalls <= localFailuresBeforeSuccess) {
            error("retry failure")
        }
    }

    override fun stopLocalScreenShareSender() = Unit

    fun setUnreachableOnNextReachabilityCheck() {
        forceUnreachableNextCheck = true
    }
}

private class RecoveryContractOutputSessionDao : OutputSessionDao {
    private var latest: OutputSessionEntity? = null

    override suspend fun getLatest(): OutputSessionEntity? = latest

    override suspend fun upsert(session: OutputSessionEntity) {
        latest = session
    }
}

private class RecoveryConsentRepository(
    private val granted: Boolean,
) : com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentRepository {
    override suspend fun beginConsentRequest(inputSourceId: String) = Unit

    override suspend fun registerConsentResult(
        inputSourceId: String,
        granted: Boolean,
        tokenRef: String?,
    ) = com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentState(inputSourceId, granted, tokenRef)

    override suspend fun getConsentState(inputSourceId: String) =
        com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentState(inputSourceId, granted = granted, tokenRef = "token")

    override suspend fun clearConsent(inputSourceId: String) = Unit
}
