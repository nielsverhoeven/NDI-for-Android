package com.ndi.feature.ndibrowser.domain.repository

import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DeveloperDiscoveryDiagnostics
import com.ndi.core.model.DiscoveryCompatibilityResult
import com.ndi.core.model.DiscoveryCompatibilitySnapshot
import com.ndi.core.model.DiscoveryCheckType
import com.ndi.core.model.DiscoveryServerCheckStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.DiscoverySelectionResult
import com.ndi.core.model.DiscoveryServerEntry
import com.ndi.core.model.NdiDeveloperOverlayState
import com.ndi.core.model.NdiDiscoveryApplyResult
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.core.model.NdiRedactedLogEntry
import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.core.model.OutputConfiguration
import com.ndi.core.model.OutputHealthSnapshot
import com.ndi.core.model.OutputSession
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.core.model.ViewerSession
import com.ndi.core.model.SettingsDetailState
import com.ndi.core.model.SettingsLayoutContext
import com.ndi.core.model.SettingsLayoutMode
import com.ndi.core.model.navigation.HomeDashboardSnapshot
import com.ndi.core.model.navigation.NavigationTransitionRecord
import com.ndi.core.model.navigation.NavigationTrigger
import com.ndi.core.model.navigation.BackgroundContinuationReason
import com.ndi.core.model.navigation.StreamContinuityState
import com.ndi.core.model.navigation.TopLevelDestination
import com.ndi.core.model.navigation.TopLevelDestinationState
import com.ndi.core.model.navigation.ViewContinuityState
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.emptyFlow
import kotlinx.coroutines.flow.map

interface NdiDiscoveryRepository {
    suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot

    fun observeDiscoveryState(): Flow<DiscoverySnapshot>

    fun observeCompatibilitySnapshot(): Flow<DiscoveryCompatibilitySnapshot> =
        observeDiscoveryState().map {
            DiscoveryCompatibilitySnapshot(
                recordedAtEpochMillis = System.currentTimeMillis(),
                results = emptyList(),
            )
        }

    fun startForegroundAutoRefresh(intervalSeconds: Int = 5)

    fun stopForegroundAutoRefresh()

    /**
     * Observes the availability debounce history for all discovered sources.
     * Maps sourceId to SourceAvailabilityStatus with two-miss debounce logic applied.
     */
    fun observeAvailabilityHistory(): Flow<Map<String, SourceAvailabilityStatus>> = emptyFlow()

    /**
     * Gets the current availability status for a specific source.
     */
    suspend fun getSourceAvailabilityStatus(sourceId: String): SourceAvailabilityStatus? = null
}

interface NdiViewerRepository {
    suspend fun connectToSource(sourceId: String): ViewerSession

    fun observeViewerSession(): Flow<ViewerSession>

    fun getLatestVideoFrame(): ViewerVideoFrame?

    suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int = 15): ViewerSession

    suspend fun stopViewing()

    /**
     * Applies a quality profile to the active viewer session.
     *
     * Implementations may route this to source-specific handling using the currently active source.
     */
    suspend fun applyQualityProfile(profile: QualityProfile): QualityProfileApplyResult {
        return QualityProfileApplyResult.NOT_SUPPORTED
    }

    suspend fun applyQualityProfile(sourceId: String, profile: QualityProfile) {}

    /**
     * Returns optimization telemetry as a flow for the currently active stream.
     */
    fun getOptimizationStats(): Flow<PlaybackOptimizationState> = emptyFlow()

    /**
     * Returns optimization telemetry as a flow for a specific source.
     */
    fun getOptimizationStats(sourceId: String): Flow<PlaybackOptimizationState> = getOptimizationStats()

    suspend fun getActiveQualityProfile(sourceId: String): QualityProfile {
        return QualityProfile.default()
    }

    fun observeDroppedFramePercent(sourceId: String): Flow<Int> = emptyFlow()

    suspend fun degradeQualityIfNeeded(sourceId: String, droppedFramePercent: Int): QualityProfile {
        return getActiveQualityProfile(sourceId)
    }

    suspend fun handleStreamDisconnection(sourceId: String, maxRetries: Int = 5): Boolean {
        retryReconnectWithinWindow(sourceId, windowSeconds = 15)
        return true
    }
}

enum class QualityProfileApplyResult {
    APPLIED,
    NOT_SUPPORTED,
    FALLBACK,
}

/**
 * Snapshot of playback optimization state used for monitoring and quality decisions.
 */
