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
}

private class RecoveryBridge(
    private val failuresBeforeSuccess: Int,
) : NdiOutputBridge {
    var startCalls: Int = 0
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
