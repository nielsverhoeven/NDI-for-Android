package com.ndi.feature.themeeditor

import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.themeeditor.domain.repository.ThemeEditorRepository

fun interface ThemeEditorTelemetryEmitter {
    fun emit(event: TelemetryEvent)
}

object ThemeEditorDependencies {
    var themeEditorRepositoryProvider: (() -> ThemeEditorRepository)? = null
    var telemetryEmitter: ThemeEditorTelemetryEmitter = ThemeEditorTelemetryEmitter {}

    fun requireThemeEditorRepository(): ThemeEditorRepository {
        return requireNotNull(themeEditorRepositoryProvider) {
            "Theme editor repository dependency is not configured."
        }.invoke()
    }
}