data class PlaybackOptimizationStats(
    val sourceId: String,
    val smoothPlaybackCount: Int = 0,
    val droppedFrameCount: Int = 0,
    val lastFrameTimeEpochMillis: Long = 0L,
    val averageFrameRate: Double = 0.0,
    val currentFrameRate: Double = 0.0,
    val droppedFramePercent: Int = 0,
    val actualWidth: Int = 0,
    val actualHeight: Int = 0,
    val detectedCodecPreference: String = "adaptive",
    val autoDegradeCount: Int = 0,
    val selectedProfileId: String = QualityProfile.default().id,
    val updatedAtEpochMillis: Long = System.currentTimeMillis(),
)

typealias PlaybackOptimizationState = PlaybackOptimizationStats

/**
 * Disconnection recovery strategy for playback interruption handling.
 */
sealed class DisconnectionRecoveryConfig(
    val maxRetries: Int,
    val initialBackoffMillis: Long,
    val maxBackoffMillis: Long,
) {
    data object Conservative : DisconnectionRecoveryConfig(
        maxRetries = 3,
        initialBackoffMillis = 2_000L,
        maxBackoffMillis = 8_000L,
    )

    data object Standard : DisconnectionRecoveryConfig(
        maxRetries = 5,
        initialBackoffMillis = 1_000L,
        maxBackoffMillis = 15_000L,
    )

    data object Aggressive : DisconnectionRecoveryConfig(
        maxRetries = 8,
        initialBackoffMillis = 500L,
        maxBackoffMillis = 10_000L,
    )
}

interface NdiOutputRepository {
    suspend fun startOutput(inputSourceId: String, streamName: String): OutputSession

    suspend fun stopOutput(): OutputSession

    fun observeOutputSession(): Flow<OutputSession>

    suspend fun retryInterruptedOutputWithinWindow(windowSeconds: Int = 15): OutputSession

    fun observeOutputHealth(): Flow<OutputHealthSnapshot>

    fun isLocalScreenSource(inputSourceId: String): Boolean {
        return inputSourceId.startsWith("device-screen:")
    }
}

data class ScreenCaptureConsentState(
    val sourceId: String,
    val granted: Boolean,
    val tokenRef: String? = null,
)

interface ScreenCaptureConsentRepository {
    suspend fun beginConsentRequest(inputSourceId: String)

    suspend fun registerConsentResult(inputSourceId: String, granted: Boolean, tokenRef: String? = null): ScreenCaptureConsentState

    suspend fun getConsentState(inputSourceId: String): ScreenCaptureConsentState?

    suspend fun clearConsent(inputSourceId: String)
}

interface OutputConfigurationRepository {
    suspend fun savePreferredStreamName(value: String)

    suspend fun getPreferredStreamName(): String

    suspend fun saveLastSelectedInputSource(sourceId: String)

    suspend fun getLastSelectedInputSource(): String?

    suspend fun getConfiguration(): OutputConfiguration
}

interface UserSelectionRepository {
    suspend fun saveLastSelectedSource(sourceId: String)

    suspend fun getLastSelectedSource(): String?
}

data class LastViewedContext(
    val contextId: String = LAST_VIEWED_CONTEXT_ID,
    val sourceId: String,
    val lastFrameImagePath: String? = null,
    val lastFrameCapturedAtEpochMillis: Long? = null,
    val restoredAtEpochMillis: Long? = null,
) {
    companion object {
        const val LAST_VIEWED_CONTEXT_ID = "last_viewed_context"
    }
}

data class ConnectionHistoryState(
    val sourceId: String,
    val previouslyConnected: Boolean = true,
    val firstSuccessfulFrameAtEpochMillis: Long,
    val lastSuccessfulFrameAtEpochMillis: Long,
)

data class SourceAvailabilityStatus(
    val sourceId: String,
    val isAvailable: Boolean,
    val consecutiveMissedPolls: Int = 0,
    val lastSeenAtEpochMillis: Long? = null,
    val lastStatusChangedAtEpochMillis: Long = System.currentTimeMillis(),
)

interface ViewerContinuityRepository {
    fun observeLastViewedContext(): Flow<LastViewedContext?>

    suspend fun getLastViewedContext(): LastViewedContext?

    suspend fun saveLastViewedContext(context: LastViewedContext)

    suspend fun clearLastViewedContext()

    fun observeConnectionHistory(): Flow<List<ConnectionHistoryState>>

    fun observePreviouslyConnectedSourceIds(): Flow<Set<String>>

