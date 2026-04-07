package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.NdiDeveloperOverlayState
import com.ndi.core.model.DeveloperDiscoveryDiagnostics
import com.ndi.core.model.NdiLogCategory
import com.ndi.core.model.NdiLogLevel
import com.ndi.core.model.NdiOverlayMode
import com.ndi.core.model.NdiRedactedLogEntry
import com.ndi.core.model.OutputSession
import com.ndi.core.model.OutputState
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerSession
import com.ndi.core.model.CompatibilityGuidance
import com.ndi.core.model.DiscoveryCompatibilitySnapshot
import com.ndi.core.model.DiscoveryCompatibilityStatus
import com.ndi.feature.ndibrowser.domain.repository.DiscoveryCompatibilityMatrixRepository
import com.ndi.feature.ndibrowser.domain.repository.DeveloperDiagnosticsRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.launch
import java.util.UUID

class DeveloperDiagnosticsRepositoryImpl(
    private val viewerRepository: NdiViewerRepository? = null,
    private val outputRepository: NdiOutputRepository? = null,
    private val logBuffer: DeveloperDiagnosticsLogBuffer = DeveloperDiagnosticsLogBuffer(),
    private val compatibilityMatrixRepository: DiscoveryCompatibilityMatrixRepository = DiscoveryCompatibilityMatrixRepositoryImpl(),
) : DeveloperDiagnosticsRepository {

    private val scope = CoroutineScope(Dispatchers.Default + SupervisorJob())

    private val _overlayState = MutableStateFlow(defaultOverlayState())
    private val _discoveryDiagnostics = MutableStateFlow(
        DeveloperDiscoveryDiagnostics(
            developerModeEnabled = false,
            latestDiscoveryRefreshStatus = null,
            latestDiscoveryRefreshAtEpochMillis = null,
            serverStatusRollup = emptyList(),
            recentDiscoveryLogs = emptyList(),
        ),
    )

    init {
        bindDiagnosticsStreams()
    }

    override fun observeOverlayState(): Flow<NdiDeveloperOverlayState> = _overlayState.asStateFlow()

    override fun observeRecentLogs(): Flow<List<NdiRedactedLogEntry>> = logBuffer.observeRecentLogs()
    override fun observeDiscoveryDiagnostics(): Flow<DeveloperDiscoveryDiagnostics> = _discoveryDiagnostics.asStateFlow()


    private fun bindDiagnosticsStreams() {
        val viewerFlow = viewerRepository?.observeViewerSession() ?: MutableStateFlow(idleViewerSession())
        val outputFlow = outputRepository?.observeOutputSession() ?: MutableStateFlow(idleOutputSession())
        val logFlow = logBuffer.observeRecentLogs()

        scope.launch {
            combine(viewerFlow, outputFlow, logFlow) { viewerSession, outputSession, recentLogs ->
                buildOverlayState(viewerSession, outputSession, recentLogs)
            }.collect { state ->
                _overlayState.value = state
            }
        }

        scope.launch {
            combine(compatibilityMatrixRepository.observeMatrixSnapshot(), logFlow) { matrixSnapshot, recentLogs ->
                buildDiscoveryDiagnostics(matrixSnapshot, recentLogs)
            }.collect { diagnostics ->
                _discoveryDiagnostics.value = diagnostics
            }
        }

        viewerRepository?.let { repository ->
            scope.launch {
                repository.observeViewerSession().collect(::appendViewerLog)
            }
        }

        outputRepository?.let { repository ->
            scope.launch {
                repository.observeOutputSession().collect(::appendOutputLog)
            }
        }
    }

    private fun defaultOverlayState(): NdiDeveloperOverlayState = NdiDeveloperOverlayState(
        visible = false,
        mode = NdiOverlayMode.DISABLED,
        streamDirectionLabel = "",
        streamStatusLabel = "",
        sessionId = null,
        streamSourceLabel = null,
        warningMessage = null,
        recentLogs = emptyList(),
        updatedAtEpochMillis = System.currentTimeMillis(),
    )

    private fun buildOverlayState(
        viewerSession: ViewerSession,
        outputSession: OutputSession,
        recentLogs: List<NdiRedactedLogEntry>,
    ): NdiDeveloperOverlayState {
        val now = System.currentTimeMillis()
        val outputActive = outputSession.inputSourceId.isNotBlank() && outputSession.state !in setOf(OutputState.READY, OutputState.STOPPED)
        if (outputActive) {
            return NdiDeveloperOverlayState(
                visible = true,
                mode = NdiOverlayMode.ACTIVE,
                streamDirectionLabel = "Output",
                streamStatusLabel = outputSession.state.name,
                sessionId = outputSession.sessionId,
                streamSourceLabel = outputSession.inputSourceId,
                warningMessage = outputSession.interruptionReason,
                recentLogs = recentLogs,
                updatedAtEpochMillis = now,
            )
        }

        val viewerActive = viewerSession.selectedSourceId.isNotBlank() && viewerSession.playbackState !in setOf(PlaybackState.IDLE, PlaybackState.STOPPED)
        if (viewerActive) {
            return NdiDeveloperOverlayState(
                visible = true,
                mode = NdiOverlayMode.ACTIVE,
                streamDirectionLabel = "Viewer",
                streamStatusLabel = viewerSession.playbackState.name,
                sessionId = viewerSession.sessionId,
                streamSourceLabel = viewerSession.selectedSourceId,
                warningMessage = viewerSession.interruptionReason,
                recentLogs = recentLogs,
                updatedAtEpochMillis = now,
            )
        }

        return NdiDeveloperOverlayState(
            visible = false,
            mode = NdiOverlayMode.IDLE,
            streamDirectionLabel = "Idle",
            streamStatusLabel = "",
            sessionId = null,
            streamSourceLabel = null,
            warningMessage = null,
            recentLogs = recentLogs,
            updatedAtEpochMillis = now,
        )
    }

    private fun appendViewerLog(session: ViewerSession) {
        if (session.selectedSourceId.isBlank()) return
        appendLog(
            category = NdiLogCategory.VIEWER,
            level = if (session.playbackState == PlaybackState.INTERRUPTED) NdiLogLevel.WARN else NdiLogLevel.INFO,
            message = "viewer ${session.playbackState.name.lowercase()} ${session.selectedSourceId} session ${session.sessionId}",
        )
    }

    private fun appendOutputLog(session: OutputSession) {
        if (session.inputSourceId.isBlank()) return
        appendLog(
            category = NdiLogCategory.OUTPUT,
            level = if (session.state == OutputState.INTERRUPTED) NdiLogLevel.WARN else NdiLogLevel.INFO,
            message = "output ${session.state.name.lowercase()} ${session.inputSourceId} session ${session.sessionId}",
        )
    }

    private fun appendLog(
        category: NdiLogCategory,
        level: NdiLogLevel,
        message: String,
    ) {
        logBuffer.appendLog(category, level, message)
    }

    private fun buildDiscoveryDiagnostics(
        matrixSnapshot: DiscoveryCompatibilitySnapshot,
        recentLogs: List<NdiRedactedLogEntry>,
    ): DeveloperDiscoveryDiagnostics {
        val compatible = matrixSnapshot.results.count { it.status == DiscoveryCompatibilityStatus.COMPATIBLE }
        val limited = matrixSnapshot.results.count { it.status == DiscoveryCompatibilityStatus.LIMITED }
        val incompatible = matrixSnapshot.results.count { it.status == DiscoveryCompatibilityStatus.INCOMPATIBLE }
        val blocked = matrixSnapshot.results.count { it.status == DiscoveryCompatibilityStatus.BLOCKED }

        val summary = "compatibility matrix: compatible=$compatible limited=$limited incompatible=$incompatible blocked=$blocked"
        val compatibilityGuidance = matrixSnapshot.results
            .filter {
                it.status == DiscoveryCompatibilityStatus.INCOMPATIBLE ||
                    it.status == DiscoveryCompatibilityStatus.BLOCKED ||
                    it.status == DiscoveryCompatibilityStatus.LIMITED
            }
            .map { result ->
                val nextStep = when (result.status) {
                    DiscoveryCompatibilityStatus.BLOCKED -> "verify endpoint reachability and rerun validation"
                    DiscoveryCompatibilityStatus.INCOMPATIBLE -> "retry stream-start verification on target"
                    DiscoveryCompatibilityStatus.LIMITED -> "treat target as discovery-only until stream validation succeeds"
                    else -> "no action required"
                }
                val message = when (result.status) {
                    DiscoveryCompatibilityStatus.BLOCKED -> "Target is blocked by environment or endpoint availability"
                    DiscoveryCompatibilityStatus.INCOMPATIBLE -> "Target is incompatible for stream start"
                    DiscoveryCompatibilityStatus.LIMITED -> "Target currently supports discovery-only behavior"
                    else -> "Target is compatible"
                }
                CompatibilityGuidance(
                    targetId = result.targetId,
                    status = result.status,
                    message = message,
                    recommendedNextStep = nextStep,
                    evidenceRef = result.notes,
                )
            }
        val nonCompatibleLines = compatibilityGuidance.map { guidance ->
            "target=${guidance.targetId} status=${guidance.status.name.lowercase()} next=${guidance.recommendedNextStep}"
        }

        return DeveloperDiscoveryDiagnostics(
            developerModeEnabled = false,
            latestDiscoveryRefreshStatus = null,
            latestDiscoveryRefreshAtEpochMillis = matrixSnapshot.recordedAtEpochMillis.takeIf { it > 0L },
            serverStatusRollup = emptyList(),
            recentDiscoveryLogs = listOf(summary) + nonCompatibleLines + recentLogs.map { it.messageRedacted },
            compatibilityGuidance = compatibilityGuidance,
        )
    }

    private fun idleViewerSession(): ViewerSession = ViewerSession(
        sessionId = UUID.randomUUID().toString(),
        selectedSourceId = "",
        playbackState = PlaybackState.IDLE,
        startedAtEpochMillis = 0L,
    )

    private fun idleOutputSession(): OutputSession = OutputSession(
        sessionId = UUID.randomUUID().toString(),
        inputSourceId = "",
        outboundStreamName = "",
        state = OutputState.READY,
        startedAtEpochMillis = 0L,
    )
}
