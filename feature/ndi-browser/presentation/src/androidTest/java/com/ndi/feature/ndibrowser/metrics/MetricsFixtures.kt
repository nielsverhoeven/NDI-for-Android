package com.ndi.feature.ndibrowser.metrics

import com.ndi.feature.ndibrowser.source_list.SourceListDependencies
import com.ndi.feature.ndibrowser.source_list.SourceListTelemetryEmitter
import com.ndi.feature.ndibrowser.viewer.ViewerDependencies
import com.ndi.feature.ndibrowser.viewer.ViewerTelemetryEmitter
import kotlin.math.ceil

fun percentile90(values: List<Long>): Long {
    require(values.isNotEmpty()) { "Cannot compute percentile for an empty dataset." }
    val sorted = values.sorted()
    val rank = ceil(sorted.size * 0.9).toInt().coerceIn(1, sorted.size)
    return sorted[rank - 1]
}

fun successRate(outcomes: List<Boolean>): Double {
    require(outcomes.isNotEmpty()) { "Cannot compute success rate for an empty dataset." }
    return outcomes.count { it }.toDouble() / outcomes.size.toDouble()
}

fun clearMetricDependencies() {
    SourceListDependencies.discoveryRepositoryProvider = null
    SourceListDependencies.userSelectionRepositoryProvider = null
    SourceListDependencies.viewerNavigationRequestProvider = null
    SourceListDependencies.telemetryEmitter = SourceListTelemetryEmitter {}
    ViewerDependencies.viewerRepositoryProvider = null
    ViewerDependencies.userSelectionRepositoryProvider = null
    ViewerDependencies.telemetryEmitter = ViewerTelemetryEmitter {}
}