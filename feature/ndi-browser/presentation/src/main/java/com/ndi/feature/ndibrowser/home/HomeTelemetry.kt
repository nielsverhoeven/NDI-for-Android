package com.ndi.feature.ndibrowser.home

import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.ndibrowser.domain.repository.HomeDashboardRepository
import com.ndi.feature.ndibrowser.domain.repository.StreamContinuityRepository
import com.ndi.feature.ndibrowser.domain.repository.ViewContinuityRepository

fun interface HomeTelemetryEmitter {
    fun emit(event: TelemetryEvent)
}

/**
 * Service-locator for Home feature dependencies.
 */
object HomeDependencies {
    var homeDashboardRepositoryProvider: (() -> HomeDashboardRepository)? = null
    var streamContinuityRepositoryProvider: (() -> StreamContinuityRepository)? = null
    var viewContinuityRepositoryProvider: (() -> ViewContinuityRepository)? = null
    var telemetryEmitter: HomeTelemetryEmitter = HomeTelemetryEmitter {}

    fun requireHomeDashboardRepository(): HomeDashboardRepository =
        requireNotNull(homeDashboardRepositoryProvider) {
            "HomeDashboardRepository dependency is not configured."
        }.invoke()

    fun requireStreamContinuityRepository(): StreamContinuityRepository =
        requireNotNull(streamContinuityRepositoryProvider) {
            "StreamContinuityRepository dependency is not configured."
        }.invoke()

    fun requireViewContinuityRepository(): ViewContinuityRepository =
        requireNotNull(viewContinuityRepositoryProvider) {
            "ViewContinuityRepository dependency is not configured."
        }.invoke()
}

