package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.OutputSessionDao
import com.ndi.core.database.OutputSessionEntity
import com.ndi.core.model.OutputHealthSnapshot
import com.ndi.core.model.OutputQualityLevel
import com.ndi.core.model.OutputSession
import com.ndi.core.model.OutputState
import com.ndi.feature.ndibrowser.data.OutputRecoveryCoordinator
import com.ndi.feature.ndibrowser.data.OutputSessionCoordinator
import com.ndi.feature.ndibrowser.data.mapper.OutputSessionMapper
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.sdkbridge.NdiOutputBridge
import java.util.UUID
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext

class NdiOutputRepositoryImpl(
    private val outputSessionDao: OutputSessionDao,
    private val outputBridge: NdiOutputBridge,
    private val mapper: OutputSessionMapper = OutputSessionMapper(),
    private val coordinator: OutputSessionCoordinator = OutputSessionCoordinator(),
    private val recoveryCoordinator: OutputRecoveryCoordinator = OutputRecoveryCoordinator(),
) : NdiOutputRepository {

    private val startMutex = Mutex()

    private val outputSessionState = MutableStateFlow(
        OutputSession(
            sessionId = UUID.randomUUID().toString(),
            inputSourceId = "",
            outboundStreamName = "",
            state = OutputState.READY,
            startedAtEpochMillis = 0L,
        ),
    )

    private val outputHealthState = MutableStateFlow(
        OutputHealthSnapshot(
            snapshotId = UUID.randomUUID().toString(),
            sessionId = outputSessionState.value.sessionId,
            capturedAtEpochMillis = System.currentTimeMillis(),
            networkReachable = true,
            inputReachable = true,
            qualityLevel = OutputQualityLevel.HEALTHY,
        ),
    )

    override suspend fun startOutput(inputSourceId: String, streamName: String): OutputSession {
        require(inputSourceId.isNotBlank()) { "Source id is required" }

        return startMutex.withLock {
            val current = outputSessionState.value
            if (current.state == OutputState.STARTING || current.state == OutputState.ACTIVE) {
                return@withLock current
            }

            val isReachable = withContext(Dispatchers.IO) { outputBridge.isSourceReachable(inputSourceId) }
            if (!isReachable) {
                val interrupted = current.copy(
                    inputSourceId = inputSourceId,
                    outboundStreamName = streamName.ifBlank { current.outboundStreamName },
                    state = OutputState.INTERRUPTED,
                    interruptionReason = "Selected source is unreachable",
                )
                outputSessionState.value = interrupted
                outputHealthState.value = coordinator.nextHealthForState(interrupted)
                throw IllegalStateException("Selected source is unreachable")
            }

            val starting = mapper.createStartingSession(
                inputSourceId = inputSourceId,
                preferredName = streamName,
                activeStreamNames = setOf(current.outboundStreamName).filter { it.isNotBlank() }.toSet(),
            )
            outputSessionState.value = starting

            runCatching {
                withContext(Dispatchers.IO) {
                    outputBridge.startSender(inputSourceId, starting.outboundStreamName)
                }
            }.onFailure { error ->
                val interrupted = starting.copy(
                    state = OutputState.INTERRUPTED,
                    interruptionReason = error.message ?: "Unable to start output",
                )
                outputSessionState.value = interrupted
                outputHealthState.value = coordinator.nextHealthForState(interrupted)
                throw error
            }

            val active = starting.copy(state = OutputState.ACTIVE)
            outputSessionState.value = active
            outputSessionDao.upsert(active.toEntity())
            outputHealthState.value = outputHealthState.value.copy(
                sessionId = active.sessionId,
                capturedAtEpochMillis = System.currentTimeMillis(),
                networkReachable = true,
                inputReachable = true,
                qualityLevel = OutputQualityLevel.HEALTHY,
                messageCode = null,
            )
            active
        }
    }

    override suspend fun stopOutput(): OutputSession {
        val current = outputSessionState.value
        if (current.state == OutputState.STOPPED) return current

        val stopping = coordinator.nextOnStopRequested(current)
        outputSessionState.value = stopping
        outputHealthState.value = coordinator.nextHealthForState(stopping)

        if (current.state != OutputState.STOPPING) {
            withContext(Dispatchers.IO) {
                outputBridge.stopSender()
            }
        }

        val stopped = coordinator.nextOnStopped(stopping)
        outputSessionState.value = stopped
        outputHealthState.value = coordinator.nextHealthForState(stopped)
        outputSessionDao.upsert(stopped.toEntity())
        return stopped
    }

    override fun observeOutputSession(): Flow<OutputSession> = outputSessionState.asStateFlow()

    override suspend fun retryInterruptedOutputWithinWindow(windowSeconds: Int): OutputSession {
        return startMutex.withLock {
            val current = outputSessionState.value
            require(current.state == OutputState.INTERRUPTED) { "Retry is only allowed from INTERRUPTED state" }
            require(current.inputSourceId.isNotBlank()) { "Retry requires a selected source" }

            val retrying = current.copy(
                state = OutputState.STARTING,
                retryAttempts = current.retryAttempts + 1,
                interruptionReason = null,
            )
            outputSessionState.value = retrying
            outputHealthState.value = coordinator.nextHealthForState(retrying)

            val result = recoveryCoordinator.retryWithinWindow(windowSeconds) {
                runCatching {
                    withContext(Dispatchers.IO) {
                        outputBridge.startSender(retrying.inputSourceId, retrying.outboundStreamName)
                    }
                }.isSuccess
            }

            if (result.recovered) {
                val active = retrying.copy(
                    state = OutputState.ACTIVE,
                    retryAttempts = current.retryAttempts + result.attempts,
                )
                outputSessionState.value = active
                outputHealthState.value = coordinator.nextHealthForState(active)
                outputSessionDao.upsert(active.toEntity())
                return@withLock active
            }

            val stopped = retrying.copy(
                state = OutputState.STOPPED,
                stoppedAtEpochMillis = System.currentTimeMillis(),
                retryAttempts = current.retryAttempts + result.attempts,
                interruptionReason = "Retry window expired",
            )
            outputSessionState.value = stopped
            outputHealthState.value = coordinator.nextHealthForState(stopped)
            outputSessionDao.upsert(stopped.toEntity())
            stopped
        }
    }

    override fun observeOutputHealth(): Flow<OutputHealthSnapshot> = outputHealthState.asStateFlow()

    private fun OutputSession.toEntity(): OutputSessionEntity {
        return OutputSessionEntity(
            sessionId = sessionId,
            inputSourceId = inputSourceId,
            outboundStreamName = outboundStreamName,
            state = state.name,
            startedAtEpochMillis = startedAtEpochMillis,
            stoppedAtEpochMillis = stoppedAtEpochMillis,
            interruptionReason = interruptionReason,
            retryAttempts = retryAttempts,
            hostInstanceId = hostInstanceId,
        )
    }
}
