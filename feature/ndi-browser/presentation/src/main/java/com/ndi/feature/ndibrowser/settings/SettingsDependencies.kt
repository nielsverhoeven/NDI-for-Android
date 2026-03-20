package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.ndibrowser.domain.repository.DeveloperDiagnosticsRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository

fun interface SettingsTelemetryEmitter {
    fun emit(event: TelemetryEvent)
}

object SettingsDependencies {
    var settingsRepositoryProvider: (() -> NdiSettingsRepository)? = null
    var developerDiagnosticsRepositoryProvider: (() -> DeveloperDiagnosticsRepository)? = null
    var settingsNavigationBackProvider: (() -> Unit)? = null
    var telemetryEmitter: SettingsTelemetryEmitter = SettingsTelemetryEmitter {}

    fun requireSettingsRepository(): NdiSettingsRepository =
        requireNotNull(settingsRepositoryProvider) {
            "Settings repository dependency is not configured."
        }.invoke()

    fun requireDeveloperDiagnosticsRepository(): DeveloperDiagnosticsRepository =
        requireNotNull(developerDiagnosticsRepositoryProvider) {
            "Developer diagnostics repository dependency is not configured."
        }.invoke()
}
