package com.ndi.feature.ndibrowser.domain.repository

import com.ndi.core.model.DiscoverySnapshot
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

interface NdiDiscoveryRepository {
    suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot

    fun observeDiscoveryState(): Flow<DiscoverySnapshot>

    fun startForegroundAutoRefresh(intervalSeconds: Int = 5)

    fun stopForegroundAutoRefresh()
}

interface NdiViewerRepository {
    suspend fun connectToSource(sourceId: String): ViewerSession

    fun observeViewerSession(): Flow<ViewerSession>

    fun getLatestVideoFrame(): ViewerVideoFrame?

    suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int = 15): ViewerSession

    suspend fun stopViewing()
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
    fun observeDiscoveryEndpoint(): Flow<NdiDiscoveryEndpoint?>
    suspend fun applyDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?): NdiDiscoveryApplyResult
    suspend fun getCurrentEndpoint(): NdiDiscoveryEndpoint?
}

interface DeveloperDiagnosticsRepository {
    fun observeOverlayState(): Flow<NdiDeveloperOverlayState>
    fun observeRecentLogs(): Flow<List<NdiRedactedLogEntry>>
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
}

