package com.ndi.feature.ndibrowser.domain.repository

import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.OutputConfiguration
import com.ndi.core.model.OutputHealthSnapshot
import com.ndi.core.model.OutputSession
import com.ndi.core.model.ViewerSession
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

