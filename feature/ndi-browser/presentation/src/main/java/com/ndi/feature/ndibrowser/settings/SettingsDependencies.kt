package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.ndibrowser.domain.repository.CachedSourceRepository
import com.ndi.feature.ndibrowser.domain.repository.DeveloperDiagnosticsRepository
import com.ndi.feature.ndibrowser.domain.repository.DiscoveryServerRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import com.ndi.feature.ndibrowser.domain.repository.SettingsLayoutModeResolver
import kotlinx.coroutines.flow.Flow

fun interface SettingsTelemetryEmitter {
    fun emit(event: TelemetryEvent)
}

object SettingsDependencies {
    var settingsRepositoryProvider: (() -> NdiSettingsRepository)? = null
    var developerDiagnosticsRepositoryProvider: (() -> DeveloperDiagnosticsRepository)? = null
    var discoveryServerRepositoryProvider: (() -> DiscoveryServerRepository)? = null
    var cachedSourceRepositoryProvider: (() -> CachedSourceRepository)? = null
    var overlayStateProvider: (() -> Flow<OverlayDisplayState?>)? = null
    var settingsNavigationBackProvider: (() -> Unit)? = null
    var telemetryEmitter: SettingsTelemetryEmitter = SettingsTelemetryEmitter {}
    /** Override the layout resolver in tests to simulate specific form factors. */
    var layoutResolverProvider: (() -> SettingsLayoutModeResolver)? = null

    fun requireSettingsRepository(): NdiSettingsRepository =
        requireNotNull(settingsRepositoryProvider) {
            "Settings repository dependency is not configured."
        }.invoke()

    fun requireDeveloperDiagnosticsRepository(): DeveloperDiagnosticsRepository =
        requireNotNull(developerDiagnosticsRepositoryProvider) {
            "Developer diagnostics repository dependency is not configured."
        }.invoke()

    fun requireDiscoveryServerRepository(): DiscoveryServerRepository =
        requireNotNull(discoveryServerRepositoryProvider) {
            "Discovery server repository dependency is not configured."
        }.invoke()

    fun cachedSourceRepositoryOrNull(): CachedSourceRepository? = cachedSourceRepositoryProvider?.invoke()

    fun overlayStateFlowOrNull(): Flow<OverlayDisplayState?>? = overlayStateProvider?.invoke()
}