    suspend fun markSuccessfulFrame(sourceId: String, frameCapturedAtEpochMillis: Long = System.currentTimeMillis())

    suspend fun captureAndSavePreviewFrame(
        sourceId: String,
        frame: ViewerVideoFrame,
        frameCapturedAtEpochMillis: Long = System.currentTimeMillis(),
    ): String? {
        return null
    }

    suspend fun resetPersistedStateOnAppDataClear() {
        clearLastViewedContext()
        clearConnectionHistory()
    }

    suspend fun getConnectionHistory(sourceId: String): ConnectionHistoryState?

    suspend fun hasPreviouslyConnected(sourceId: String): Boolean

    suspend fun clearConnectionHistory()
}

// ---- Spec 003: Three-Screen Navigation repositories ----

/**
 * Manages top-level destination selection state and persist/restore semantics.
 */
interface TopLevelNavigationRepository {
    fun observeTopLevelDestination(): Flow<TopLevelDestinationState>

    suspend fun selectTopLevelDestination(
        destination: TopLevelDestination,
        trigger: NavigationTrigger,
    ): NavigationTransitionRecord

    suspend fun getLastTopLevelDestination(): TopLevelDestination?

    suspend fun saveLastTopLevelDestination(destination: TopLevelDestination)
}

/**
 * Aggregates non-sensitive status summaries for the Home dashboard.
 */
interface HomeDashboardRepository {
    fun observeDashboardSnapshot(): Flow<HomeDashboardSnapshot>

    suspend fun refreshDashboardSnapshot(): HomeDashboardSnapshot
}

/**
 * Tracks Stream/output continuity when navigating between top-level destinations.
 * Active output must NOT be stopped by top-level navigation or app backgrounding.
 * Continuity state is cleared only when an explicit stop is processed.
 */
interface StreamContinuityRepository {
    fun observeContinuityState(): Flow<StreamContinuityState>

    suspend fun captureLastKnownState()

    suspend fun markAppBackgrounded(reason: BackgroundContinuationReason)

    suspend fun markAppForegrounded()

    suspend fun clearTransientStateOnExplicitStop()
}

/**
 * Tracks View/playback continuity when navigating between top-level destinations.
 * Playback MUST be stopped when leaving the View destination.
 */
interface ViewContinuityRepository {
    fun observeContinuityState(): Flow<ViewContinuityState>

    suspend fun stopForTopLevelNavigation()

    suspend fun getLastSelectedSourceId(): String?
}

// ---- Spec 006: Settings Menu repositories ----

interface NdiSettingsRepository {
    suspend fun getSettings(): NdiSettingsSnapshot
    suspend fun saveSettings(snapshot: NdiSettingsSnapshot)
    fun observeSettings(): Flow<NdiSettingsSnapshot>
}

interface NdiDiscoveryConfigRepository {
    fun observeDiscoveryEndpoints(): Flow<List<NdiDiscoveryEndpoint>>

    fun observeDiscoveryEndpoint(): Flow<NdiDiscoveryEndpoint?>

    suspend fun applyDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?): NdiDiscoveryApplyResult

    suspend fun getCurrentEndpoints(): List<NdiDiscoveryEndpoint>

    suspend fun getCurrentEndpoint(): NdiDiscoveryEndpoint?
}

interface DeveloperDiagnosticsRepository {
    fun observeOverlayState(): Flow<NdiDeveloperOverlayState>
    fun observeRecentLogs(): Flow<List<NdiRedactedLogEntry>>
    /**
     * Observes aggregated developer diagnostics for discovery operations.
     * Returns empty/default state when developer mode is OFF.
     */
    fun observeDiscoveryDiagnostics(): Flow<DeveloperDiscoveryDiagnostics> = emptyFlow()
}

interface DiscoveryCompatibilityMatrixRepository {
    fun observeMatrixSnapshot(): Flow<DiscoveryCompatibilitySnapshot>

    suspend fun upsertResults(
        results: List<DiscoveryCompatibilityResult>,
        recordedAtEpochMillis: Long = System.currentTimeMillis(),
    )

    suspend fun getCurrentMatrix(): DiscoveryCompatibilitySnapshot
}

interface SettingsLayoutModeResolver {
    fun resolve(widthDp: Int, isLandscape: Boolean): SettingsLayoutMode
}

