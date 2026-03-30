package com.ndi.feature.ndibrowser.viewer

import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.QualityProfileRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.feature.ndibrowser.domain.repository.ViewerContinuityRepository
import com.ndi.feature.ndibrowser.settings.OverlayDisplayState
import kotlinx.coroutines.flow.Flow

fun interface ViewerTelemetryEmitter {
    fun emit(event: TelemetryEvent)
}

object ViewerDependencies {
    var viewerRepositoryProvider: (() -> NdiViewerRepository)? = null
    var qualityProfileRepositoryProvider: (() -> QualityProfileRepository)? = null
    var userSelectionRepositoryProvider: (() -> UserSelectionRepository)? = null
    var viewerContinuityRepositoryProvider: (() -> ViewerContinuityRepository)? = null
    var overlayStateProvider: (() -> Flow<OverlayDisplayState?>)? = null
    var telemetryEmitter: ViewerTelemetryEmitter = ViewerTelemetryEmitter {}

    fun requireViewerRepository(): NdiViewerRepository {
        return requireNotNull(viewerRepositoryProvider) { "Viewer repository dependency is not configured." }.invoke()
    }

    fun requireQualityProfileRepository(): QualityProfileRepository {
        return requireNotNull(qualityProfileRepositoryProvider) {
            "Viewer quality repository dependency is not configured."
        }.invoke()
    }

    fun qualityProfileRepositoryOrNull(): QualityProfileRepository? = qualityProfileRepositoryProvider?.invoke()

    fun requireUserSelectionRepository(): UserSelectionRepository {
        return requireNotNull(userSelectionRepositoryProvider) { "Viewer selection repository dependency is not configured." }.invoke()
    }

    fun viewerContinuityRepositoryOrNull(): ViewerContinuityRepository? = viewerContinuityRepositoryProvider?.invoke()

    fun overlayStateFlowOrNull(): Flow<OverlayDisplayState?>? = overlayStateProvider?.invoke()
}

object ViewerTelemetry {

    fun playbackStarted(sourceId: String): TelemetryEvent {
        return TelemetryEvent(
            name = "playback_started",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("sourceId" to sourceId),
        )
    }

    fun playbackStopped(sourceId: String): TelemetryEvent {
        return TelemetryEvent(
            name = "playback_stopped",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("sourceId" to sourceId),
        )
    }

    fun restoreContextApplied(sourceId: String, hasSavedPreview: Boolean): TelemetryEvent {
        return TelemetryEvent(
            name = "viewer_restore_context_applied",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "hasSavedPreview" to hasSavedPreview.toString(),
            ),
        )
    }

    fun restoreUnavailableNoAutoplay(sourceId: String, hasSavedPreview: Boolean): TelemetryEvent {
        return TelemetryEvent(
            name = "viewer_restore_unavailable_no_autoplay",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "sourceId" to sourceId,
                "hasSavedPreview" to hasSavedPreview.toString(),
            ),
        )
    }

    fun restorePreviewRendered(sourceId: String): TelemetryEvent {
        return TelemetryEvent(
            name = "viewer_restore_preview_rendered",
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("sourceId" to sourceId),
        )
    }
}
