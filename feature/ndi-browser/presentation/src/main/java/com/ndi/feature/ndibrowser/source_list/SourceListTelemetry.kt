package com.ndi.feature.ndibrowser.source_list

import androidx.navigation.NavDeepLinkRequest
import com.ndi.core.model.DiscoveryCompatibilitySnapshot
import com.ndi.core.model.DiscoveryCompatibilityStatus
import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.feature.ndibrowser.domain.repository.ViewerContinuityRepository
import com.ndi.feature.ndibrowser.domain.repository.PerSourceFrameRepository
import kotlinx.coroutines.flow.Flow

fun interface SourceListTelemetryEmitter {
    fun emit(event: TelemetryEvent)
}

object SourceListDependencies {
    var discoveryRepositoryProvider: (() -> NdiDiscoveryRepository)? = null
    var userSelectionRepositoryProvider: (() -> UserSelectionRepository)? = null
    var viewerNavigationRequestProvider: ((String) -> NavDeepLinkRequest)? = null
    var outputNavigationRequestProvider: ((String) -> NavDeepLinkRequest)? = null
    var fallbackWarningProvider: (() -> Flow<String?>)? = null
    var overlayStateProvider: (() -> Flow<com.ndi.feature.ndibrowser.settings.OverlayDisplayState?>)? = null
    var viewerContinuityRepositoryProvider: (() -> ViewerContinuityRepository)? = null
    var perSourceFrameRepositoryProvider: (() -> PerSourceFrameRepository)? = null
    var telemetryEmitter: SourceListTelemetryEmitter = SourceListTelemetryEmitter {}

    fun requireDiscoveryRepository(): NdiDiscoveryRepository {
        return requireNotNull(discoveryRepositoryProvider) { "SourceList discovery repository dependency is not configured." }.invoke()
    }

    fun requireUserSelectionRepository(): UserSelectionRepository {
        return requireNotNull(userSelectionRepositoryProvider) { "SourceList selection repository dependency is not configured." }.invoke()
    }

    fun viewerNavigationRequest(sourceId: String): NavDeepLinkRequest {
        return requireNotNull(viewerNavigationRequestProvider) { "Source list viewer navigation is not configured." }.invoke(sourceId)
    }

    fun outputNavigationRequest(sourceId: String): NavDeepLinkRequest {
        return requireNotNull(outputNavigationRequestProvider) { "Source list output navigation is not configured." }.invoke(sourceId)
    }

    fun fallbackWarningFlowOrNull(): Flow<String?>? = fallbackWarningProvider?.invoke()

    fun overlayStateFlowOrNull(): Flow<com.ndi.feature.ndibrowser.settings.OverlayDisplayState?>? = overlayStateProvider?.invoke()

    fun viewerContinuityRepositoryOrNull(): ViewerContinuityRepository? = viewerContinuityRepositoryProvider?.invoke()
    fun perSourceFrameRepositoryOrNull(): PerSourceFrameRepository? = perSourceFrameRepositoryProvider?.invoke()
}

object SourceListTelemetry {

    fun fromSnapshot(snapshot: DiscoverySnapshot): TelemetryEvent {
        val name = when (snapshot.status) {
            DiscoveryStatus.IN_PROGRESS -> "discovery_started"
            DiscoveryStatus.SUCCESS, DiscoveryStatus.EMPTY -> "discovery_completed"
            DiscoveryStatus.FAILURE -> "discovery_failed"
        }
        return TelemetryEvent(
            name = name,
            timestampEpochMillis = snapshot.completedAtEpochMillis,
            attributes = mapOf(
                "status" to snapshot.status.name,
                "sourceCount" to snapshot.sourceCount.toString(),
            ),
        )
    }

    fun sourceSelected(sourceId: String): TelemetryEvent {
        return TelemetryEvent(
            name = "source_selected",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("sourceId" to sourceId),
        )
    }

    fun viewSelectionOpenedViewer(sourceId: String): TelemetryEvent {
        return TelemetryEvent(
            name = TelemetryEvent.VIEW_SELECTION_OPENED_VIEWER,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("sourceId" to sourceId),
        )
    }

    /**
     * T038: Telemetry event for blocked source selection.
     * Emitted when user attempts to select an unavailable source.
     */
    fun sourceSelectionBlocked(sourceId: String, reason: String): TelemetryEvent {
        return TelemetryEvent(
            name = "source_selection_blocked",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "reason" to reason,
            ),
        )
    }

    /**
     * T038: Telemetry event for availability state changes.
     * Emitted when a source transitions from available to unavailable or vice versa.
     */
    fun availabilityStateChanged(sourceId: String, isAvailable: Boolean): TelemetryEvent {
        return TelemetryEvent(
            name = "source_availability_changed",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "isAvailable" to isAvailable.toString(),
            ),
        )
    }

    fun partialCompatibilityDetected(snapshot: DiscoveryCompatibilitySnapshot): TelemetryEvent {
        val blockedCount = snapshot.results.count { it.status == DiscoveryCompatibilityStatus.BLOCKED }
        val incompatibleCount = snapshot.results.count { it.status == DiscoveryCompatibilityStatus.INCOMPATIBLE }
        val compatibleOrLimitedCount = snapshot.results.count {
            it.status == DiscoveryCompatibilityStatus.COMPATIBLE ||
                it.status == DiscoveryCompatibilityStatus.LIMITED
        }
        return TelemetryEvent(
            name = "discovery_partial_compatibility_detected",
            timestampEpochMillis = snapshot.recordedAtEpochMillis,
            attributes = mapOf(
                "blockedCount" to blockedCount.toString(),
                "incompatibleCount" to incompatibleCount.toString(),
                "usableCount" to compatibleOrLimitedCount.toString(),
            ),
        )
    }
}