interface SettingsWorkspaceStateRepository {
    suspend fun saveLastSelectedCategoryId(categoryId: String)
    suspend fun getLastSelectedCategoryId(): String?
    fun buildDetailState(categoryId: String?): SettingsDetailState
    fun buildLayoutContext(widthDp: Int, isLandscape: Boolean): SettingsLayoutContext
}

// ---- Spec 018: Discovery Server Management ----

/**
 * Manages the ordered collection of user-configured discovery server entries.
 * All mutations are persisted in Room and survive app restart.
 */
interface DiscoveryServerRepository {
    /** Emits ordered list of all entries whenever any entry changes. */
    fun observeServers(): Flow<List<DiscoveryServerEntry>>

    /**
     * Add a new server. Trims hostOrIp, applies default port 5959 when portInput is blank.
     * @throws IllegalArgumentException on invalid hostOrIp, invalid port, or duplicate (host+port).
     */
    suspend fun addServer(hostOrIp: String, portInput: String): DiscoveryServerEntry

    /**
     * Update an existing entry. Trims hostOrIp, applies default port when portInput is blank.
     * @throws IllegalArgumentException on invalid values or duplicate (host+port) conflicts.
     * @throws NoSuchElementException when id does not exist.
     */
    suspend fun updateServer(id: String, hostOrIp: String, portInput: String): DiscoveryServerEntry

    /**
     * Remove an entry by id. No-op if id does not exist.
     */
    suspend fun removeServer(id: String)

    /**
     * Persist the enabled/disabled state of an entry.
     * @throws NoSuchElementException when id does not exist.
     */
    suspend fun setServerEnabled(id: String, enabled: Boolean): DiscoveryServerEntry

    /**
     * Reorder entries according to the supplied id list.
     * Returns the new ordered list.
     */
    suspend fun reorderServers(idsInOrder: List<String>): List<DiscoveryServerEntry>

    /**
     * Attempt ordered, sequential failover across enabled entries.
     * Returns the first reachable entry or an appropriate error outcome.
     */
    suspend fun resolveActiveDiscoveryTarget(): DiscoverySelectionResult

    /**
     * Performs a protocol-level discovery connection check for the given server entry.
     * Called automatically after addServer and on-demand via recheckServer.
     * @return DiscoveryServerCheckStatus with check outcome and failure details.
     */
    suspend fun performDiscoveryServerCheck(
        serverId: String,
        correlationId: String,
    ): DiscoveryServerCheckStatus

    /**
     * Rechecks only the targeted server's connectivity.
     * Does NOT alter orderIndex, enabled state, or other server entries.
     * @throws NoSuchElementException when serverId does not exist.
     */
    suspend fun recheckServer(
        serverId: String,
        correlationId: String,
    ): DiscoveryServerCheckStatus

    /**
     * Returns the latest check status for a registered server, or null if not yet checked.
     */
    suspend fun getServerCheckStatus(serverId: String): DiscoveryServerCheckStatus?

    /**
     * Observes the check status for a specific server entry.
     */
    fun observeServerCheckStatus(serverId: String): Flow<DiscoveryServerCheckStatus?>
}

// ---- Spec 023: Per-Source Frame Retention ----

/**
 * Manages session-scoped retention of thumbnail frames (one per NDI source).
 * Retained frames are in-memory only (no disk persistence); discarded on app exit.
 * LRU eviction triggers when max concurrent retained sources (10) is exceeded.
 *
 * Frame capture happens when user exits viewer for a source (last frame at exit time).
 * Frames are scaled to thumbnail resolution (~320×height) and stored in session cache directory.
 */
interface PerSourceFrameRepository {
    /**
     * Captures and stores the last frame for a given source.
     * If frame is null, no-op (source viewed but no frame available).
     * If sourceId is blank, no-op.
     * Overwrites existing frame for the same source; evicts LRU entry if cap exceeded.
     */
    suspend fun saveFrameForSource(sourceId: String, frame: ViewerVideoFrame?)

    /**
     * Returns the absolute file path to the thumbnail PNG for a source, or null if not retained.
     */
    suspend fun getFramePathForSource(sourceId: String): String?

    /**
     * Observes the current per-source frame map as sourceId → thumbnailFilePath.
     * Emits empty map initially; updates whenever frames are saved or evicted.
     */
    fun observeFrameMap(): kotlinx.coroutines.flow.StateFlow<Map<String, String>>

    /**
     * Clears all retained frames and emits empty map.
     * Useful for app data clear or session cleanup.
     */
    suspend fun clearAll()

    companion object {
        const val MAX_RETAINED_SOURCES = 10
    }
}

