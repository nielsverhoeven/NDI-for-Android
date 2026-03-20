package com.ndi.feature.ndibrowser.source_list

import androidx.navigation.NavDeepLinkRequest
import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository

fun interface SourceListTelemetryEmitter {
    fun emit(event: TelemetryEvent)
}

object SourceListDependencies {
    var discoveryRepositoryProvider: (() -> NdiDiscoveryRepository)? = null
    var userSelectionRepositoryProvider: (() -> UserSelectionRepository)? = null
    var viewerNavigationRequestProvider: ((String) -> NavDeepLinkRequest)? = null
    var outputNavigationRequestProvider: ((String) -> NavDeepLinkRequest)? = null
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
}
