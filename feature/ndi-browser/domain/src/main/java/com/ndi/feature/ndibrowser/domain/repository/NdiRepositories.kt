package com.ndi.feature.ndibrowser.domain.repository

import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.OutputConfiguration
import com.ndi.core.model.OutputHealthSnapshot
import com.ndi.core.model.OutputSession
import com.ndi.core.model.ViewerSession
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
